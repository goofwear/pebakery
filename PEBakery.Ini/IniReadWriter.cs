﻿/*
    Copyright (C) 2016-2019 Hajin Jang
 
    MIT License

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

using PEBakery.Helper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
// ReSharper disable UnusedMember.Global

namespace PEBakery.Ini
{
    #region IniKey
    public struct IniKey
    {
        public string Section;
        public string Key;
        public string Value; // In GetKeys, this record is not used, set to null

        public IniKey(string section)
        {
            Section = section;
            Key = null;
            Value = null;
        }
        public IniKey(string section, string key)
        {
            Section = section;
            Key = key;
            Value = null;
        }
        public IniKey(string section, string key, string value)
        {
            Section = section;
            Key = key;
            Value = value;
        }
    }
    #endregion

    #region IniReadWriter
    public static class IniReadWriter
    {
        #region Lock
        private static readonly ConcurrentDictionary<string, ReaderWriterLockSlim> LockDict =
            new ConcurrentDictionary<string, ReaderWriterLockSlim>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Must be called when Ini class is not used
        /// </summary>
        public static void ClearLockDict()
        {
            LockDict.Clear();
        }

        private static ReaderWriterLockSlim GetFileLock(string filePath)
        {
            filePath = Path.GetFullPath(filePath);

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(filePath))
            {
                rwLock = LockDict[filePath];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[filePath] = rwLock;
            }

            return rwLock;
        }
        #endregion

        #region ReadKey
        /// <summary>
        /// Get key's value from ini file.
        /// </summary>
        public static string ReadKey(string file, IniKey iniKey)
        {
            IniKey[] iniKeys = InternalReadKeys(file, new IniKey[] { iniKey });
            return iniKeys[0].Value;
        }
        /// <summary>
        /// Get key's value from ini file.
        /// </summary>
        public static string ReadKey(string file, string section, string key)
        {
            IniKey[] iniKeys = InternalReadKeys(file, new IniKey[] { new IniKey(section, key) });
            return iniKeys[0].Value;
        }
        /// <summary>
        /// Get key's value from ini file.
        /// </summary>
        public static IniKey[] ReadKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalReadKeys(file, iniKeys.ToArray());
        }
        private static IniKey[] InternalReadKeys(string file, IniKey[] iniKeys)
        {
            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                List<int> processedKeyIdxs = new List<int>(iniKeys.Length);

                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader reader = new StreamReader(file, encoding, true))
                {
                    string rawLine;
                    bool inTargetSection = false;
                    string currentSection = null;

                    while ((rawLine = reader.ReadLine()) != null)
                    { // Read text line by line
                        if (processedKeyIdxs.Count == iniKeys.Length) // Work Done
                            break;

                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                        if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith("//".AsSpan(), StringComparison.Ordinal)) // Ignore comment
                            continue;

                        if (inTargetSection)
                        {
                            int idx = line.IndexOf('=');
                            if (idx != -1 && idx != 0) // there is key, and key name is not empty
                            {
                                ReadOnlySpan<char> keyName = line.Slice(0, idx).Trim();
                                for (int i = 0; i < iniKeys.Length; i++)
                                {
                                    if (processedKeyIdxs.Contains(i))
                                        continue;

                                    // Only if <section, key> is same, copy value;
                                    IniKey iniKey = iniKeys[i];
                                    if (currentSection.Equals(iniKey.Section, StringComparison.OrdinalIgnoreCase) &&
                                        keyName.Equals(iniKey.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        iniKey.Value = line.Slice(idx + 1).Trim().ToString();
                                        iniKeys[i] = iniKey;
                                        processedKeyIdxs.Add(i);
                                    }
                                }
                            }
                            else
                            {
                                // search if current section reached its end
                                if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                                    line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                                {
                                    // Only sections contained in iniKeys will be targeted
                                    inTargetSection = false;
                                    currentSection = null;
                                    ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                                    for (int i = 0; i < iniKeys.Length; i++)
                                    {
                                        if (processedKeyIdxs.Contains(i))
                                            continue;

                                        if (foundSection.Equals(iniKeys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            inTargetSection = true;
                                            currentSection = foundSection.ToString();
                                            break; // for shorter O(n)
                                        }
                                    }
                                }
                            }
                        }
                        else
                        { // not in section
                          // Check if encountered section head Ex) [Process]
                            if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                                line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                            {
                                // Only sections contained in iniKeys will be targeted
                                ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                                for (int i = 0; i < iniKeys.Length; i++)
                                {
                                    if (processedKeyIdxs.Contains(i))
                                        continue;

                                    if (foundSection.Equals(iniKeys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        inTargetSection = true;
                                        currentSection = foundSection.ToString();
                                        break; // for shorter O(n)
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                rwLock.ExitReadLock();
            }

            return iniKeys;
        }
        #endregion

        #region WriteKey
        public static bool WriteKey(string file, string section, string key, string value)
        {
            return InternalWriteKeys(file, new List<IniKey> { new IniKey(section, key, value) });
        }

        public static bool WriteKey(string file, IniKey iniKey)
        {
            return InternalWriteKeys(file, new List<IniKey> { iniKey });
        }

        public static bool WriteKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalWriteKeys(file, iniKeys.ToList());
        }

        private static bool InternalWriteKeys(string file, List<IniKey> inputKeys)
        {
            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            #region FinalizeFile
            void FinalizeFile(StreamWriter w, ReadOnlySpan<char> lastLine, bool firstEmptyLine)
            {
                bool firstSection = true;
                if (0 < inputKeys.Count)
                {
                    // Call ToArray() to make a copy of inputKeys
                    string[] unprocessedSections = inputKeys
                        .Select(x => x.Section)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    foreach (string section in unprocessedSections)
                    {
                        IniKey[] secKeys = inputKeys
                            .Where(x => x.Section.Equals(section))
                            .ToArray();

                        if ((lastLine == null || lastLine.Length != 0) && (!firstSection || firstEmptyLine))
                            w.WriteLine();
                        w.WriteLine($"[{section}]");

                        foreach (var secKey in secKeys)
                        {
                            w.WriteLine($"{secKey.Key}={secKey.Value}");
                            inputKeys.RemoveAll(x =>
                                x.Section.Equals(section, StringComparison.OrdinalIgnoreCase) &&
                                x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                        }

                        firstSection = false;
                    }
                }
            }
            #endregion

            rwLock.EnterWriteLock();
            try
            {
                // If file does not exist just create new file and insert keys.
                if (!File.Exists(file))
                {
                    using (StreamWriter w = new StreamWriter(file, false, Encoding.UTF8))
                    {
                        FinalizeFile(w, null, false);
                    }
                    return true;
                }

                // Append IniKey into existing file
                string tempPath = FileHelper.GetTempFile();
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    bool inTargetSection = false;
                    string currentSection = null;
                    List<(string Line, string RawLine)> lineBuffer = new List<(string, string)>(32);

                    #region FinalizeSection
                    void FinalizeSection()
                    {
                        Debug.Assert(currentSection != null);
                        List<IniKey> secKeys = inputKeys
                            .Where(x => x.Section.Equals(currentSection, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        Debug.Assert(0 < secKeys.Count);

                        // Remove tailing empty lines 
                        for (int i = lineBuffer.Count - 1; 0 <= i; i--)
                        {
                            string targetLine = lineBuffer[i].Line;
                            if (targetLine.Length == 0)
                                lineBuffer.RemoveAt(i);
                            else
                                break;
                        }

                        // lineBuffer 내에 있는 key들을 검사해서 덮어 쓸 것들을 확인한다.
                        foreach ((string targetLine, string targetRawLine) in lineBuffer)
                        {
                            int eIdx = targetLine.IndexOf('=');
                            if (eIdx == -1 || // No identifier '='
                                eIdx == 0 || // Key length is 0
                                IsLineComment(targetLine)) // Does line start with { ";", "#", "//" }?
                            {
                                w.WriteLine(targetRawLine);
                            }
                            else
                            {
                                // Overwrite if key=line already exists
                                bool processed = false;
                                ReadOnlySpan<char> targetKey = targetLine.AsSpan(0, eIdx).Trim();

                                // Call ToArray() to make copy of secKeys
                                foreach (IniKey secKey in secKeys.ToArray())
                                {
                                    if (targetKey.Equals(secKey.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        processed = true;
                                        w.WriteLine($"{secKey.Key}={secKey.Value}");

                                        // Remove processed keys
                                        inputKeys.RemoveAll(x =>
                                            x.Section.Equals(currentSection, StringComparison.OrdinalIgnoreCase) &&
                                            x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                                        secKeys.RemoveAll(x =>
                                            x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));

                                        break;
                                    }
                                }

                                if (!processed)
                                    w.WriteLine(targetRawLine);
                            }
                        }

                        lineBuffer.Clear();

                        // Process remaining keys
                        // Call ToArray() to make copy of secKeys
                        foreach (IniKey secKey in secKeys.ToArray())
                        {
                            w.WriteLine($"{secKey.Key}={secKey.Value}");

                            // Remove processed keys
                            inputKeys.RemoveAll(x =>
                                x.Section.Equals(currentSection, StringComparison.OrdinalIgnoreCase) &&
                                x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                            secKeys.RemoveAll(x =>
                                x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                        }
                        w.WriteLine();

                        Debug.Assert(secKeys.Count == 0);
                    }
                    #endregion

                    bool firstLine = true;
                    ReadOnlySpan<char> lastLine = null;
                    while (true)
                    {
                        string rawLine = r.ReadLine();
                        if (rawLine == null)
                        { // Last line!
                            if (inTargetSection)
                                FinalizeSection();

                            // Finalize file
                            FinalizeFile(w, lastLine, !firstLine);
                            break;
                        }

                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Section head like [Process] encountered
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            // Finalize section
                            if (inTargetSection)
                                FinalizeSection();

                            // Write section name
                            w.WriteLine(rawLine);

                            if (inputKeys.Count == 0)
                            { // Job done, no more section parsing
                                inTargetSection = false;
                            }
                            else
                            {
                                string foundSection = line.Slice(1, line.Length - 2).ToString();
                                if (0 < inputKeys.Count(x => foundSection.Equals(x.Section, StringComparison.OrdinalIgnoreCase)))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
                                }
                                else
                                {
                                    inTargetSection = false;
                                }
                            }
                        }
                        else
                        {
                            // Parse section only if corresponding iniKeys exist
                            if (inTargetSection)
                                lineBuffer.Add((line.ToString(), rawLine));
                            else // Pass-through
                                w.WriteLine(rawLine);
                        }

                        firstLine = false;
                        lastLine = line;
                    }
                }

                if (inputKeys.Count == 0)
                { // Success
                    FileHelper.FileReplaceEx(tempPath, file);
                    return true;
                }
                else
                { // Internal Error
                    return false;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region WriteRawLine
        public static bool WriteRawLine(string file, string section, string line, bool append = true)
        {
            return InternalWriteRawLine(file, new List<IniKey> { new IniKey(section, line) }, append);
        }

        public static bool WriteRawLine(string file, IniKey iniKey, bool append = true)
        {
            return InternalWriteRawLine(file, new List<IniKey> { iniKey }, append);
        }

        public static bool WriteRawLines(string file, IEnumerable<IniKey> iniKeys, bool append = true)
        {
            return InternalWriteRawLine(file, iniKeys, append);
        }

        private static bool InternalWriteRawLine(string file, IEnumerable<IniKey> iniKeys, bool append = true)
        {
            List<IniKey> keys = iniKeys.ToList();

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                // If file do not exists or blank, just create new file and insert keys.
                if (!File.Exists(file))
                {
                    using (StreamWriter writer = new StreamWriter(file, false, Encoding.UTF8))
                    {
                        string beforeSection = string.Empty;
                        for (int i = 0; i < keys.Count; i++)
                        {
                            if (beforeSection.Equals(keys[i].Section, StringComparison.OrdinalIgnoreCase) == false)
                            {
                                if (0 < i)
                                    writer.WriteLine();
                                writer.WriteLine($"[{keys[i].Section}]");
                            }

                            // File does not exists, so we don't have to consider "append"
                            writer.WriteLine(keys[i].Key);

                            beforeSection = keys[i].Section;
                        }
                        writer.Close();
                    }
                    return true;
                }

                List<int> processedKeys = new List<int>(keys.Count);
                string tempPath = FileHelper.GetTempFile();
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader reader = new StreamReader(file, encoding, true))
                using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
                {
                    string rawLine;
                    bool inTargetSection = false;
                    ReadOnlySpan<char> currentSection = null;
                    List<string> processedSections = new List<string>(keys.Count);

                    // Is Original File Empty?
                    if (reader.Peek() == -1)
                    {
                        reader.Close();

                        // Write all and exit
                        string beforeSection = string.Empty;
                        for (int i = 0; i < keys.Count; i++)
                        {
                            if (beforeSection.Equals(keys[i].Section, StringComparison.OrdinalIgnoreCase) == false)
                            {
                                if (0 < i)
                                    writer.WriteLine();
                                writer.WriteLine($"[{keys[i].Section}]");
                            }

                            // File is blank, so we don't have to consider "append"
                            writer.WriteLine(keys[i].Key);

                            beforeSection = keys[i].Section;
                        }
                        writer.Close();
                        FileHelper.FileReplaceEx(tempPath, file);
                        return true;
                    }

                    // Main Logic
                    while ((rawLine = reader.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineWritten = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comments. If you wrote all keys successfully, also skip.
                        if (keys.Count == 0 ||
                            line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith("//".AsSpan(), StringComparison.Ordinal))
                        {
                            thisLineWritten = true;
                            writer.WriteLine(rawLine);
                        }
                        else
                        {
                            // Check if encountered section head Ex) [Process]
                            if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                                line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                            {
                                ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                                // Append Mode : Add to last line of section
                                if (append && inTargetSection)
                                { // End of targetSection and start of foundSection
                                    for (int i = 0; i < keys.Count; i++)
                                    {
                                        if (processedKeys.Contains(i))
                                            continue;

                                        // Add to last line of foundSection
                                        if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            processedKeys.Add(i);
                                            writer.WriteLine(keys[i].Key);
                                        }
                                    }
                                }

                                // Start of the section
                                inTargetSection = false;
                                // Only sections contained in iniKeys will be targeted
                                for (int i = 0; i < keys.Count; i++)
                                {
                                    if (processedKeys.Contains(i))
                                        continue;

                                    if (foundSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        inTargetSection = true;
                                        currentSection = foundSection;
                                        processedSections.Add(currentSection.ToString());
                                        break; // for shorter O(n)
                                    }
                                }

                                // Non-Append Mode : Add to first line of section
                                if (!append && inTargetSection)
                                {
                                    for (int i = 0; i < keys.Count; i++)
                                    {
                                        if (processedKeys.Contains(i))
                                            continue;

                                        // Add to last line of foundSection
                                        if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (!thisLineWritten)
                                                writer.WriteLine(rawLine);
                                            thisLineWritten = true;

                                            processedKeys.Add(i);
                                            writer.WriteLine(keys[i].Key);
                                        }
                                    }

                                    inTargetSection = false;
                                }
                            }

                            // Blank line - for Append Mode
                            if (line.Length == 0)
                            {
                                if (append && inTargetSection)
                                {
                                    for (int i = 0; i < keys.Count; i++)
                                    {
                                        if (processedKeys.Contains(i))
                                            continue;

                                        if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                        { // append key to section
                                            processedKeys.Add(i);
                                            writer.WriteLine(keys[i].Key);
                                        }
                                    }
                                }
                                thisLineWritten = true;
                                writer.WriteLine();
                            }
                        }

                        // End of file
                        if (reader.Peek() == -1)
                        {
                            // Append Mode : Add to last line of section
                            if (append && inTargetSection)
                            { // End of targetSection and start of foundSection
                                for (int i = 0; i < keys.Count; i++)
                                {
                                    if (processedKeys.Contains(i))
                                        continue;

                                    // Add to last line of foundSection
                                    if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        processedKeys.Add(i);

                                        if (!thisLineWritten)
                                            writer.WriteLine(rawLine);
                                        thisLineWritten = true;

                                        writer.WriteLine(keys[i].Key);
                                    }
                                }
                            }

                            if (!append)
                            {
                                if (!thisLineWritten)
                                    writer.WriteLine(rawLine);
                                thisLineWritten = true;
                            }

                            // Not in section, so create new section
                            for (int i = 0; i < keys.Count; i++)
                            { // At this time, only not found section remains in iniKeys
                                if (processedKeys.Contains(i))
                                    continue;

                                if (!processedSections.Any(s => s.Equals(keys[i].Section, StringComparison.OrdinalIgnoreCase)))
                                {
                                    if (!thisLineWritten)
                                        writer.WriteLine(rawLine);
                                    thisLineWritten = true;

                                    processedSections.Add(keys[i].Section);
                                    writer.WriteLine($"\r\n[{keys[i].Section}]");
                                }

                                processedKeys.Add(i);

                                writer.WriteLine(keys[i].Key);
                            }
                        }

                        if (!thisLineWritten)
                            writer.WriteLine(rawLine);
                    }
                }

                if (processedKeys.Count == keys.Count)
                {
                    FileHelper.FileReplaceEx(tempPath, file);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region RenameKey
        public static bool RenameKey(string file, string section, string key, string value)
        {
            return InternalRenameKeys(file, new IniKey[] { new IniKey(section, key, value) })[0];
        }

        public static bool RenameKey(string file, IniKey iniKey)
        {
            return InternalRenameKeys(file, new IniKey[] { iniKey })[0];
        }

        public static bool[] RenameKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalRenameKeys(file, iniKeys.ToArray());
        }

        /// <summary>
        /// Internal method for RenameKeys
        /// </summary>
        /// <param name="file"></param>
        /// <param name="iniKeys">Use Value for new names of Key</param>
        /// <returns></returns>
        private static bool[] InternalRenameKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            IniKey[] keys = iniKeys.ToArray();
            bool[] processed = new bool[keys.Length];

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                if (!File.Exists(file))
                    return processed; // All False

                string tempPath = FileHelper.GetTempFile();
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed; // All False
                    }

                    string rawLine;
                    bool inTargetSection = false;
                    ReadOnlySpan<char> currentSection = null;

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comments. If you deleted all keys successfully, also skip.
                        if (processed.Count(x => !x) == 0
                            || line.StartsWith("#".AsSpan(), StringComparison.Ordinal)
                            || line.StartsWith(";".AsSpan(), StringComparison.Ordinal)
                            || line.StartsWith("//".AsSpan(), StringComparison.Ordinal))
                        {
                            w.WriteLine(rawLine);
                            continue;
                        }

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                            // Start of the section
                            inTargetSection = false;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < keys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
                                    break; // for shorter O(n)
                                }
                            }
                            thisLineProcessed = true;
                            w.WriteLine(rawLine);
                        }

                        // key=value
                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // Key exists
                        {
                            if (inTargetSection) // process here only if we are in target section
                            {
                                ReadOnlySpan<char> lineKey = line.Slice(0, idx).Trim();
                                ReadOnlySpan<char> lineValue = line.Slice(idx + 1).Trim();
                                for (int i = 0; i < keys.Length; i++)
                                {
                                    if (processed[i])
                                        continue;

                                    IniKey key = keys[i];
                                    if (currentSection.Equals(key.Section.AsSpan(), StringComparison.OrdinalIgnoreCase)
                                        && lineKey.Equals(key.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    { // key exists, so do not write this line, which lead to 'deletion'
                                        w.WriteLine($"{key.Value}={lineValue.ToString()}");
                                        thisLineProcessed = true;
                                        processed[i] = true;
                                    }
                                }
                            }
                        }

                        if (!thisLineProcessed)
                            w.WriteLine(rawLine);
                    }
                }

                if (processed.Any(x => x))
                    FileHelper.FileReplaceEx(tempPath, file);

                return processed;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region DeleteKey
        public static bool DeleteKey(string file, IniKey iniKey)
        {
            return InternalDeleteKeys(file, new IniKey[] { iniKey })[0];
        }
        public static bool DeleteKey(string file, string section, string key)
        {
            return InternalDeleteKeys(file, new IniKey[] { new IniKey(section, key) })[0];
        }
        public static bool[] DeleteKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalDeleteKeys(file, iniKeys);
        }
        private static bool[] InternalDeleteKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            IniKey[] keys = iniKeys.ToArray();
            bool[] processed = new bool[keys.Length];
            for (int i = 0; i < processed.Length; i++)
                processed[i] = false;

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                if (!File.Exists(file))
                    return processed; // All False

                string tempPath = FileHelper.GetTempFile();
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed; // All False
                    }

                    string rawLine;
                    bool inTargetSection = false;
                    ReadOnlySpan<char> currentSection = null;

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comments. If you deleted all keys successfully, also skip.
                        if (processed.Count(x => !x) == 0 ||
                            line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith("//".AsSpan(), StringComparison.Ordinal))
                        {
                            w.WriteLine(rawLine);
                            continue;
                        }

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                            // Start of the section
                            inTargetSection = false;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < keys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
                                    break; // for shorter O(n)
                                }
                            }
                            thisLineProcessed = true;
                            w.WriteLine(rawLine);
                        }

                        // key=value
                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // Key exists
                        {
                            if (inTargetSection) // process here only if we are in target section
                            {
                                ReadOnlySpan<char> lineKey = line.Slice(0, idx).Trim();
                                for (int i = 0; i < keys.Length; i++)
                                {
                                    if (processed[i])
                                        continue;

                                    if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase)
                                        && lineKey.Equals(keys[i].Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    { // key exists, so do not write this line, which lead to 'deletion'
                                        thisLineProcessed = true;
                                        processed[i] = true;
                                    }
                                }
                            }
                        }

                        if (!thisLineProcessed)
                            w.WriteLine(rawLine);
                    }
                }

                if (0 < processed.Count(x => x))
                    FileHelper.FileReplaceEx(tempPath, file);

                return processed;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region ReadSection
        public static IniKey[] ReadSection(string file, IniKey iniKey)
        {
            return InternalReadSection(file, new string[] { iniKey.Section }).Select(x => x.Value).First();
        }
        public static IniKey[] ReadSection(string file, string section)
        {
            return InternalReadSection(file, new string[] { section }).Select(x => x.Value).First();
        }
        public static Dictionary<string, IniKey[]> ReadSections(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalReadSection(file, iniKeys.Select(x => x.Section));
        }
        public static Dictionary<string, IniKey[]> ReadSections(string file, IEnumerable<string> sections)
        {
            return InternalReadSection(file, sections);
        }
        private static Dictionary<string, IniKey[]> InternalReadSection(string file, IEnumerable<string> sections)
        {
            string[] sectionNames = sections.ToArray();
            Dictionary<string, List<IniKey>> secDict = new Dictionary<string, List<IniKey>>(StringComparer.OrdinalIgnoreCase);
            foreach (string section in sectionNames)
                secDict[section] = null;

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader reader = new StreamReader(file, encoding, true))
                {
                    string rawLine;
                    bool inTargetSection = false;
                    string currentSection = null;
                    List<IniKey> currentIniKeys = null;

                    while ((rawLine = reader.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                        if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith("//".AsSpan(), StringComparison.Ordinal)) // Ignore comment
                            continue;

                        if (inTargetSection)
                        {
                            Debug.Assert(currentSection != null);

                            int idx = line.IndexOf('=');
                            if (idx != -1 && idx != 0) // there is key, and key name is not empty
                            {
                                string key = line.Slice(0, idx).Trim().ToString();
                                string value = line.Slice(idx + 1).Trim().ToString();
                                currentIniKeys.Add(new IniKey(currentSection, key, value));
                            }
                            else
                            {
                                // Search if current section ended
                                if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                                    line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                                {
                                    // Only sections contained in sectionNames will be targeted
                                    inTargetSection = false;
                                    currentSection = null;

                                    string foundSection = line.Slice(1, line.Length - 2).ToString();
                                    int sIdx = Array.FindIndex(sectionNames, x => x.Equals(foundSection, StringComparison.OrdinalIgnoreCase));
                                    if (sIdx != -1)
                                    {
                                        inTargetSection = true;
                                        currentSection = foundSection;
                                        if (secDict[currentSection] == null)
                                            secDict[currentSection] = new List<IniKey>(16);
                                        currentIniKeys = secDict[currentSection];
                                    }
                                }
                            }
                        }
                        else
                        { // not in section
                          // Check if encountered section head Ex) [Process]
                            if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                                line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                            {
                                // Only sections contained in iniKeys will be targeted
                                string foundSection = line.Slice(1, line.Length - 2).ToString();
                                int sIdx = Array.FindIndex(sectionNames, x => x.Equals(foundSection, StringComparison.OrdinalIgnoreCase));
                                if (sIdx != -1)
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
                                    if (secDict[currentSection] == null)
                                        secDict[currentSection] = new List<IniKey>(16);
                                    currentIniKeys = secDict[currentSection];
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                rwLock.ExitReadLock();
            }

            return secDict.ToDictionary(x => x.Key, x => x.Value?.ToArray(), StringComparer.OrdinalIgnoreCase);
        }
        #endregion

        #region AddSection
        public static bool AddSection(string file, IniKey iniKey)
        {
            return InternalAddSection(file, new List<string> { iniKey.Section });
        }
        public static bool AddSection(string file, string section)
        {
            return InternalAddSection(file, new List<string> { section });
        }
        public static bool AddSections(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalAddSection(file, iniKeys.Select(x => x.Section).ToList());
        }
        public static bool AddSections(string file, IEnumerable<string> sections)
        {
            return InternalAddSection(file, sections);
        }
        /// <summary>
        /// Return true if success
        /// </summary>
        private static bool InternalAddSection(string file, IEnumerable<string> sections)
        {
            List<string> sectionList = sections.ToList();
            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                // Not exist -> Create and exit
                if (!File.Exists(file))
                {
                    using (StreamWriter writer = new StreamWriter(file, false, Encoding.UTF8))
                    {
                        for (int i = 0; i < sectionList.Count; i++)
                        {
                            if (0 < i)
                                writer.WriteLine();
                            writer.WriteLine($"[{sectionList[i]}]");
                        }

                        writer.Close();
                    }
                    return true;
                }

                string tempPath = FileHelper.GetTempFile();
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    // Is Original File Empty?
                    if (r.Peek() == -1)
                    {
                        r.Close();

                        // Write all and exit
                        for (int i = 0; i < sectionList.Count; i++)
                        {
                            if (0 < i)
                                w.WriteLine();
                            w.WriteLine($"[{sectionList[i]}]");
                        }

                        w.Close();

                        FileHelper.FileReplaceEx(tempPath, file);
                        return true;
                    }

                    string rawLine;
                    List<string> processedSections = new List<string>(sectionList.Count);

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < sectionList.Count; i++)
                            {
                                if (foundSection.Equals(sectionList[i].AsSpan(), StringComparison.OrdinalIgnoreCase))
                                {
                                    processedSections.Add(foundSection.ToString());
                                    sectionList.RemoveAt(i);
                                    break; // for shorter O(n)
                                }
                            }
                            thisLineProcessed = true;
                            w.WriteLine(rawLine);
                        }

                        if (!thisLineProcessed)
                            w.WriteLine(rawLine);

                        // End of file
                        if (r.Peek() == -1)
                        { // If sections were not added, add it now
                            List<int> processedIdxs = new List<int>(sectionList.Count);
                            for (int i = 0; i < sectionList.Count; i++)
                            { // At this time, only not found section remains in iniKeys
                                if (processedSections.Any(s => s.Equals(sectionList[i], StringComparison.OrdinalIgnoreCase)) == false)
                                {
                                    processedSections.Add(sectionList[i]);
                                    w.WriteLine($"\r\n[{sectionList[i]}]");
                                }
                                processedIdxs.Add(i);
                            }
                            foreach (int i in processedIdxs.OrderByDescending(x => x))
                                sectionList.RemoveAt(i);
                        }
                    }
                }

                if (sectionList.Count == 0)
                {
                    FileHelper.FileReplaceEx(tempPath, file);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region WriteSectionFast
        /// <summary>
        /// Write to section fast, designed for EncodedFile.Encode()
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteSectionFast(string file, string section, IList<string> lines)
        {
            return InternalWriteSectionFast(file, section, lines);
        }

        /// <summary>
        /// Write to section fast, designed for EncodedFile.Encode()
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteSectionFast(string file, string section, string str)
        {
            return InternalWriteSectionFast(file, section, str);
        }

        /// <summary>
        /// Write to section fast, designed for EncodedFile.Encode()
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteSectionFast(string file, string section, TextReader tr)
        {
            return InternalWriteSectionFast(file, section, tr);
        }

        private static bool InternalWriteSectionFast(string file, string section, object content)
        {
            void WriteContent(TextWriter w)
            {
                w.WriteLine($"[{section}]");
                switch (content)
                {
                    case string str:
                        w.WriteLine(str);
                        break;
                    case IList<string> strs:
                        foreach (string str in strs)
                            w.WriteLine(str);
                        break;
                    case IReadOnlyList<string> strs:
                        foreach (string str in strs)
                            w.WriteLine(str);
                        break;
                    case TextReader tr:
                        string readLine;
                        while ((readLine = tr.ReadLine()) != null)
                            w.WriteLine(readLine);
                        break;
                    default:
                        throw new ArgumentException("Invalid content");
                }
            }

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                // If file does not exist or blank, just create new file and insert keys.
                if (!File.Exists(file))
                {
                    using (StreamWriter w = new StreamWriter(file, false, Encoding.UTF8))
                    {
                        WriteContent(w);
                    }

                    return true;
                }

                bool finished = false;
                string tempPath = FileHelper.GetTempFile();
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    string rawLine;
                    bool passThisSection = false;

                    // Main Logic
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            passThisSection = false;

                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                            if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                            {
                                WriteContent(w);

                                passThisSection = true;
                                finished = true;
                            }
                        }

                        if (!passThisSection)
                            w.WriteLine(rawLine);
                    }

                    // End of file
                    if (!finished)
                    {
                        WriteContent(w);
                    }
                }

                FileHelper.FileReplaceEx(tempPath, file);
                return true;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region RenameSection
        public static bool RenameSection(string file, IniKey iniKey)
        {
            return InternalRenameSection(file, new IniKey[] { iniKey })[0];
        }
        public static bool RenameSection(string file, string srcSection, string destSection)
        {
            return InternalRenameSection(file, new IniKey[] { new IniKey(srcSection, destSection) })[0];
        }
        public static bool[] RenameSections(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalRenameSection(file, iniKeys);
        }

        /// <summary>
        /// Internal method for RenameSection
        /// </summary>
        /// <param name="file"></param>
        /// <param name="iniKeys">Section is src section name, Key is dest section name</param>
        /// <returns></returns>
        private static bool[] InternalRenameSection(string file, IEnumerable<IniKey> iniKeys)
        {
            IniKey[] keys = iniKeys.ToArray();
            bool[] processed = new bool[keys.Length];

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                if (!File.Exists(file))
                    return processed;

                string tempPath = FileHelper.GetTempFile();
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed;
                    }

                    string rawLine;

                    // Main Logic
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                        bool thisLineProcessed = false;

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < keys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                IniKey key = keys[i];
                                if (foundSection.Equals(key.Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                { // Rename this section
                                    w.WriteLine($"[{key.Key}]");
                                    thisLineProcessed = true;
                                    processed[i] = true;
                                    break; // for shorter O(n)
                                }
                            }
                        }

                        if (!thisLineProcessed)
                            w.WriteLine(rawLine);
                    }
                }

                if (processed.Any(x => x))
                    FileHelper.FileReplaceEx(tempPath, file);

                return processed;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region DeleteSection
        public static bool DeleteSection(string file, IniKey iniKey)
        {
            return InternalDeleteSection(file, new string[] { iniKey.Section })[0];
        }
        public static bool DeleteSection(string file, string section)
        {
            return InternalDeleteSection(file, new string[] { section })[0];
        }
        public static bool[] DeleteSections(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalDeleteSection(file, iniKeys.Select(x => x.Section));
        }
        public static bool[] DeleteSections(string file, IEnumerable<string> sections)
        {
            return InternalDeleteSection(file, sections);
        }
        /// <summary>
        /// Return true if success
        /// </summary>
        /// <param name="file"></param>
        /// <param name="sections"></param>
        /// <returns></returns>
        private static bool[] InternalDeleteSection(string file, IEnumerable<string> sections)
        {
            List<string> sectionList = sections.ToList();
            bool[] processed = new bool[sectionList.Count];

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                if (!File.Exists(file))
                    return processed;

                string tempPath = FileHelper.GetTempFile();
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed;
                    }

                    string rawLine;
                    bool ignoreCurrentSection = false;

                    // Main Logic
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                            ignoreCurrentSection = false;

                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < sectionList.Count; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(sectionList[i].AsSpan(), StringComparison.OrdinalIgnoreCase))
                                { // Delete this section!
                                    ignoreCurrentSection = true;
                                    processed[i] = true;
                                    break; // for shorter O(n)
                                }
                            }
                        }

                        if (!ignoreCurrentSection)
                            w.WriteLine(rawLine);
                    }
                }

                if (processed.Any(x => x))
                    FileHelper.FileReplaceEx(tempPath, file);

                return processed;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region ReadRawSection
        public static List<string> ReadRawSection(string file, IniKey iniKey, bool includeEmptyLine = false)
        {
            return InternalReadRawSection(file, new string[] { iniKey.Section }, includeEmptyLine).Select(x => x.Value).First();
        }
        public static List<string> ReadRawSection(string file, string section, bool includeEmptyLine = false)
        {
            return InternalReadRawSection(file, new string[] { section }, includeEmptyLine).Select(x => x.Value).First();
        }
        public static Dictionary<string, List<string>> ReadRawSections(string file, IEnumerable<IniKey> iniKeys, bool includeEmptyLine = false)
        {
            return InternalReadRawSection(file, iniKeys.Select(x => x.Section), includeEmptyLine);
        }
        public static Dictionary<string, List<string>> ReadRawSections(string file, IEnumerable<string> sections, bool includeEmptyLine = false)
        {
            return InternalReadRawSection(file, sections, includeEmptyLine);
        }
        private static Dictionary<string, List<string>> InternalReadRawSection(string file, IEnumerable<string> sections, bool includeEmptyLine)
        {
            List<string> sectionNames = sections.ToList();
            Dictionary<string, List<string>> secDict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string section in sectionNames)
                secDict[section] = null;

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                {
                    string rawLine;
                    bool inTargetSection = false;
                    string currentSection = null;
                    List<string> currentContents = null;

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                        if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith("//".AsSpan(), StringComparison.Ordinal)) // Ignore comment
                            continue;

                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            inTargetSection = false;

                            string foundSection = line.Slice(1, line.Length - 2).ToString();
                            int sIdx = sectionNames.FindIndex(x => x.Equals(foundSection, StringComparison.OrdinalIgnoreCase));
                            if (sIdx != -1)
                            {
                                inTargetSection = true;
                                currentSection = foundSection;

                                secDict[currentSection] = new List<string>(16);
                                currentContents = secDict[currentSection];

                                sectionNames.RemoveAt(sIdx);
                                continue;
                            }
                        }

                        if (inTargetSection)
                        {
                            Debug.Assert(currentSection != null);

                            if (includeEmptyLine)
                                currentContents.Add(line.ToString());
                            else if (0 < line.Length)
                                currentContents.Add(line.ToString());
                        }
                    }
                }
            }
            finally
            {
                rwLock.ExitReadLock();
            }

            return secDict;
        }
        #endregion

        #region Merge
        public static bool Merge(string srcFile, string destFile)
        {
            IniFile srcIniFile = new IniFile(srcFile);

            bool result = true;
            // kvSec => Key: Section Value:<Key-Value>
            foreach (var kvSec in srcIniFile.Sections)
            {
                List<IniKey> keys = new List<IniKey>();

                // kvKey => Key:Key Value:Value
                foreach (var kvKey in kvSec.Value)
                    keys.Add(new IniKey(kvSec.Key, kvKey.Key, kvKey.Value));

                result &= InternalWriteKeys(destFile, keys);
            }

            return result;
        }

        public static bool Merge(string srcFile1, string srcFile2, string destFile)
        {
            IniFile[] srcIniFiles =
            {
                new IniFile(srcFile1),
                new IniFile(srcFile2),
            };

            bool result = true;
            foreach (IniFile srcIniFile in srcIniFiles)
            {
                // kvSec => Key: Section Value:<Key-Value>
                foreach (var kvSec in srcIniFile.Sections)
                {
                    List<IniKey> keys = new List<IniKey>();

                    // kvKey => Key:Key Value:Value
                    foreach (var kvKey in kvSec.Value)
                        keys.Add(new IniKey(kvSec.Key, kvKey.Key, kvKey.Value));

                    result &= InternalWriteKeys(destFile, keys);
                }
            }

            return result;
        }
        #endregion

        #region Compact
        public static void Compact(string filePath)
        {
            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(filePath))
            {
                rwLock = LockDict[filePath];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[filePath] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                // Append IniKey into existing file
                string tempPath = FileHelper.GetTempFile();
                Encoding encoding = EncodingHelper.DetectBom(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    while (true)
                    {
                        string rawLine = r.ReadLine();
                        if (rawLine == null) // End of file
                            break;

                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) && line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        { // Section head encountered (e.g. [DestinationDirs]), just trim it
                            w.WriteLine(line.ToString());
                        }
                        else
                        {
                            int idx = line.IndexOf('=');
                            if (idx != -1 && idx != 0) // there is key, and key name is not empty
                            { // Ini style entry, reformat into [Key=Value] template
                                string key = line.Slice(0, idx).Trim().ToString();
                                string value = line.Slice(idx + 1).Trim().ToString();
                                w.WriteLine($"{key}={value}");
                            }
                            else
                            { // Non-ini style entry, trim only end of it (Commands are often indented)
                                w.WriteLine(line.ToString());
                            }
                        }
                    }
                }

                FileHelper.FileReplaceEx(tempPath, filePath);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region Fast Forward
        /// <summary>
        /// Move position of TextReader to read content of specific .ini section
        /// Designed for fast read in EncodedFile class
        /// </summary>
        /// <param name="tr">TextReader to fast-forward.</param>
        /// <param name="section">Section to find.</param>
        public static void FastForwardTextReader(TextReader tr, string section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            // Read base64 block directly from file
            string rawLine;
            while ((rawLine = tr.ReadLine()) != null)
            { // Read text line by line
                ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                // Ignore comment
                if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                    line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                    line.StartsWith("//".AsSpan(), StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                    line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                { // Start of section
                    ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2); // Remove [ and ]

                    // Found target section, so return
                    if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }
        }

        /// <summary>
        /// Copy from TextReader to TextWriter until specific .ini section.
        /// Designed for fast write in EncodedFile class
        /// </summary>
        /// <param name="tr">Source</param>
        /// <param name="tw">Destination</param>
        /// <param name="section">Section to find. Use null for full copy.</param>
        /// <param name="copyFromNewSection">If tr is in the middle of a section, start copy after finding new section</param>
        public static void FastForwardTextWriter(TextReader tr, TextWriter tw, string section, bool copyFromNewSection)
        {
            bool enableCopy = !copyFromNewSection;

            string rawLine;
            while ((rawLine = tr.ReadLine()) != null)
            { // Read text line by line
                if (enableCopy)
                    tw.WriteLine(rawLine);

                if (section == null && !enableCopy)
                {
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                        line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                    { // Start of section
                        tw.WriteLine(rawLine);
                        enableCopy = true;
                    }
                }
                else if (section != null)
                {
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                        line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                    { // Start of section
                        if (!enableCopy)
                            tw.WriteLine(rawLine);
                        enableCopy = true;

                        // Found target section, so return
                        ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2); // Remove [ and ]
                        if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                            return;
                    }
                }
            }

            // Target section not found, insert section header at the end of TextWriter
            if (section != null)
            {
                tw.WriteLine();
                tw.WriteLine($"[{section}]");
            }
        }
        #endregion

        #region Utility
        public static bool IsLineComment(string line)
        {
            return line.StartsWith("#", StringComparison.Ordinal) ||
                   line.StartsWith(";", StringComparison.Ordinal) ||
                   line.StartsWith("//", StringComparison.Ordinal);
        }

        public static List<string> FilterCommentLines(IEnumerable<string> lines)
        {
            return lines.Where(x => 0 < x.Length && !IsLineComment(x)).ToList();
        }

        public static List<string> FilterInvalidIniLines(IEnumerable<string> lines)
        {
            return lines.Where(x =>
            {
                if (x.Length == 0)
                    return false;
                int idx = x.IndexOf('=');
                return idx != 0 && idx != -1;
            }).ToList();
        }

        /// <summary>
        /// Parse INI style strings into dictionary
        /// </summary>
        public static Dictionary<string, string> ParseIniLinesIniStyle(IEnumerable<string> lines)
        {
            // This regex exclude %A%=BCD form.
            // Used [^=] to prevent '=' in key.
            return InternalParseIniLinesRegex(@"^(?<!\/\/|#|;)([^%=\r\n]+)=(.*)$", lines);
        }

        /// <summary>
        /// Parse PEBakery-Variable style strings into dictionary, format of %VarKey%=VarValue. 
        /// </summary>
        public static Dictionary<string, string> ParseIniLinesVarStyle(IEnumerable<string> lines)
        {
            // Used [^=] to prevent '=' in key.
            return InternalParseIniLinesRegex(@"^%([^=]+)%=(.*)$", lines);
        }

        /// <summary>
        /// Parse strings with regex.
        /// </summary>
        private static Dictionary<string, string> InternalParseIniLinesRegex(string regex, IEnumerable<string> lines)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines)
            {
                Regex regexInstance = new Regex(regex, RegexOptions.Compiled);
                MatchCollection matches = regexInstance.Matches(line);

                // Make instances of sections
                for (int i = 0; i < matches.Count; i++)
                {
                    string key = matches[i].Groups[1].Value.Trim();
                    string value = matches[i].Groups[2].Value.Trim();
                    dict[key] = value;
                }
            }
            return dict;
        }


        /// <summary>
        /// Parse section to dictionary.
        /// </summary>
        public static Dictionary<string, string> ParseIniSectionToDict(string file, string section)
        {
            List<string> lines = ParseIniSection(file, section);
            return lines == null ? null : ParseIniLinesIniStyle(lines);
        }

        public static List<string> ParseIniSection(string file, string section)
        {

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                List<string> lines = new List<string>();

                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                {
                    string rawLine;
                    bool appendState = false;
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comment
                        if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith("//".AsSpan(), StringComparison.Ordinal))
                            continue;

                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        { // Start of section
                            if (appendState)
                                break;

                            // Remove [ and ]
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                            if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                appendState = true;
                        }
                        else
                        {
                            int idx;
                            if ((idx = line.IndexOf('=')) != -1 && idx != 0)
                            { // valid ini key, and not empty
                                if (appendState)
                                    lines.Add(line.ToString());
                            }
                        }
                    }

                    if (!appendState) // Section not found
                        return null;
                }
                return lines;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public static List<string> ParseRawSection(string file, string section)
        {
            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                List<string> lines = new List<string>();

                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                {
                    string rawLine;
                    bool appendState = false;
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comment
                        if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith("//".AsSpan(), StringComparison.Ordinal))
                            continue;

                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        { // Start of section
                            if (appendState)
                                break;

                            // Remove [ and ]
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                            if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                appendState = true;
                        }
                        else if (appendState)
                        {
                            if (line.Length != 0)
                                lines.Add(line.ToString());
                        }
                    }

                    if (!appendState) // Section not found
                        return null;
                }
                return lines;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Parse section to dictionary array.
        /// </summary>
        public static Dictionary<string, string>[] ParseSectionsToDicts(string file, string[] sections)
        {
            List<string>[] lines = ParseIniSections(file, sections);
            Dictionary<string, string>[] dict = new Dictionary<string, string>[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                dict[i] = ParseIniLinesIniStyle(lines[i]);
            return dict;
        }
        /// <summary>
        /// Parse sections to string 2D array.
        /// </summary>
        public static List<string>[] ParseIniSections(string file, IEnumerable<string> sectionList)
        {
            string[] sections = sectionList.Distinct().ToArray(); // Remove duplicate

            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                List<string>[] lines = new List<string>[sections.Length];
                for (int i = 0; i < sections.Length; i++)
                    lines[i] = new List<string>();

                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, true))
                {
                    string rawLine;
                    int currentSection = -1; // -1 == empty, 0, 1, ... == index value of sections array
                    List<int> processedSectionIdxs = new List<int>();

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        if (sections.Length < processedSectionIdxs.Count)
                            break;

                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        { // Start of section
                            bool isSectionFound = false;
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                            for (int i = 0; i < sections.Length; i++)
                            {
                                if (processedSectionIdxs.Contains(i))
                                    continue;

                                if (foundSection.Equals(sections[i].AsSpan(), StringComparison.Ordinal))
                                {
                                    isSectionFound = true;
                                    processedSectionIdxs.Add(i);
                                    currentSection = i;
                                    break;
                                }
                            }
                            if (!isSectionFound)
                                currentSection = -1;
                        }
                        else
                        {
                            int idx;
                            if ((idx = line.IndexOf('=')) != -1 && idx != 0)
                            { // valid ini key
                                if (currentSection != -1) // current section is target, and key is empty
                                    lines[currentSection].Add(line.ToString());
                            }
                        }
                    }

                    r.Close();
                }

                return lines;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public static Dictionary<string, Dictionary<string, string>> ParseToDict(string srcFile)
        {
            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(srcFile))
            {
                rwLock = LockDict[srcFile];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[srcFile] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                Dictionary<string, Dictionary<string, string>> dict = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

                if (!File.Exists(srcFile))
                    return dict; // Return Empty dict if srcFile does not exist

                Encoding encoding = EncodingHelper.DetectBom(srcFile);
                using (StreamReader r = new StreamReader(srcFile, encoding))
                {
                    // Is Original File Empty?
                    if (r.Peek() == -1)
                    {
                        r.Close();

                        // Return Empty Dict
                        return dict;
                    }

                    string rawLine;
                    string section = null;

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                        if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                            line.StartsWith("//".AsSpan(), StringComparison.Ordinal)) // Ignore comment
                            continue;

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            section = line.Slice(1, line.Length - 2).ToString();
                            dict[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            continue;
                        }

                        // Read Keys
                        if (section != null)
                        {
                            int idx = line.IndexOf('=');
                            if (idx != -1 && idx != 0) // there is key, and key name is not empty
                            {
                                string key = line.Slice(0, idx).ToString();
                                string value = line.Slice(idx + 1).ToString();

                                dict[section][key] = value;
                            }
                        }
                    }
                }

                return dict;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get name of sections from INI file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static List<string> GetSectionNames(string file)
        {
            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                List<string> sections = new List<string>();

                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, true))
                {
                    string rawLine;
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal)) // Count sections
                            sections.Add(line.Slice(1, line.Length - 2).ToString());
                    }
                }

                return sections;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Check if INI file has specified section
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        public static bool ContainsSection(string file, string section)
        {
            ReaderWriterLockSlim rwLock;
            if (LockDict.ContainsKey(file))
            {
                rwLock = LockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                LockDict[file] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                bool result = false;

                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                {
                    string rawLine;
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                            if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                            {
                                result = true;
                                break;
                            }
                        }
                    }
                }

                return result;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Used to handle [EncodedFile-InterfaceEncoded-*] section
        /// Return null if failed
        /// </summary>
        public static (string key, string value) GetKeyValueFromLine(string rawLine)
        {
            int idx = rawLine.IndexOf('=');
            if (idx == -1) // Unable to find key and value
                return (null, null);

            string key = rawLine.Substring(0, idx).Trim();
            string value = rawLine.Substring(idx + 1).Trim();
            return (key, value);
        }

        /// <summary>
        /// Used to handle [EncodedFile-InterfaceEncoded-*] section
        /// Return null if failed
        /// </summary>
        public static (List<string> Keys, List<string> Values) GetKeyValueFromLines(IList<string> rawLines)
        {
            List<string> keys = new List<string>();
            List<string> values = new List<string>();
            foreach (string rawLine in rawLines)
            {
                (string key, string value) = GetKeyValueFromLine(rawLine);
                if (key == null || value == null)
                    return (null, null);
                keys.Add(key);
                values.Add(value);
            }
            return (keys, values);
        }
        #endregion
    }
    #endregion

    #region IniFile
    public class IniFile
    {
        public string FilePath { get; set; }
        public Dictionary<string, Dictionary<string, string>> Sections { get; set; }

        public IniFile(string filePath)
        {
            FilePath = filePath;
            Sections = IniReadWriter.ParseToDict(filePath);
        }
    }
    #endregion
}
