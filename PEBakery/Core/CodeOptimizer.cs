﻿using PEBakery.WPF;
using System;
using System.IO;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.Exceptions;
using System.Diagnostics;

namespace PEBakery.Core
{
    /* 
     * Basic of Code Optimization
     * 
     * 같은 파일에 대해 File IO를 하는 명령어가 연달아 있을 경우
     * -> 한번에 묶어서 처리하면 IO 오버헤드를 크게 줄일 수 있다.
     * 
     * TXTAddLine, IniRead, IniWrite, Visible 등이 해당.
     * 
     * Visible의 경우, 배치 처리할 경우 DrawPlugin의 호출 횟수도 줄일 수 있다.
     * 
     */

    public static class CodeOptimizer
    {
        private static readonly List<CodeType> toOptimize = new List<CodeType>()
        {
            CodeType.TXTAddLine,
            CodeType.TXTDelLine,
            CodeType.INIRead,
            CodeType.INIWrite,
            CodeType.INIDelete,
            CodeType.INIAddSection,
            CodeType.INIDeleteSection,
            CodeType.INIWriteTextLine,
            CodeType.Visible,
        };

        #region OptimizeCommands
        public static List<CodeCommand> OptimizeCommands(List<CodeCommand> cmdList)
        {
            List<CodeCommand> optimized = new List<CodeCommand>();
            
            Dictionary<CodeType, List<CodeCommand>> opDict = new Dictionary<CodeType, List<CodeCommand>>();
            foreach (CodeType type in toOptimize)
                opDict[type] = new List<CodeCommand>();

            CodeType state = CodeType.None;
            for (int i = 0; i < cmdList.Count; i++)
            {
                CodeCommand cmd = cmdList[i];
                
                switch (state)
                {
                    #region Default
                    case CodeType.None:
                        switch (cmd.Type)
                        {
                            case CodeType.TXTAddLine:
                                state = CodeType.TXTAddLine;
                                opDict[CodeType.TXTAddLine].Add(cmd);
                                break;
                            case CodeType.TXTDelLine:
                                state = CodeType.TXTDelLine;
                                opDict[CodeType.TXTDelLine].Add(cmd);
                                break;
                            case CodeType.INIWrite:
                                state = CodeType.INIWrite;
                                opDict[CodeType.INIWrite].Add(cmd);
                                break;
                            case CodeType.INIRead:
                                state = CodeType.INIRead;
                                opDict[CodeType.INIRead].Add(cmd);
                                break;
                            case CodeType.INIWriteTextLine:
                                state = CodeType.INIWriteTextLine;
                                opDict[CodeType.INIWriteTextLine].Add(cmd);
                                break;
                            case CodeType.Visible:
                                state = CodeType.Visible;
                                opDict[CodeType.Visible].Add(cmd);
                                break;
                            default:
                                optimized.Add(cmd);
                                break;
                        }
                        break;
                    #endregion
                    #region TXTAddLine
                    case CodeType.TXTAddLine:
                        Debug.Assert(opDict[state][0].Info.GetType() == typeof(CodeInfo_TXTAddLine));
                        switch (cmd.Type)
                        {
                            case CodeType.TXTAddLine:
                                {
                                    Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTAddLine));
                                    CodeInfo_TXTAddLine firstInfo = (opDict[state][0].Info as CodeInfo_TXTAddLine);
                                    if (cmd.Info is CodeInfo_TXTAddLine info &&
                                        info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase) &&
                                        info.Mode.Equals(firstInfo.Mode, StringComparison.OrdinalIgnoreCase))
                                        opDict[state].Add(cmd);
                                    else
                                        goto default;
                                }
                                break;
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[state].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[state][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeTXTAddLine(opDict[state]);
                                    optimized.Add(opCmd);
                                }
                                opDict[state].Clear();
                                optimized.Add(cmd);
                                state = CodeType.None;
                                break;
                        }
                        break;
                    #endregion
                    #region TXTDelLine
                    case CodeType.TXTDelLine:
                        Debug.Assert(opDict[state][0].Info.GetType() == typeof(CodeInfo_TXTDelLine));
                        switch (cmd.Type)
                        {
                            case CodeType.TXTDelLine:
                                {
                                    Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelLine));

