﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CASCExplorer
{
    class FileScanner
    {
        private static readonly List<string> excludeFileTypes = new List<string>()
        {
            ".ogg", ".mp3", ".wav", ".avi", ".ttf", ".blp", ".sig", ".toc", ".blob", ".anim", ".skin", ".phys"
        };

        private static readonly List<string> extensions = new List<string>()
        {
            ".adt", ".anim", ".avi", ".blob", ".blp", ".bls", ".bone", ".db2", ".dbc", ".html", ".ini", ".lst", ".lua", ".m2", ".mp3", ".ogg",
            ".phys", ".sbt", ".sig", ".skin", ".tex", ".toc", ".ttf", ".txt", ".wdl", ".wdt", ".wfx", ".wmo", ".wtf", ".xml", ".xsd", ".zmp"
        };

        private static readonly Dictionary<byte[], string> MagicNumbers = new Dictionary<byte[], string>()
        {
            { new byte[] { 0x42, 0x4c, 0x50, 0x32 }, ".blp" },
            { new byte[] { 0x4d, 0x44, 0x32, 0x30 }, ".m2" },
            { new byte[] { 0x4d, 0x44, 0x32, 0x31 }, ".m2" },
            { new byte[] { 0x53, 0x59, 0x48, 0x50 }, ".phys" },
            { new byte[] { 0x53, 0x4b, 0x49, 0x4e }, ".skin" },
            { new byte[] { 0x57, 0x44, 0x42, 0x43 }, ".dbc" },
            { new byte[] { 0x57, 0x44, 0x42, 0x35 }, ".db2" },
            { new byte[] { 0x52, 0x56, 0x58, 0x54 }, ".tex" },
            { new byte[] { 0x4f, 0x67, 0x67, 0x53 }, ".ogg" },
            { new byte[] { 0x48, 0x53, 0x58, 0x47 }, ".bls" },
            { new byte[] { 0x52, 0x49, 0x46, 0x46 }, ".wav" },
            { new byte[] { 0x44, 0x55, 0x54, 0x53 }, ".duts" },
            { new byte[] { 0x42, 0x4B, 0x48, 0x44 }, ".bkhd" },
            { new byte[] { 0x45, 0x45, 0x44, 0x43 }, ".eedc" },
            { new byte[] { 0x49, 0x44, 0x33 }, ".mp3" },
            { new byte[] { 0xff, 0xfb }, ".mp3" },
        };

        private CASCHandler CASC;
        private CASCFolder Root;

        public FileScanner(CASCHandler casc, CASCFolder root)
        {
            CASC = casc;
            Root = root;
        }

        public IEnumerable<string> ScanFile(CASCFile file)
        {
            if (excludeFileTypes.Contains(Path.GetExtension(file.FullName).ToLower()))
                yield break;

            Stream fileStream = null;

            try
            {
                fileStream = CASC.OpenFile(file.Hash);
            }
            catch (Exception exc)
            {
                Logger.WriteLine("Skipped {0}, error {1}.", file.FullName, exc.Message);
                yield break;
            }

            using (fileStream)
            {
                int b;
                int state = 1;
                StringBuilder sb = new StringBuilder();

                // look for regex a+(da+)*\.a+ where a = IsAlphaNum() and d = IsFileDelim()
                // using a simple state machine
                while ((b = fileStream.ReadByte()) > -1)
                {
                    if (state == 1 && IsAlphaNum(b) || state == 2 && IsAlphaNum(b) || state == 3 && IsAlphaNum(b)) // alpha
                    {
                        state = 2;
                        sb.Append((char)b);

                        if (sb.Length > 10)
                        {
                            int nextByte = fileStream.ReadByte();

                            if (nextByte == 0)
                            {
                                string foundStr = sb.ToString();

                                foreach (var ext in extensions)
                                    yield return foundStr + ext;
                            }

                            if (nextByte > -1)
                                fileStream.Position -= 1;
                        }
                    }
                    else if (state == 2 && IsFileDelim(b)) // delimiter
                    {
                        state = 3;
                        sb.Append((char)b);
                    }
                    else if (state == 2 && b == 46) // dot
                    {
                        state = 4;
                        sb.Append((char)b);
                    }
                    else if (state == 4 && IsAlphaNum(b)) // extension
                    {
                        state = 5;
                        sb.Append((char)b);
                    }
                    else if (state == 5 && IsAlphaNum(b)) // extension
                    {
                        sb.Append((char)b);
                    }
                    else if (state == 5 && !IsFileChar(b)) // accept
                    {
                        state = 1;
                        if (sb.Length >= 10)
                            yield return sb.ToString();
                        sb.Clear();
                    }
                    else
                    {
                        state = 1;
                        sb.Clear();
                    }
                }
            }
        }

        // dash, space, underscore, point, slash, backslash, a-z, A-Z, 0-9
        private bool IsFileChar(int i)
        {
            return ((i >= 46 && i <= 57) || (i >= 65 && i <= 90) || i == 92 || i == 95 || (i >= 97 && i <= 122));
        }

        // dash, underscore, a-z, A-Z, 0-9
        private bool IsAlphaNum(int i)
        {
            return (i == 45 || (i >= 48 && i <= 57) || (i >= 65 && i <= 90) || i == 95 || (i >= 97 && i <= 122));
        }

        // slash or backslash
        private bool IsFileDelim(int i)
        {
            return (i == 47 || i == 92);
        }

        public string GetFileExtension(CASCFile file)
        {
            try
            {
                using (Stream stream = CASC.OpenFile(file.Hash))
                {
                    byte[] magic = new byte[4];

                    stream.Read(magic, 0, 4);

                    foreach (var number in MagicNumbers)
                    {
                        if (number.Key.EqualsToIgnoreLength(magic))
                        {
                            return number.Value;
                        }
                    }
                }
            }
            catch
            { }
            return string.Empty;
        }
    }
}
