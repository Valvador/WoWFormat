using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using WoWFormatLib.DBC;
using WoWFormatLib.FileReaders;
using WoWFormatLib.Utils;

namespace WoWFormatLib
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string pathArg = "--path=";
                if (arg.StartsWith(pathArg))
                {
                    string director = arg.Remove(0, pathArg.Length);
                    string[] files = Directory.GetFiles(director, "*.adt");
                    ADTReader reader = new ADTReader();
                    //CASC.InitCasc();
                    for (int j = 0; j < files.Length; j++)
                    {
                        if (!(files[j].EndsWith("lod.adt") || files[j].EndsWith("obj0.adt") || files[j].EndsWith("obj1.adt") || files[j].EndsWith("tex0.adt")))
                        {
                            reader.LoadADT(files[j], false, false, true);
                        }
                    }
                }
            }
        }
    }
}