                                    CodeInfo_TXTDelLine firstInfo = (opDict[state][0].Info as CodeInfo_TXTDelLine);
                                    if (cmd.Info is CodeInfo_TXTDelLine info &&
                                        info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                        opDict[state].Add(cmd);
                                    else
                                        goto default;
                                }
                                break;
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[state].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[state][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeTXTAddLine(opDict[state]);
                                    optimized.Add(opCmd);
                                }
                                opDict[state].Clear();
                                optimized.Add(cmd);
                                state = CodeType.None;
                                break;
                        }
                        break;
                    #endregion
                    #region INIRead
                    case CodeType.INIRead:
                        Debug.Assert(opDict[state][0].Info.GetType() == typeof(CodeInfo_INIRead));
                        switch (cmd.Type)
                        {
                            case CodeType.INIRead:
                                {
                                    Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIRead));
                                    CodeInfo_INIRead firstInfo = (opDict[state][0].Info as CodeInfo_INIRead);
                                    if (cmd.Info is CodeInfo_INIRead info &&
                                        info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                        opDict[state].Add(cmd);
                                    else
                                        goto default;
                                }
                                break;
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[state].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[state][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIRead(opDict[state]);
                                    optimized.Add(opCmd);
                                }
                                opDict[state].Clear();
                                optimized.Add(cmd);
                                state = CodeType.None;
                                break;
                        }
                        break;
                    #endregion
                    #region INIWrite
                    case CodeType.INIWrite:
                        Debug.Assert(opDict[state][0].Info.GetType() == typeof(CodeInfo_INIWrite));
                        switch (cmd.Type)
                        {
                            case CodeType.INIWrite:
                                {
                                    Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIWrite));
                                    CodeInfo_INIWrite firstInfo = (opDict[state][0].Info as CodeInfo_INIWrite);
                                    if (cmd.Info is CodeInfo_INIWrite info &&
                                        info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                        opDict[state].Add(cmd);
                                    else
                                        goto default;
                                }
                                break;
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[state].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[state][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIWrite(opDict[state]);
                                    optimized.Add(opCmd);
                                }
                                opDict[state].Clear();
                                optimized.Add(cmd);
                                state = CodeType.None;
                                break;
                        }
                        break;
                    #endregion
                    #region INIDelete
                    case CodeType.INIDelete:
                        Debug.Assert(opDict[state][0].Info.GetType() == typeof(CodeInfo_INIDelete));
                        switch (cmd.Type)
                        {
                            case CodeType.INIDelete:
                                {
                                    Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIDelete));
                                    CodeInfo_INIDelete firstInfo = (opDict[state][0].Info as CodeInfo_INIDelete);
                                    if (cmd.Info is CodeInfo_INIDelete info &&
                                        info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                        opDict[state].Add(cmd);
                                    else
                                        goto default;
                                }
                                break;
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[state].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[state][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIDelete(opDict[state]);
                                    optimized.Add(opCmd);
                                }
                                opDict[state].Clear();
                                optimized.Add(cmd);
                                state = CodeType.None;
                                break;
                        }
                        break;
                    #endregion
                    #region INIAddSection
                    case CodeType.INIAddSection:
                        Debug.Assert(opDict[state][0].Info.GetType() == typeof(CodeInfo_INIAddSection));
                        switch (cmd.Type)
                        {
                            case CodeType.INIAddSection:
                                {
                                    Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIAddSection));
                                    CodeInfo_INIAddSection firstInfo = (opDict[state][0].Info as CodeInfo_INIAddSection);
                                    if (cmd.Info is CodeInfo_INIAddSection info &&
                                        info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                        opDict[state].Add(cmd);
                                    else
                                        goto default;
                                }
                                break;
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[state].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[state][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIAddSection(opDict[state]);
                                    optimized.Add(opCmd);
                                }
                                opDict[state].Clear();
                                optimized.Add(cmd);
                                state = CodeType.None;
                                break;
                        }
                        break;
                    #endregion
                    #region INIDeleteSection
                    case CodeType.INIDeleteSection:
                        Debug.Assert(opDict[state][0].Info.GetType() == typeof(CodeInfo_INIDeleteSection));
                        switch (cmd.Type)
                        {
                            case CodeType.INIDeleteSection:
                                {
                                    Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIDeleteSection));
                                    CodeInfo_INIDeleteSection firstInfo = (opDict[state][0].Info as CodeInfo_INIDeleteSection);
                                    if (cmd.Info is CodeInfo_INIDeleteSection info &&
                                        info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                        opDict[state].Add(cmd);
                                    else
                                        goto default;
                                }
                                break;
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[state].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[state][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIDeleteSection(opDict[state]);
                                    optimized.Add(opCmd);
                                }
                                opDict[state].Clear();
                                optimized.Add(cmd);
                                state = CodeType.None;
                                break;
                        }
                        break;
                    #endregion
                    #region INIWriteTextLine
                    case CodeType.INIWriteTextLine:
                        Debug.Assert(opDict[state][0].Info.GetType() == typeof(CodeInfo_INIWriteTextLine));
                        switch (cmd.Type)
                        {
                            case CodeType.INIWriteTextLine:
                                {
                                    Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIWriteTextLine));
                                    CodeInfo_INIWriteTextLine firstInfo = (opDict[state][0].Info as CodeInfo_INIWriteTextLine);
                                    if (cmd.Info is CodeInfo_INIWriteTextLine info &&
                                        info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase) &&
                                        info.Append == firstInfo.Append)
                                        opDict[state].Add(cmd);
                                    else
                                        goto default;
                                }
                                break;
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[state].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[state][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIWriteTextLine(opDict[state]);
                                    optimized.Add(opCmd);
                                }
                                opDict[state].Clear();
                                optimized.Add(cmd);
                                state = CodeType.None;
                                break;
                        }
                        break;
                    #endregion
                    #region Visible
                    case CodeType.Visible:
                        switch (cmd.Type)
                        {
                            case CodeType.Visible:
                                opDict[state].Add(cmd);
                                break;
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[state].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[state][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeVisible(opDict[state]);
                                    optimized.Add(opCmd);
                                }
                                opDict[state].Clear();
                                optimized.Add(cmd);
                                state = CodeType.None;
                                break;
                        }
                        break;
                    #endregion
                    #region Error
                    default:
                        Debug.Assert(false);
                        break;
                        #endregion
                }
            }

            #region Finalize
            foreach (var kv in opDict)
            {
                if (1 < kv.Value.Count)
                {
                    CodeCommand opCmd = null;
                    switch (kv.Key)
                    {
                        case CodeType.TXTAddLine:
                            opCmd = OptimizeTXTAddLine(kv.Value);
                            break;
                        case CodeType.TXTDelLine:
                            opCmd = OptimizeTXTDelLine(kv.Value);
                            break;
                        case CodeType.INIWrite:
                            opCmd = OptimizeINIWrite(kv.Value);
                            break;
                        case CodeType.INIRead:
                            opCmd = OptimizeINIRead(kv.Value);
                            break;
                        case CodeType.INIDelete:
                            opCmd = OptimizeINIDelete(kv.Value);
                            break;
                        case CodeType.INIAddSection:
                            opCmd = OptimizeINIAddSection(kv.Value);
                            break;
                        case CodeType.INIDeleteSection:
                            opCmd = OptimizeINIDeleteSection(kv.Value);
                            break;
                        case CodeType.INIWriteTextLine:
                            opCmd = OptimizeINIWriteTextLine(kv.Value);
                            break;
                        case CodeType.Visible:
                            opCmd = OptimizeVisible(kv.Value);
                            break;
                    }
                    Debug.Assert(opCmd != null); // Logic Error
                    optimized.Add(opCmd);
                }
                else if (1 == kv.Value.Count)
                {
                    CodeCommand oneCmd = kv.Value[0];
                    optimized.Add(oneCmd);
                }
            }
            #endregion

            return optimized;
        }
        #endregion

        #region Optimize Individual
        // TODO: Is there any 'generic' way?
        private static CodeCommand OptimizeTXTAddLine(List<CodeCommand> cmdList)
        {
            List<CodeInfo_TXTAddLine> infoList = new List<CodeInfo_TXTAddLine>();
            foreach (CodeCommand cmd in cmdList)
            {
                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTAddLine));
                CodeInfo_TXTAddLine info = cmd.Info as CodeInfo_TXTAddLine;

                infoList.Add(info);
            }

            string rawCode = $"Optimized TXTAddLine at [{cmdList[0].Addr.Section.SectionName}]";
            return new CodeCommand(rawCode, cmdList[0].Addr, CodeType.TXTAddLineOp, new CodeInfo_TXTAddLineOp(infoList));
        }

        private static CodeCommand OptimizeTXTDelLine(List<CodeCommand> cmdList)
        {
            List<CodeInfo_TXTDelLine> infoList = new List<CodeInfo_TXTDelLine>();
            foreach (CodeCommand cmd in cmdList)
            {
                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelLine));
                CodeInfo_TXTDelLine info = cmd.Info as CodeInfo_TXTDelLine;

                infoList.Add(info);
            }

            // string rawCode = $"Optimized TXTDelLine at [{cmdList[0].Addr.Section.SectionName}]";
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cmdList.Count; i++)
            {
                b.Append(cmdList[i].RawCode);
                if (i + 1 < cmdList.Count)
                    b.AppendLine();
            }
            return new CodeCommand(b.ToString(), cmdList[0].Addr, CodeType.TXTDelLineOp, new CodeInfo_TXTDelLineOp(infoList));
        }

        private static CodeCommand OptimizeINIRead(List<CodeCommand> cmdList)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cmdList.Count; i++)
                b.AppendLine(cmdList[0].RawCode);
            return new CodeCommand(b.ToString(), cmdList[0].Addr, CodeType.INIReadOp, new CodeInfo_INIReadOp(cmdList));
        }

        private static CodeCommand OptimizeINIWrite(List<CodeCommand> cmdList)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cmdList.Count; i++)
                b.AppendLine(cmdList[0].RawCode);
            return new CodeCommand(b.ToString(), cmdList[0].Addr, CodeType.INIWriteOp, new CodeInfo_INIWriteOp(cmdList));
        }

        private static CodeCommand OptimizeINIDelete(List<CodeCommand> cmdList)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cmdList.Count; i++)
                b.AppendLine(cmdList[0].RawCode);
            return new CodeCommand(b.ToString(), cmdList[0].Addr, CodeType.INIWriteTextLineOp, new CodeInfo_INIDeleteOp(cmdList));
        }

        private static CodeCommand OptimizeINIAddSection(List<CodeCommand> cmdList)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cmdList.Count; i++)
                b.AppendLine(cmdList[0].RawCode);
            return new CodeCommand(b.ToString(), cmdList[0].Addr, CodeType.INIAddSectionOp, new CodeInfo_INIAddSectionOp(cmdList));
        }

        private static CodeCommand OptimizeINIDeleteSection(List<CodeCommand> cmdList)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cmdList.Count; i++)
                b.AppendLine(cmdList[0].RawCode);
            return new CodeCommand(b.ToString(), cmdList[0].Addr, CodeType.INIDeleteSectionOp, new CodeInfo_INIDeleteSectionOp(cmdList));
        }

        private static CodeCommand OptimizeINIWriteTextLine(List<CodeCommand> cmdList)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cmdList.Count; i++)
                b.AppendLine(cmdList[0].RawCode);
            return new CodeCommand(b.ToString(), cmdList[0].Addr, CodeType.INIWriteTextLineOp, new CodeInfo_INIWriteTextLineOp(cmdList));
        }

        private static CodeCommand OptimizeVisible(List<CodeCommand> cmdList)
        {
            List<CodeInfo_Visible> infoList = new List<CodeInfo_Visible>();
            foreach (CodeCommand cmd in cmdList)
            {
                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Visible));
                CodeInfo_Visible info = cmd.Info as CodeInfo_Visible;

                infoList.Add(info);
            }

            string rawCode = $"Optimized Visible at [{cmdList[0].Addr.Section.SectionName}]";
            return new CodeCommand(rawCode, cmdList[0].Addr, CodeType.VisibleOp, new CodeInfo_VisibleOp(infoList));
        }
        #endregion
    }
}
