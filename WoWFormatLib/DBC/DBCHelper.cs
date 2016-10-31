﻿using DBFilesClient.NET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WoWFormatLib.Utils;

namespace WoWFormatLib.DBC
{
    public class DBCHelper
    {
        public static uint[] getTexturesByModelFilename(int modelID, int flag, int texid = 0)
        {
            List<uint> results = new List<uint>();
            switch (flag)
            {
                case 1:
                case 2:

                    //ModelFileData.db2 (FileDataID) 1272528 => (ModelFileDataID) 37177
                    var modelFileData = new Storage<ModelFileDataEntry>(CASC.OpenFile(@"DBFilesClient/ModelFileData.db2"));
                    var modelFileDataID = modelFileData[modelID].modelFileDataID;

                    //ItemDisplayInfoMaterialRes.db2 (ID) 37177 => (ItemDisplayInfoID) 53536, (TextureFileDataID) 59357
                    var itemDisplayInfoMaterialRes = new Storage<ItemDisplayInfoMaterialResEntry>(CASC.OpenFile(@"DBFilesClient/ItemDisplayInfoMaterialRes.db2"));
                    var textureFileDataID = itemDisplayInfoMaterialRes[modelFileDataID].textureFileDataID;

                    // TextureFileData
                    var textureFileData = new Storage<TextureFileDataEntry>(CASC.OpenFile(@"DBFilesClient/TextureFileData.db2"));
                    foreach(var entry in textureFileData)
                    {
                        if(entry.Value.textureFileDataID == textureFileDataID)
                        {
                            results.Add(entry.Value.fileDataID);
                        }
                    }

                    break;

                case 11:
                    var creatureModelData = new Storage<CreatureModelDataEntry>(CASC.OpenFile(@"DBFilesClient/CreatureModelData.db2"));
                    foreach (var cmdEntry in creatureModelData)
                    {
                        if (cmdEntry.Value.fileDataID == modelID)
                        {
                            var creatureDisplayInfo = new Storage<CreatureDisplayInfoEntry>(CASC.OpenFile(@"DBFilesClient/CreatureDisplayInfo.db2"));
                            foreach (var cdiEntry in creatureDisplayInfo)
                            {
                                if (cdiEntry.Value.ModelID == cmdEntry.Key)
                                {
                                    results.Add(cdiEntry.Value.TextureVariation[0]);
                                }
                            }
                        }
                    }
                    break;
            }

            /*if (flag == 1)
            {
                var instance = new Storage<CreatureDisplayInfoEntry>("CreatureDisplayInfoEntry.db2");
                if (modelfilename.StartsWith("character", StringComparison.CurrentCultureIgnoreCase))
                {
                    DBCReader<ChrRaceRecord> reader = new DBCReader<ChrRaceRecord>("DBFilesClient\\ChrRaces.dbc");

                    int race_id = 1; //Default to human male
                    int gender_id = 0; 

                    for (int i = 0; i < reader.recordCount; i++)
                    {
                        if (modelfilename.IndexOf(reader[i].clientFileString, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                        {
                            race_id = reader[i].ID;
                            if (modelfilename.IndexOf("female", 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                            {
                                gender_id = 1;
                            }
                            else
                            {
                                gender_id = 0;
                            }
                            break;
                        }
                    }

                    DBCReader<CharSectionRecord> secreader = new DBCReader<CharSectionRecord>("DBFilesClient\\CharSections.dbc");
                    for (int i = 0; i < secreader.recordCount; i++)
                    {
                        if (secreader[i].TextureName_0 == "")
                        {
                            continue; //skip empty shit
                        }
                        int addhd = 0;
                        if (modelfilename.IndexOf("_HD", 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                        {
                            addhd = 5;
                        }
                        if (secreader[i].raceID == race_id && secreader[i].sexID == gender_id)
                        {
                            if (modelfilename.IndexOf("_HD", 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                            {
                                if (secreader[i].baseSection == (0 + addhd) && texid == 1)
                                {
                                    filenames.Add(secreader[i].TextureName_0);
                                }
                            }
                            else
                            {
                                if (secreader[i].baseSection == (0 + addhd) && texid == 0)
                                {
                                    filenames.Add(secreader[i].TextureName_0);
                                }
                            }
                        }
                    }

                    Console.WriteLine("Detected model as race ID " + race_id + " and gender " + gender_id);
                    Console.WriteLine("[NYI] Type 1 character texture lookups aren't implemented yet");
                }
                else if (modelfilename.StartsWith("creature", StringComparison.CurrentCultureIgnoreCase))
                {
                    Console.WriteLine("RUNNING TYPE 1 DETECTION");
                    DBCReader<FileDataRecord> reader = new DBCReader<FileDataRecord>("DBFilesClient\\FileData.dbc");
                    string[] modelnamearr = modelfilename.Split('\\');
                    string modelonly = modelnamearr[modelnamearr.Count() - 1];
                    modelonly = Path.ChangeExtension(modelonly, ".M2");
                    for (int i = 0; i < reader.recordCount; i++)
                    {
                        if (reader[i].FileName == modelonly)
                        {
                            Console.WriteLine("Found ID in FileData.dbc: " + reader[i].ID);
                            DBCReader<CreatureModelDataRecord> cmdreader = new DBCReader<CreatureModelDataRecord>("DBFilesClient\\CreatureModelData.dbc");
                            for (int cmdi = 0; cmdi < cmdreader.recordCount; cmdi++)
                            {
                                if (reader[i].ID == cmdreader[cmdi].fileDataID)
                                {
                                    Console.WriteLine("Found Creature ID in CreatureModelData.dbc: " + cmdreader[cmdi].ID);
                                    DBCReader<CreatureDisplayInfoRecord> cdireader = new DBCReader<CreatureDisplayInfoRecord>("DBFilesClient\\CreatureDisplayInfo.dbc");
                                    for (int cdii = 0; cdii < cdireader.recordCount; cdii++)
                                    {
                                        if (cdireader[cdii].modelID == cmdreader[cmdi].ID && cdireader[cdii].textureVariation_0 != null)
                                        {
                                            //filenames.Add(cdireader[cdii].textureVariation_0);
                                            filenames.Add(modelfilename.Replace(modelonly, cdireader[cdii].textureVariation_0 + ".blp"));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else if (modelfilename.StartsWith("item", StringComparison.CurrentCultureIgnoreCase))
                {
                    Console.WriteLine("[NYI] Type 1 item texture lookups aren't implemented yet");
                }
                else
                {
                    Console.WriteLine("[NYI] Type 1 texture lookups aren't implemented yet (model: " + modelfilename + ")");
                }
            }
            else if (flag == 11)
            {
                DBCReader<FileDataRecord> reader = new DBCReader<FileDataRecord>("DBFilesClient\\FileData.dbc");
                string[] modelnamearr = modelfilename.Split('\\');
                string modelonly = modelnamearr[modelnamearr.Count() - 1];
                modelonly = Path.ChangeExtension(modelonly, ".M2");
                for (int i = 0; i < reader.recordCount; i++)
                {
                    if (reader[i].FileName == modelonly)
                    {
                        Console.WriteLine("Found ID in FileData.dbc: " + reader[i].ID);
                        DBCReader<CreatureModelDataRecord> cmdreader = new DBCReader<CreatureModelDataRecord>("DBFilesClient\\CreatureModelData.dbc");
                        for (int cmdi = 0; cmdi < cmdreader.recordCount; cmdi++)
                        {
                            if (reader[i].ID == cmdreader[cmdi].fileDataID)
                            {
                                Console.WriteLine("Found Creature ID in CreatureModelData.dbc: " + cmdreader[cmdi].ID);
                                DBCReader<CreatureDisplayInfoRecord> cdireader = new DBCReader<CreatureDisplayInfoRecord>("DBFilesClient\\CreatureDisplayInfo.dbc");
                                for (int cdii = 0; cdii < cdireader.recordCount; cdii++)
                                {
                                    if (cdireader[cdii].modelID == cmdreader[cmdi].ID && cdireader[cdii].textureVariation_0 != null)
                                    {
                                        filenames.Add(cdireader[cdii].textureVariation_0);
                                    }
                                }
                            }
                        }
                    }
                }
            }
           */
            uint[] ret = results.Distinct().ToArray();
            return ret;
        }
    }
}
