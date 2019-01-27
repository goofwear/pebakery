﻿/*
    MIT License (MIT)

    Copyright (c) 2018 Hajin Jang
	
	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:
	
	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.
	
	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

// ReSharper disable InconsistentNaming
namespace PEBakery.WPF.Controls
{
    public class DragCanvas : Canvas
    {
        #region Enums and Const
        public enum DragMode
        {
            SingleMove,
            MultiMove,
            SingleResize,
            MultiResize,
        }

        public enum ResizeClickPosition
        {
            Left,
            Right,
            Top,
            Bottom,
            LeftTop,
            RightTop,
            LeftBottom,
            RightBottom,
            Inside,
            Outside,
        }

        // HeightLimit
        public const int CanvasWidthHeightLimit = 600;
        public const int ElementWidthHeightLimit = 100;

        // DragHandle
        private const int DragHandleLength = 6;
        private const int DragHandleShowThreshold = 20;
        #endregion

        #region Fields, Properties
        // SelectedElement
        private readonly List<SelectedElement> _selectedElements = new List<SelectedElement>();
        private int _selectedElementIndex = -1;
        private ResizeClickPosition _selectedClickPos;
        /// <summary>
        /// Helper for single move/resize of SelectedElements
        /// </summary>
        private SelectedElement _selected
        {
            get
            {
                if (!(0 <= _selectedElementIndex && _selectedElementIndex < _selectedElements.Count))
                    return null;
                return _selectedElements[_selectedElementIndex];
            }
        }

        // Drag
        private DragMode _dragMode;
        private bool _isBeingDragged;
        private Point _dragStartCursorPos;

        // Max Z Index
        private int MaxZIndex
        {
            get
            {
                int max = GetZIndex(this);
                foreach (UIElement element in Children)
                {
                    int z = GetZIndex(element);
                    if (max < z)
                        max = z;
                }
                return max;
            }
        }
        #endregion

        #region Constructor
        public DragCanvas()
        {
            // Set Background to always fire OnPreviewMouseMove on DragCanvas
            Background = Brushes.Transparent;
        }
        #endregion

        #region Events
        public class UIControlSelectedEventArgs : EventArgs
        {
            public UIControl UIControl { get; set; }
            public List<UIControl> UIControls { get; set; }
            public bool MultiSelect => UIControls != null;
            public UIControlSelectedEventArgs() { }
            public UIControlSelectedEventArgs(UIControl uiCtrl)
            {
                UIControl = uiCtrl;
            }
            public UIControlSelectedEventArgs(List<UIControl> uiCtrls)
            {
                UIControls = uiCtrls;
            }
        }
        public delegate void UIControlSelectedEventHandler(object sender, UIControlSelectedEventArgs e);
        public event UIControlSelectedEventHandler UIControlSelected;

        public class UIControlDraggedEventArgs : EventArgs
        {
            public UIControl UIControl { get; set; }
            public List<UIControl> UIControls { get; set; }
            public bool MultiSelect => UIControls != null;
            public double DeltaX { get; set; }
            public double DeltaY { get; set; }
            public bool ForceUpdate { get; set; }
            public UIControlDraggedEventArgs(UIControl uiCtrl, double deltaX, double deltaY, bool forceUpdate = false)
            {
                UIControl = uiCtrl;
                DeltaX = deltaX;
                DeltaY = deltaY;
                ForceUpdate = forceUpdate;
            }
            public UIControlDraggedEventArgs(List<UIControl> uiCtrls, double deltaX, double deltaY, bool forceUpdate = false)
            {
                UIControls = uiCtrls;
                DeltaX = deltaX;
                DeltaY = deltaY;
                ForceUpdate = forceUpdate;
            }
        }
        public delegate void UIControlMovedEventHandler(object sender, UIControlDraggedEventArgs e);
        public event UIControlMovedEventHandler UIControlMoved;

        public delegate void UIControlResizedEventHandler(object sender, UIControlDraggedEventArgs e);
        public event UIControlResizedEventHandler UIControlResized;
        #endregion

        #region Mouse Event Handler
        /// <inheritdoc />
        /// <summary>
        /// Clear mouse cursor when mouse is not hovering DragCanvas, such as close of the window.
        /// </summary>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            // Sometime this is called outside of STA thread
            Dispatcher.Invoke(ResetMouseCursor);
        }

        /// <inheritdoc />
        /// <summary>
        /// Start dragging
        /// </summary>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (_isBeingDragged)
                return;

            FrameworkElement focusedElement = FindRootFrameworkElement(e.Source);

            // No UIControl was selected
            if (focusedElement == null)
            { // Clicked background -> Reset selected elements
                _isBeingDragged = false;
                ClearSelectedElements(true);
                UIControlSelected?.Invoke(this, new UIControlSelectedEventArgs());
                return;
            }

            // Multi-select mode handling
            bool addMultiSelect = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control || (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool isMultiSelect = addMultiSelect || 2 <= _selectedElements.Count;

            if (focusedElement is Border dragHandle && dragHandle.Tag is DragHandleTag info)
            { // Clicked drag handle
                // Resize mode
                _dragMode = DragMode.SingleResize;

                // Record select information
                _isBeingDragged = true;
                _selectedClickPos = info.ClickPos;
                _selectedElementIndex = _selectedElements.FindIndex(x => x.Element.Equals(info.Parent));
                Debug.Assert(_selectedElementIndex != -1, "Incorrect SelectedElement handling");
                SelectedElement selected = _selectedElements[_selectedElementIndex];

                // Record position and size
                _dragStartCursorPos = e.GetPosition(this);
                selected.ElementInitialRect = GetElementSize(selected.Element);

                // Set Cursor
                SetMouseCursor(_selectedClickPos);

                // Capture mouse
                CaptureMouse();
            }
            else if (focusedElement.Tag is UIControl)
            { // Clicked UIControl
                // Move mode
                // if (addMultiSelect)
                //    ClearSelectedElements(false);
                
                if (isMultiSelect)
                {
                    _dragMode = DragMode.MultiMove;
                }
                else
                {
                    ClearSelectedElements(true);
                    _dragMode = DragMode.SingleMove;
                }

                // Record select information
                SelectedElement selected = new SelectedElement(focusedElement);
                int idx = _selectedElements.FindIndex(x => x.Element.Equals(focusedElement));
                if (_dragMode == DragMode.SingleMove || _dragMode == DragMode.MultiMove && idx == -1)
                    _selectedElements.Add(selected); // Add to list only if (1) single-select mode or (2) multi-select and the element is not added yet
                _selectedElementIndex = idx == -1 ? _selectedElements.Count - 1 : idx;
                _selectedClickPos = ResizeClickPosition.Inside;
                _isBeingDragged = true;

                // Record position and size
                _dragStartCursorPos = e.GetPosition(this);

                // Draw border and drag handles
                DrawSelectedElements();
                
                // Set Cursor
                SetMouseCursor();

                // Capture mouse
                CaptureMouse();
            }
            else
            { // Clicked background -> Reset selected elements
                _isBeingDragged = false;
                ClearSelectedElements(true);
            }

            e.Handled = true;
        }

        /// <inheritdoc />
        /// <summary>
        /// Middle of dragging 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            // Change cursor following underlying element
            if (!_isBeingDragged)
            {
                FrameworkElement hoverElement = FindRootFrameworkElement(e.Source);
                if (hoverElement == null) // Outside
                    ResetMouseCursor();
                else if (hoverElement is Border border && border.Tag is DragHandleTag info)
                    SetMouseCursor(info.ClickPos);
                else if (hoverElement.Tag is UIControl) // Inside
                    SetMouseCursor();
                else
                    ResetMouseCursor();
                return;
            }

            Debug.Assert(0 < _selectedElements.Count, "Incorrect SelectedElement handling");

            // Moving/Resizing a UIControl
            Point nowCursorPos = e.GetPosition(this);

            switch (_dragMode)
            {
                case DragMode.SingleMove:
                    {
                        Debug.Assert(_selected != null, "SelectedElement is null");
                        Point dragStartElementPos = new Point(_selected.ElementInitialRect.X, _selected.ElementInitialRect.Y);
                        Point newElementPos = CalcNewPosition(_dragStartCursorPos, nowCursorPos, dragStartElementPos);
                        Rect r = new Rect
                        {
                            X = newElementPos.X,
                            Y = newElementPos.Y,
                            Width = _selected.ElementInitialRect.Width,
                            Height = _selected.ElementInitialRect.Height,
                        };
                        MoveSelectedElement(_selected, r);
                    }
                    break;
                case DragMode.MultiMove:
                    {
                        foreach (SelectedElement selected in _selectedElements)
                        {
                            Point dragStartElementPos = new Point(selected.ElementInitialRect.X, selected.ElementInitialRect.Y);
                            Point newElementPos = CalcNewPosition(_dragStartCursorPos, nowCursorPos, dragStartElementPos);
                            Rect r = new Rect
                            {
                                X = newElementPos.X,
                                Y = newElementPos.Y,
                                Width = selected.ElementInitialRect.Width,
                                Height = selected.ElementInitialRect.Height,
                            };
                            MoveSelectedElement(selected, r);
                        }
                    }
                    break;
                case DragMode.SingleResize:
                    {
                        Debug.Assert(_selected != null, "SelectedElement is null");
                        Rect newElementRect = CalcNewSize(_dragStartCursorPos, nowCursorPos, _selected.ElementInitialRect, _selectedClickPos);
                        ResizeSelectedElements(_selected, newElementRect);
                    }
                    break;
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// End of dragging
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!_isBeingDragged)
                return;

            Debug.Assert(_selected != null, "SelectedElement was not set");
            ReleaseMouseCapture();

            double deltaX;
            double deltaY;
            Point nowCursorPoint = e.GetPosition(this);
            switch (_dragMode)
            {
                case DragMode.SingleMove:
                    {
                        UIControl uiCtrl = _selected.UIControl;

                        Point newCtrlPos = CalcNewPosition(_dragStartCursorPos, nowCursorPoint, new Point(uiCtrl.X, uiCtrl.Y));
                        deltaX = newCtrlPos.X - uiCtrl.X;
                        deltaY = newCtrlPos.Y - uiCtrl.Y;

                        // UIControl should have position/size of int
                        uiCtrl.X = (int)newCtrlPos.X;
                        uiCtrl.Y = (int)newCtrlPos.Y;

                        UIControlMoved?.Invoke(this, new UIControlDraggedEventArgs(uiCtrl, deltaX, deltaY));
                    }
                    break;
                case DragMode.MultiMove:
                    {
                        double maxDeltaX = 0;
                        double maxDeltaY = 0;
                        List<UIControl> uiCtrls = new List<UIControl>(_selectedElements.Count);
                        foreach (SelectedElement selected in _selectedElements)
                        {
                            UIControl uiCtrl = selected.UIControl;
                            uiCtrls.Add(uiCtrl);

                            Point newCtrlPos = CalcNewPosition(_dragStartCursorPos, nowCursorPoint, new Point(uiCtrl.X, uiCtrl.Y));
                            deltaX = newCtrlPos.X - uiCtrl.X;
                            deltaY = newCtrlPos.Y - uiCtrl.Y;
                            maxDeltaX = Math.Max(deltaX, maxDeltaX);
                            maxDeltaY = Math.Max(deltaY, maxDeltaY);

                            // UIControl should have position/size of int
                            uiCtrl.X = (int)newCtrlPos.X;
                            uiCtrl.Y = (int)newCtrlPos.Y;
                        }

                        UIControlMoved?.Invoke(this, new UIControlDraggedEventArgs(uiCtrls, maxDeltaX, maxDeltaY));
                    }
                    break;
                case DragMode.SingleResize:
                    {
                        UIControl uiCtrl = _selected.UIControl;

                        Rect newCtrlRect = CalcNewSize(_dragStartCursorPos, nowCursorPoint, uiCtrl.Rect, _selectedClickPos);
                        deltaX = newCtrlRect.X - uiCtrl.X;
                        deltaY = newCtrlRect.Y - uiCtrl.Y;

                        // UIControl should have position/size of int
                        uiCtrl.X = (int)newCtrlRect.X;
                        uiCtrl.Y = (int)newCtrlRect.Y;
                        uiCtrl.Width = (int)newCtrlRect.Width;
                        uiCtrl.Height = (int)newCtrlRect.Height;

                        UIControlResized?.Invoke(this, new UIControlDraggedEventArgs(uiCtrl, deltaX, deltaY));
                    }
                    break;
            }

            ResetMouseCursor();

            _isBeingDragged = false;
        }
        #endregion

        #region (public) SelectedElements
        /// <summary>
        /// Clear border and drag handles around selected element
        /// </summary>
        public void ClearSelectedElements(bool clearList)
        {
            foreach (SelectedElement selected in _selectedElements)
            {
                if (selected.Border != null)
                    UIRenderer.RemoveFromCanvas(this, selected.Border);
                foreach (Border dragHandle in selected.DragHandles)
                    UIRenderer.RemoveFromCanvas(this, dragHandle);
            }

            if (clearList)
                _selectedElements.Clear();
        }

        /// <summary>
        /// Draw border and drag handles around selected elements
        /// </summary>
        public void DrawSelectedElements()
        {
            ClearSelectedElements(false);

            if (1 < _selectedElements.Count)
            {
                List<UIControl> uiCtrls = new List<UIControl>(_selectedElements.Count);
                foreach (SelectedElement selected in _selectedElements)
                {
                    uiCtrls.Add(selected.UIControl);
                    DrawSelectedElement(selected, true);
                }

                UIControlSelected?.Invoke(this, new UIControlSelectedEventArgs(uiCtrls));
            }
            else if (_selectedElements.Count == 1)
            {
                SelectedElement selected = _selectedElements[0];
                DrawSelectedElement(selected, false);

                UIControlSelected?.Invoke(this, new UIControlSelectedEventArgs(selected.UIControl));
            }
        }

        /// <summary>
        /// Draw border and drag handles around a selected element
        /// </summary>
        private void DrawSelectedElement(SelectedElement selected, bool multiSelect)
        {
            UIControl uiCtrl = selected.Element.Tag as UIControl;
            Debug.Assert(uiCtrl != null, "Incorrect SelectedElement handling");

            int z = MaxZIndex;
            Rect elementRect = GetElementSize(selected.Element);

            // Draw selected border
            selected.Border = new Border
            {
                Opacity = 0.75,
                BorderBrush = multiSelect ? Brushes.Blue : Brushes.Red,
                BorderThickness = new Thickness(2),
                Focusable = false,
            };

            if (uiCtrl.Type != UIControlType.Bevel)
            {
                SetZIndex(selected.Element, z + 1);
                SetZIndex(selected.Border, z + 2);
            }

            UIRenderer.DrawToCanvas(this, selected.Border, elementRect);

            // Draw drag handle
            List<ResizeClickPosition> clickPosList = new List<ResizeClickPosition>(9)
            {
                // Only visible if ElementRect.Height is longer than 20px
                ResizeClickPosition.Left,
                ResizeClickPosition.Right,
                // Only visible if ElementRect.Width is longer than 20px
                ResizeClickPosition.Top,
                ResizeClickPosition.Bottom,
                // Always visible
                ResizeClickPosition.LeftTop,
                ResizeClickPosition.RightTop,
                ResizeClickPosition.LeftBottom,
                ResizeClickPosition.RightBottom,
            };

            foreach (ResizeClickPosition clickPos in clickPosList)
            {
                Border dragHandle = new Border
                {
                    BorderThickness = new Thickness(1),
                    Tag = new DragHandleTag(clickPos, selected.Element, elementRect),
                };
                SetDragHandleVisibility(dragHandle, clickPos, elementRect);
                SetZIndex(dragHandle, z + 3);

                selected.DragHandles.Add(dragHandle);

                Point p = CalcDragHandlePosition(clickPos, elementRect);
                Rect r = new Rect(p.X, p.Y, DragHandleLength, DragHandleLength);
                UIRenderer.DrawToCanvas(this, dragHandle, r);
            }
        }

        /// <summary>
        /// Draw border and drag handles around selected element, from outside
        /// </summary>
        public void DrawSelectedElement(UIControl uiCtrl)
        {
            if (uiCtrl == null)
                return;

            foreach (FrameworkElement child in Children)
            {
                if (!(child.Tag is UIControl ctrl))
                    continue;
                if (!ctrl.Key.Equals(uiCtrl.Key, StringComparison.Ordinal))
                    continue;

                _selectedElements.Clear();
                _selectedElements.Add(new SelectedElement(child));
                break;
            }

            DrawSelectedElements();
        }

        /// <summary>
        /// Draw border and drag handles around selected element, from outside
        /// </summary>
        public void DrawSelectedElements(List<UIControl> uiCtrls)
        {
            if (uiCtrls == null)
                return;

            _selectedElements.Clear();
            foreach (UIControl uiCtrl in uiCtrls)
            {
                foreach (FrameworkElement child in Children)
                {
                    if (!(child.Tag is UIControl ctrl))
                        continue;
                    if (!ctrl.Key.Equals(uiCtrl.Key, StringComparison.Ordinal))
                        continue;

                    _selectedElements.Add(new SelectedElement(child));
                    break;
                }
            }

            DrawSelectedElements();
        }

        private static void MoveSelectedElement(SelectedElement selected, Rect newRect)
        {
            // Assertion
            Debug.Assert(selected.Element != null, "Incorrect SelectedElement handling");
            Debug.Assert(selected.Border != null, "Incorrect SelectedElement handling");

            // Move element and border
            Point newPos = new Point(newRect.X, newRect.Y);
            SetElementPosition(selected.Element, newPos);
            SetElementPosition(selected.Border, newPos);

            // Move drag handles
            foreach (Border dragHandle in selected.DragHandles)
            {
                Debug.Assert(dragHandle.Tag.GetType() == typeof(DragHandleTag), "Incorrect SelectedElement handling");
                DragHandleTag info = (DragHandleTag)dragHandle.Tag;

                SetDragHandleVisibility(dragHandle, info.ClickPos, newRect);
                Point p = CalcDragHandlePosition(info.ClickPos, newRect);
                SetElementPosition(dragHandle, p);
            }
        }

        private static void ResizeSelectedElements(SelectedElement selected, Rect newRect)
        {
            // Assertion
            Debug.Assert(selected.Element != null, "Incorrect SelectedElement handling");
            Debug.Assert(selected.Border != null, "Incorrect SelectedElement handling");

            // Resize element and border
            SetElementSize(selected.Element, newRect);
            SetElementSize(selected.Border, newRect);

            // Resize drag handles
            foreach (Border dragHandle in selected.DragHandles)
            {
                Debug.Assert(dragHandle.Tag.GetType() == typeof(DragHandleTag));
                DragHandleTag info = (DragHandleTag)dragHandle.Tag;

                SetDragHandleVisibility(dragHandle, info.ClickPos, newRect);
                Point p = CalcDragHandlePosition(info.ClickPos, newRect);
                SetElementPosition(dragHandle, p);
            }
        }
        #endregion

        #region (public) Mouse Cursor
        public static void SetMouseCursor(ResizeClickPosition clickPos = ResizeClickPosition.Inside)
        {
            Cursor newCursor = null;
            switch (clickPos)
            {
                case ResizeClickPosition.Left:
                case ResizeClickPosition.Right:
                    newCursor = Cursors.SizeWE;
                    break;
                case ResizeClickPosition.Top:
                case ResizeClickPosition.Bottom:
                    newCursor = Cursors.SizeNS;
                    break;
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.RightBottom:
                    newCursor = Cursors.SizeNWSE;
                    break;
                case ResizeClickPosition.RightTop:
                case ResizeClickPosition.LeftBottom:
                    newCursor = Cursors.SizeNESW;
                    break;
                case ResizeClickPosition.Inside:
                    newCursor = Cursors.SizeAll;
                    break;
            }

            if (Mouse.OverrideCursor != newCursor)
                Mouse.OverrideCursor = newCursor;
        }

        public static void ResetMouseCursor()
        {
            if (Mouse.OverrideCursor != null)
                Mouse.OverrideCursor = null;
        }
        #endregion

        #region (private) DragHandle Utility
        private static Point CalcDragHandlePosition(ResizeClickPosition clickPos, Rect elementRect)
        {
            double x = elementRect.X;
            double y = elementRect.Y;
            switch (clickPos)
            {
                case ResizeClickPosition.Left:
                    x -= DragHandleLength;
                    y -= DragHandleLength / 2.0;
                    y += elementRect.Height / 2;
                    break;
                case ResizeClickPosition.Right:
                    x += elementRect.Width;
                    y -= DragHandleLength / 2.0;
                    y += elementRect.Height / 2;
                    break;
                case ResizeClickPosition.Top:
                    x -= DragHandleLength / 2.0;
                    x += elementRect.Width / 2;
                    y -= DragHandleLength;
                    break;
                case ResizeClickPosition.Bottom:
                    x -= DragHandleLength / 2.0;
                    x += elementRect.Width / 2;
                    y += elementRect.Height;
                    break;
                case ResizeClickPosition.LeftTop:
                    x -= DragHandleLength;
                    y -= DragHandleLength;
                    break;
                case ResizeClickPosition.LeftBottom:
                    x -= DragHandleLength;
                    y += elementRect.Height;
                    break;
                case ResizeClickPosition.RightTop:
                    x += elementRect.Width;
                    y -= DragHandleLength;
                    break;
                case ResizeClickPosition.RightBottom:
                    x += elementRect.Width;
                    y += elementRect.Height;
                    break;
                default:
                    throw new ArgumentException(nameof(clickPos));
            }

            return new Point(x, y);
        }

        private static void SetDragHandleVisibility(Border dragHandle, ResizeClickPosition clickPos, Rect elementRect)
        {
            bool visible;
            switch (clickPos)
            {
                // Only visible if ElementRect.Height is longer than 20px
                case ResizeClickPosition.Left:
                case ResizeClickPosition.Right:
                    visible = DragHandleShowThreshold < elementRect.Height;
                    break;
                // Only visible if ElementRect.Width is longer than 20px
                case ResizeClickPosition.Top:
                case ResizeClickPosition.Bottom:
                    visible = DragHandleShowThreshold < elementRect.Width;
                    break;
                // Always visible
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.LeftBottom:
                case ResizeClickPosition.RightTop:
                case ResizeClickPosition.RightBottom:
                    visible = true;
                    break;
                default:
                    throw new ArgumentException(nameof(clickPos));
            }

            if (visible)
            {
                dragHandle.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                dragHandle.BorderBrush = Brushes.Black;
                dragHandle.Focusable = true;
            }
            else
            {
                dragHandle.Background = Brushes.Transparent;
                dragHandle.BorderBrush = Brushes.Transparent;
                dragHandle.Focusable = false;
            }
        }
        #endregion

        #region (private) Move/Resize Utility
        private static Point CalcNewPosition(Point cursorStart, Point cursorNow, Point elementStart)
        {
            double x = elementStart.X + cursorNow.X - cursorStart.X;
            double y = elementStart.Y + cursorNow.Y - cursorStart.Y;

            // Do not use Width and Height here, or canvas cannot be expanded
            if (x < 0)
                x = 0;
            else if (CanvasWidthHeightLimit < x)
                x = CanvasWidthHeightLimit;
            if (y < 0)
                y = 0;
            else if (CanvasWidthHeightLimit < y)
                y = CanvasWidthHeightLimit;

            return new Point(x, y);
        }

        private static Rect CalcNewSize(Point cursorStart, Point cursorNow, Rect elementRect, ResizeClickPosition clickPos)
        {
            const int MinLineLen = 16;

            // Do not touch Width and Height if border was not clicked
            switch (clickPos)
            {
                case ResizeClickPosition.Inside:
                case ResizeClickPosition.Outside:
                    return elementRect;
            }

            // Get delta of X and Y
            double xDelta = cursorNow.X - cursorStart.X;
            double yDelta = cursorNow.Y - cursorStart.Y;

            // Prepare variables
            double x = elementRect.X;
            double y = elementRect.Y;
            double width = elementRect.Width;
            double height = elementRect.Height;

            // X Direction
            switch (clickPos)
            {
                case ResizeClickPosition.Left:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.LeftBottom:
                    if (Math.Abs(xDelta) < double.Epsilon)
                        break;
                    if (0 < xDelta)
                    { // L [->    ] R, delta is positive
                        if (xDelta + MinLineLen < width)
                        {
                            x += xDelta;
                            width -= xDelta;
                        }
                        else
                        { // Guard
                            x += width - MinLineLen;
                            width = MinLineLen;
                        }
                    }
                    else
                    { // L <-[    ] R, delta is negative
                        x += xDelta;
                        width -= xDelta;
                    }
                    break;
                case ResizeClickPosition.Right:
                case ResizeClickPosition.RightTop:
                case ResizeClickPosition.RightBottom:
                    if (Math.Abs(xDelta) < double.Epsilon)
                        break;
                    if (0 < xDelta)
                    { // L [    ]-> R, delta is positive
                        width += xDelta;
                    }
                    else
                    { // L [    <-] R, delta is negative
                        if (MinLineLen < width + xDelta) // Guard
                            width += xDelta;
                        else
                            width = MinLineLen;
                    }
                    break;
            }

            // Y Direction
            switch (clickPos)
            {
                case ResizeClickPosition.Top:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.RightTop:
                    if (Math.Abs(yDelta) < double.Epsilon)
                        break;
                    if (0 < yDelta)
                    { // T [->    ] B, delta is positive
                        if (yDelta + MinLineLen < height)
                        {
                            y += yDelta;
                            height -= yDelta;
                        }
                        else
                        { // Guard
                            y += height - MinLineLen;
                            height = MinLineLen;
                        }
                    }
                    else
                    { // T <-[    ] B, delta is negative
                        y += yDelta;
                        height -= yDelta;
                    }
                    break;
                case ResizeClickPosition.Bottom:
                case ResizeClickPosition.LeftBottom:
                case ResizeClickPosition.RightBottom:
                    if (Math.Abs(yDelta) < double.Epsilon)
                        break;
                    if (0 < yDelta)
                    { // T [    ]-> B, delta is positive
                        height += yDelta;
                    }
                    else
                    { // T [    <-] B, delta is negative
                        if (MinLineLen < height + yDelta) // Guard
                            height += yDelta;
                        else
                            height = MinLineLen;
                    }
                    break;
            }

            // Check if X and Width is correct
            switch (clickPos)
            {
                case ResizeClickPosition.Left:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.LeftBottom:
                    if (x < 0)
                    {
                        width += x;
                        x = 0;
                    }
                    break;
                case ResizeClickPosition.Right:
                case ResizeClickPosition.RightTop:
                case ResizeClickPosition.RightBottom:
                    if (CanvasWidthHeightLimit + ElementWidthHeightLimit < x + width)
                        width = CanvasWidthHeightLimit + ElementWidthHeightLimit - x;
                    break;
            }

            // Check if Y and Height is correct
            switch (clickPos)
            {
                case ResizeClickPosition.Top:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.RightTop:
                    if (y < 0)
                    {
                        height += y;
                        y = 0;
                    }
                    break;
                case ResizeClickPosition.Bottom:
                case ResizeClickPosition.LeftBottom:
                case ResizeClickPosition.RightBottom:
                    if (CanvasWidthHeightLimit + ElementWidthHeightLimit < y + height)
                        height = CanvasWidthHeightLimit + ElementWidthHeightLimit - y;
                    break;
            }

            return new Rect(x, y, width, height);
        }

        private static void SetElementPosition(FrameworkElement element, Point p)
        {
            SetLeft(element, p.X);
            SetTop(element, p.Y);
        }

        private static void SetElementSize(FrameworkElement element, Rect rect)
        {
            SetLeft(element, rect.X);
            SetTop(element, rect.Y);
            element.Width = rect.Width;
            element.Height = rect.Height;
        }

        private static Rect GetElementSize(FrameworkElement element)
        {
            return new Rect
            {
                X = GetLeft(element),
                Y = GetTop(element),
                Width = element.Width,
                Height = element.Height,
            };
        }
        #endregion

        #region (private) FrameworkElement Utility
        public FrameworkElement FindRootFrameworkElement(object obj)
        {
            if (obj is DependencyObject dObj)
                return FindRootFrameworkElement(dObj);
            else
                return null;
        }

        public FrameworkElement FindRootFrameworkElement(DependencyObject dObj)
        {
            while (dObj != null)
            {
                if (dObj is FrameworkElement element && Children.Contains(element))
                    return element;

                if (dObj is Visual || dObj is Visual3D)
                    dObj = VisualTreeHelper.GetParent(dObj);
                else
                    dObj = LogicalTreeHelper.GetParent(dObj);
            }
            return null;
        }
        #endregion

        #region class DragHandleTag
        protected class DragHandleTag
        {
            public ResizeClickPosition ClickPos;
            public FrameworkElement Parent;
            public Rect ParentRect;

            public DragHandleTag(ResizeClickPosition clickPos, FrameworkElement parent, Rect parentRect)
            {
                ClickPos = clickPos;
                Parent = parent;
                ParentRect = parentRect;
            }
        }
        #endregion

        #region class SelectedElements
        protected class SelectedElement
        {
            public FrameworkElement Element;
            public UIControl UIControl => Element.Tag as UIControl;
            public Rect ElementInitialRect;
            public Border Border;
            public readonly List<Border> DragHandles = new List<Border>();

            public SelectedElement(FrameworkElement element)
            {
                Debug.Assert(element.Tag.GetType() == typeof(UIControl), "Incorrect Element.Tag");
                Element = element;
                ElementInitialRect = DragCanvas.GetElementSize(element);
            }
        }
        #endregion
    }
}
