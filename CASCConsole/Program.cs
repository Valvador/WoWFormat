﻿using CASCConsole.Properties;
using CASCExplorer;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CASCConsole
{
    class Program
    {
        static readonly object ProgressLock = new object();

        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Invalid arguments count!");
                Console.WriteLine("Usage: CASCConsole <pattern> <destination> <localeFlags> <contentFlags>");
                return;
            }

            Console.WriteLine("Settings:");
            Console.WriteLine("    WowPath: {0}", Settings.Default.StoragePath);
            Console.WriteLine("    OnlineMode: {0}", Settings.Default.OnlineMode);

            Console.WriteLine("Loading...");

            BackgroundWorkerEx bgLoader = new BackgroundWorkerEx();
            bgLoader.ProgressChanged += BgLoader_ProgressChanged;

            CASCConfig config = Settings.Default.OnlineMode
                ? CASCConfig.LoadOnlineStorageConfig(Settings.Default.Product, "us")
                : CASCConfig.LoadLocalStorageConfig(Settings.Default.StoragePath);

            CASCHandler cascHandler = CASCHandler.OpenStorage(config, bgLoader);

            string pattern = args[0];
            string dest = args[1];
            LocaleFlags locale = (LocaleFlags)Enum.Parse(typeof(LocaleFlags), args[2]);
            ContentFlags content = (ContentFlags)Enum.Parse(typeof(ContentFlags), args[3]);

            cascHandler.Root.LoadListFile(Path.Combine(Environment.CurrentDirectory, "listfile.txt"), bgLoader);
            CASCFolder root = cascHandler.Root.SetFlags(locale, content);

            Console.WriteLine("Loaded.");

            Console.WriteLine("Extract params:");
            Console.WriteLine("    Pattern: {0}", pattern);
            Console.WriteLine("    Destination: {0}", dest);
            Console.WriteLine("    LocaleFlags: {0}", locale);
            Console.WriteLine("    ContentFlags: {0}", content);

            Wildcard wildcard = new Wildcard(pattern, true, RegexOptions.IgnoreCase);

            foreach (var file in CASCFolder.GetFiles(root.Entries.Select(kv => kv.Value)))
            {
                if (wildcard.IsMatch(file.FullName))
                {
                    Console.Write("Extracting '{0}'...", file.FullName);

                    try
                    {
                        cascHandler.SaveFileTo(file.FullName, dest);
                        Console.WriteLine(" Ok!");
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(" Error!");
                        Logger.WriteLine(exc.Message);
                    }
                }
            }

            Console.WriteLine("Extracted.");
        }

        private static void BgLoader_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lock (ProgressLock)
            {
                if (e.UserState != null)
                    Console.WriteLine(e.UserState);

                DrawProgressBar(e.ProgressPercentage, 100, 72, '#');
            }
        }

        private static void DrawProgressBar(long complete, long maxVal, int barSize, char progressCharacter)
        {
            float perc = (float)complete / maxVal;
            DrawProgressBar(perc, barSize, progressCharacter);
        }

        private static void DrawProgressBar(float percent, int barSize, char progressCharacter)
        {
            Console.CursorVisible = false;
            int left = Console.CursorLeft;
            int chars = (int)Math.Round(percent / (1.0f / barSize));
            string p1 = string.Empty, p2 = string.Empty;

            for (int i = 0; i < chars; i++)
                p1 += progressCharacter;
            for (int i = 0; i < barSize - chars; i++)
                p2 += progressCharacter;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(p1);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write(p2);

            Console.ResetColor();
            Console.Write(" {0}%", (percent * 100).ToString("N2"));
            Console.CursorLeft = left;
        }
    }
}
