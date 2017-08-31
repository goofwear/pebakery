﻿using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Lib;
using PEBakery.Helper;
using System.Text;
using System.Collections.Generic;

namespace UnitTest.Libs
{
    [TestClass]
    public class UnitTest_IniLib
    {
        #region GetKey
        [TestMethod]
        public void IniLib_GetKey_1()
        {
            string tempFile = Path.GetTempFileName();

            Assert.IsNull(Ini.GetKey(tempFile, "Section", "Key"));
        }

        [TestMethod]
        public void IniLib_GetKey_2()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section]");
                w.WriteLine("Key=Value");
                w.Close();
            }

            Assert.IsTrue(Ini.GetKey(tempFile, "Section", "Key").Equals("Value", StringComparison.Ordinal));
            Assert.IsTrue(Ini.GetKey(tempFile, "Section", "key").Equals("Value", StringComparison.Ordinal));
            Assert.IsTrue(Ini.GetKey(tempFile, "section", "Key").Equals("Value", StringComparison.Ordinal));
            Assert.IsTrue(Ini.GetKey(tempFile, "section", "key").Equals("Value", StringComparison.Ordinal));
            Assert.IsFalse(Ini.GetKey(tempFile, "Section", "Key").Equals("value", StringComparison.Ordinal));
        }
        
        [TestMethod]
        public void IniLib_GetKey_3()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("1=A");
                w.WriteLine("2=B");
                w.WriteLine("3=C");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("4=D");
                w.WriteLine("5=E");
                w.WriteLine("[Section3]");
                w.WriteLine("6=F");
                w.WriteLine("7=G");
                w.WriteLine("8=H");
                w.Close();
            }

            Assert.IsTrue(Ini.GetKey(tempFile, "Section1", "1").Equals("A", StringComparison.Ordinal));
            Assert.IsTrue(Ini.GetKey(tempFile, "Section1", "2").Equals("B", StringComparison.Ordinal));
            Assert.IsTrue(Ini.GetKey(tempFile, "section1", "3").Equals("C", StringComparison.Ordinal));
            Assert.IsTrue(Ini.GetKey(tempFile, "Section2", "4").Equals("D", StringComparison.Ordinal));
            Assert.IsTrue(Ini.GetKey(tempFile, "Section2", "5").Equals("E", StringComparison.Ordinal));
            Assert.IsTrue(Ini.GetKey(tempFile, "section3", "6").Equals("F", StringComparison.Ordinal));
            Assert.IsTrue(Ini.GetKey(tempFile, "section3", "7").Equals("G", StringComparison.Ordinal));
            Assert.IsTrue(Ini.GetKey(tempFile, "section3", "8").Equals("H", StringComparison.Ordinal));
        }
        #endregion

        #region GetKeys
        [TestMethod]
        public void IniLib_GetKeys_1()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("1=A");
                w.WriteLine("2=B");
                w.WriteLine("3=C");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("4=D");
                w.WriteLine("5=E");
                w.WriteLine("[Section3]");
                w.WriteLine("6=F");
                w.WriteLine("7=G");
                w.WriteLine("8=H");
                w.Close();
            }

            IniKey[] keys = new IniKey[10];
            keys[0] = new IniKey("Section1", "3");
            keys[1] = new IniKey("Section3", "8");
            keys[2] = new IniKey("Section2", "5");
            keys[3] = new IniKey("Section3", "6");
            keys[4] = new IniKey("Section1", "4");
            keys[5] = new IniKey("Section2", "1");
            keys[6] = new IniKey("Section1", "2");
            keys[7] = new IniKey("Section3", "7");
            keys[8] = new IniKey("Section1", "1");
            keys[9] = new IniKey("Section2", "4");

            keys = Ini.GetKeys(tempFile, keys);
            Assert.IsTrue(keys[0].Value.Equals("C", StringComparison.Ordinal));
            Assert.IsTrue(keys[1].Value.Equals("H", StringComparison.Ordinal));
            Assert.IsTrue(keys[2].Value.Equals("E", StringComparison.Ordinal));
            Assert.IsTrue(keys[3].Value.Equals("F", StringComparison.Ordinal));
            Assert.IsNull(keys[4].Value);
            Assert.IsNull(keys[5].Value);
            Assert.IsTrue(keys[6].Value.Equals("B", StringComparison.Ordinal));
            Assert.IsTrue(keys[7].Value.Equals("G", StringComparison.Ordinal));
            Assert.IsTrue(keys[8].Value.Equals("A", StringComparison.Ordinal));
            Assert.IsTrue(keys[9].Value.Equals("D", StringComparison.Ordinal));
        }
        #endregion

        #region SetKey
        [TestMethod]
        public void IniLib_SetKey_1()
        {
            string tempFile = Path.GetTempFileName();

            Assert.IsTrue(Ini.SetKey(tempFile, "Section", "Key", "Value"));

            string read;
            using (StreamReader r = new StreamReader(tempFile))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section]");
            b.AppendLine("Key=Value");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_SetKey_2()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section]");
                w.WriteLine("Key=A");
                w.Close();
            }

            Assert.IsTrue(Ini.SetKey(tempFile, "Section", "Key", "B"));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section]");
            b.AppendLine("Key=B");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region SetKeys
        [TestMethod]
        public void IniLib_SetKeys_1()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);

            IniKey[] keys = new IniKey[3];
            keys[0] = new IniKey("Section2", "20", "English");
            keys[1] = new IniKey("Section1", "10", "한국어");
            keys[2] = new IniKey("Section3", "30", "Français");

            Assert.IsTrue(Ini.SetKeys(tempFile, keys));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section2]");
            b.AppendLine("20=English");
            b.AppendLine();
            b.AppendLine("[Section1]");
            b.AppendLine("10=한국어");
            b.AppendLine();
            b.AppendLine("[Section3]");
            b.AppendLine("30=Français");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_SetKeys_2()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("00=A");
                w.WriteLine("01=B");
                w.WriteLine("02=C");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("10=한");
                w.WriteLine("11=국");
                w.WriteLine("[Section3]");
                w.WriteLine("20=韓");
                w.WriteLine("21=國");
                w.Close();
            }

            IniKey[] keys = new IniKey[5];
            keys[0] = new IniKey("Section1", "03", "D");
            keys[1] = new IniKey("Section3", "22", "語");
            keys[2] = new IniKey("Section2", "12", "어");
            keys[3] = new IniKey("Section1", "04", "Unicode");
            keys[4] = new IniKey("Section2", "13", "한글");

            Assert.IsTrue(Ini.SetKeys(tempFile, keys));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section1]");
            b.AppendLine("00=A");
            b.AppendLine("01=B");
            b.AppendLine("02=C");
            b.AppendLine("03=D");
            b.AppendLine("04=Unicode");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("10=한");
            b.AppendLine("11=국");
            b.AppendLine("12=어");
            b.AppendLine("13=한글");
            b.AppendLine("[Section3]");
            b.AppendLine("20=韓");
            b.AppendLine("21=國");
            b.AppendLine("22=語");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region WriteRawLine
        [TestMethod]
        public void IniLib_WriteRawLine_1()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);

            Assert.IsTrue(Ini.WriteRawLine(tempFile, "Section", "RawLine"));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();  
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section]");
            b.AppendLine("RawLine");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_WriteRawLine_2()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section]");
                w.WriteLine("1=A");
                w.Close();
            }

            Assert.IsTrue(Ini.WriteRawLine(tempFile, "Section", "LineAppend", true));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section]");
            b.AppendLine("1=A");
            b.AppendLine("LineAppend");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_WriteRawLine_3()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section]");
                w.WriteLine("1=A");
                w.Close();
            }

            Assert.IsTrue(Ini.WriteRawLine(tempFile, "Section", "LinePrepend", false));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section]");
            b.AppendLine("LinePrepend");
            b.AppendLine("1=A");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region WriteRawLines
        [TestMethod]
        public void IniLib_WriteRawLines_1()
        {
            string tempFile = Path.GetTempFileName();

            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("10=한국어");
                w.WriteLine("11=中文");
                w.WriteLine("12=にほんご");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("20=English");
                w.WriteLine("[Section3]");
                w.WriteLine("30=Français");
                w.Close();
            }

            IniKey[] keys = new IniKey[5];
            keys[0] = new IniKey("Section2", "영어");
            keys[1] = new IniKey("Section1", "한중일 (CJK)");
            keys[2] = new IniKey("Section3", "프랑스어");
            keys[3] = new IniKey("Section4", "עברית");
            keys[4] = new IniKey("Section4", "العربية");

            Assert.IsTrue(Ini.WriteRawLines(tempFile, keys, false));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section1]");
            b.AppendLine("한중일 (CJK)");
            b.AppendLine("10=한국어");
            b.AppendLine("11=中文");
            b.AppendLine("12=にほんご");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("영어");
            b.AppendLine("20=English");
            b.AppendLine("[Section3]");
            b.AppendLine("프랑스어");
            b.AppendLine("30=Français");
            b.AppendLine();
            b.AppendLine("[Section4]");
            b.AppendLine("עברית");
            b.AppendLine("العربية");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_WriteRawLines_2()
        {
            string tempFile = Path.GetTempFileName();

            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("10=한국어");
                w.WriteLine("11=中文");
                w.WriteLine("12=にほんご");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("20=English");
                w.WriteLine("[Section3]");
                w.WriteLine("30=Français");
                w.Close();
            }

            IniKey[] keys = new IniKey[5];
            keys[0] = new IniKey("Section2", "영어");
            keys[1] = new IniKey("Section1", "한중일 (CJK)");
            keys[2] = new IniKey("Section3", "프랑스어");
            keys[3] = new IniKey("Section4", "עברית");
            keys[4] = new IniKey("Section4", "العربية");

            Assert.IsTrue(Ini.WriteRawLines(tempFile, keys, true));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section1]");
            b.AppendLine("10=한국어");
            b.AppendLine("11=中文");
            b.AppendLine("12=にほんご");
            b.AppendLine("한중일 (CJK)");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("20=English");
            b.AppendLine("영어");
            b.AppendLine("[Section3]");
            b.AppendLine("30=Français");
            b.AppendLine("프랑스어");
            b.AppendLine();
            b.AppendLine("[Section4]");
            b.AppendLine("עברית");
            b.AppendLine("العربية");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region DeleteKey
        [TestMethod]
        public void IniLib_DeleteKey_1()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section]");
                w.WriteLine("1=A");
                w.WriteLine("2=B");
                w.WriteLine("3=C");
                w.WriteLine("4=D");
                w.Close();
            }

            Assert.IsTrue(Ini.DeleteKey(tempFile, "Section", "2"));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section]");
            b.AppendLine("1=A");
            b.AppendLine("3=C");
            b.AppendLine("4=D");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_DeleteKey_2()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section]");
                w.WriteLine("1=A");
                w.WriteLine("2=B");
                w.WriteLine("3=C");
                w.WriteLine("4=D");
                w.Close();
            }

            // Induce Error
            Assert.IsFalse(Ini.DeleteKey(tempFile, "Section", "5"));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section]");
            b.AppendLine("1=A");
            b.AppendLine("2=B");
            b.AppendLine("3=C");
            b.AppendLine("4=D");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_DeleteKey_3()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section]");
                w.WriteLine("1=A");
                w.WriteLine("2=B");
                w.WriteLine("3=C");
                w.WriteLine("4=D");
                w.Close();
            }

            Assert.IsTrue(Ini.DeleteKey(tempFile, "Section", "2"));
            Assert.IsTrue(Ini.DeleteKey(tempFile, "Section", "4"));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section]");
            b.AppendLine("1=A");
            b.AppendLine("3=C");
            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region DeleteKeys
        [TestMethod]
        public void IniLib_DeleteKeys_1()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("00=A");
                w.WriteLine("01=B");
                w.WriteLine("02=C");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("10=한");
                w.WriteLine("11=국");
                w.WriteLine("[Section3]");
                w.WriteLine("20=韓");
                w.WriteLine("21=國");
                w.Close();
            }

            IniKey[] keys = new IniKey[3];
            keys[0] = new IniKey("Section1", "00");
            keys[1] = new IniKey("Section3", "20");
            keys[2] = new IniKey("Section2", "11");
        
            bool result = Ini.DeleteKeys(tempFile, keys);

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section1]");
            b.AppendLine("01=B");
            b.AppendLine("02=C");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("10=한");
            b.AppendLine("[Section3]");
            b.AppendLine("21=國");

            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region AddSection
        [TestMethod]
        public void IniLib_AddSection_1()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            Assert.IsTrue(Ini.AddSection(tempFile, "Section"));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section]");

            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_AddSection_2()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section]");
                w.WriteLine("1=A");
                w.WriteLine("2=B");
                w.Close();
            }

            Assert.IsTrue(Ini.AddSection(tempFile, "Section"));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section]");
            b.AppendLine("1=A");
            b.AppendLine("2=B");

            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_AddSection_3()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("00=A");
                w.WriteLine("01=B");
                w.WriteLine("02=C");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("10=한");
                w.WriteLine("11=국");
                w.WriteLine("[Section3]");
                w.WriteLine("20=韓");
                w.WriteLine("21=國");
                w.Close();
            }

            Assert.IsTrue(Ini.AddSection(tempFile, "Section4"));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section1]");
            b.AppendLine("00=A");
            b.AppendLine("01=B");
            b.AppendLine("02=C");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("10=한");
            b.AppendLine("11=국");
            b.AppendLine("[Section3]");
            b.AppendLine("20=韓");
            b.AppendLine("21=國");
            b.AppendLine();
            b.AppendLine("[Section4]");

            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region AddSections
        [TestMethod]
        public void IniLib_AddSections_1()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);

            List<string> sections = new List<string>()
            {
                "Section1",
                "Section3",
                "Section2",
            };

            Assert.IsTrue(Ini.AddSections(tempFile, sections));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section1]");
            b.AppendLine();
            b.AppendLine("[Section3]");
            b.AppendLine();
            b.AppendLine("[Section2]");

            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_AddSections_2()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("00=A");
                w.WriteLine("01=B");
                w.WriteLine("02=C");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("10=한");
                w.WriteLine("11=국");
                w.WriteLine("[Section3]");
                w.WriteLine("20=韓");
                w.WriteLine("21=國");
                w.Close();
            }

            List<string> sections = new List<string>()
            {
                "Section4",
                "Section2",
            };

            Assert.IsTrue(Ini.AddSections(tempFile, sections));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section1]");
            b.AppendLine("00=A");
            b.AppendLine("01=B");
            b.AppendLine("02=C");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("10=한");
            b.AppendLine("11=국");
            b.AppendLine("[Section3]");
            b.AppendLine("20=韓");
            b.AppendLine("21=國");
            b.AppendLine();
            b.AppendLine("[Section4]");

            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region DeleteSection
        [TestMethod]
        public void IniLib_DeleteSection_1()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);

            // Induce Error
            Assert.IsFalse(Ini.DeleteSection(tempFile, "Section"));

            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                string read = r.ReadToEnd();
                string comp = string.Empty;

                r.Close();
                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
        }

        [TestMethod]
        public void IniLib_DeleteSection_2()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section]");
                w.WriteLine("1=A");
                w.WriteLine("2=B");
                w.Close();
            }

            Assert.IsTrue(Ini.DeleteSection(tempFile, "Section"));

            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            { // Must be same
                string read = r.ReadToEnd();
                string comp = string.Empty;

                r.Close();
                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
        }

        [TestMethod]
        public void IniLib_DeleteSection_3()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("00=A");
                w.WriteLine("01=B");
                w.WriteLine("02=C");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("10=한");
                w.WriteLine("11=국");
                w.WriteLine("[Section3]");
                w.WriteLine("20=韓");
                w.WriteLine("21=國");
                w.Close();
            }

            Assert.IsTrue(Ini.DeleteSection(tempFile, "Section2"));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section1]");
            b.AppendLine("00=A");
            b.AppendLine("01=B");
            b.AppendLine("02=C");
            b.AppendLine();
            b.AppendLine("[Section3]");
            b.AppendLine("20=韓");
            b.AppendLine("21=國");

            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region DeleteSections
        [TestMethod]
        public void IniLib_DeleteSections_1()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("00=A");
                w.WriteLine("01=B");
                w.WriteLine("02=C");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("10=한");
                w.WriteLine("11=국");
                w.WriteLine("[Section3]");
                w.WriteLine("20=韓");
                w.WriteLine("21=國");
                w.Close();
            }

            List<string> sections = new List<string>()
            {
                "Section1",
                "Section3",
            };

            Assert.IsTrue(Ini.DeleteSections(tempFile, sections));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section2]");
            b.AppendLine("10=한");
            b.AppendLine("11=국");

            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }

        [TestMethod]
        public void IniLib_DeleteSections_2()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.WriteLine("[Section1]");
                w.WriteLine("00=A");
                w.WriteLine("01=B");
                w.WriteLine("02=C");
                w.WriteLine();
                w.WriteLine("[Section2]");
                w.WriteLine("10=한");
                w.WriteLine("11=국");
                w.WriteLine("[Section3]");
                w.WriteLine("20=韓");
                w.WriteLine("21=國");
                w.Close();
            }

            List<string> sections = new List<string>()
            {
                "Section4",
                "Section2",
            };

            Assert.IsFalse(Ini.DeleteSections(tempFile, sections));

            string read;
            Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
            using (StreamReader r = new StreamReader(tempFile, encoding))
            {
                read = r.ReadToEnd();
                r.Close();
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section1]");
            b.AppendLine("00=A");
            b.AppendLine("01=B");
            b.AppendLine("02=C");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("10=한");
            b.AppendLine("11=국");
            b.AppendLine("[Section3]");
            b.AppendLine("20=韓");
            b.AppendLine("21=國");

            string comp = b.ToString();

            Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
        }
        #endregion
    }
}