﻿using CASCExplorer.Properties;
using SereniaBLPLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CASCExplorer
{
    delegate void OnStorageChangedDelegate();
    delegate void OnCleanupDelegate();

    class CASCViewHelper
    {
        private ExtractProgress extractProgress;
        private CASCHandler _casc;
        private CASCFolder _root;
        private CASCFolder _currentFolder;
        private List<ICASCEntry> _displayedEntries;
        private CASCEntrySorter Sorter = new CASCEntrySorter();
        private ScanForm scanForm;
        private NumberFormatInfo sizeNumberFmt = new NumberFormatInfo()
        {
            NumberGroupSizes = new int[] { 3, 3, 3, 3, 3 },
            NumberDecimalDigits = 0,
            NumberGroupSeparator = " "
        };

        public event OnStorageChangedDelegate OnStorageChanged;
        public event OnCleanupDelegate OnCleanup;

        public CASCHandler CASC => _casc;

        public CASCFolder Root => _root;

        public CASCFolder CurrentFolder => _currentFolder;

        public List<ICASCEntry> DisplayedEntries => _displayedEntries;

        public void ExtractFiles(NoFlickerListView filesList)
        {
            if (_currentFolder == null)
                return;

            if (!filesList.HasSelection)
                return;

            if (extractProgress == null)
                extractProgress = new ExtractProgress();

            var files = CASCFolder.GetFiles(_displayedEntries, filesList.SelectedIndices.Cast<int>()).ToList();
            extractProgress.SetExtractData(_casc, files);
            extractProgress.ShowDialog();
        }

        public async Task ExtractInstallFiles(Action<int> progressCallback)
        {
            if (_casc == null)
                return;

            IProgress<int> progress = new Progress<int>(progressCallback);

            await Task.Run(() =>
            {
                var installFiles = _casc.Install.GetEntries("Windows");
                var build = _casc.Config.BuildName;

                int numFiles = installFiles.Count();
                int numDone = 0;

                foreach (var file in installFiles)
                {
                    EncodingEntry enc;

                    if (_casc.Encoding.GetEntry(file.MD5, out enc))
                        _casc.SaveFileTo(enc.Key, Path.Combine("data", build, "install_files"), file.Name);

                    progress.Report((int)(++numDone / (float)numFiles * 100));
                }
            });
        }

        public async Task AnalyzeUnknownFiles(Action<int> progressCallback)
        {
            if (_casc == null)
                return;

            IProgress<int> progress = new Progress<int>(progressCallback);

            await Task.Run(() =>
            {
                FileScanner scanner = new FileScanner(_casc, _root);

                Dictionary<int, string> idToName = new Dictionary<int, string>();

                if (_casc.Config.GameType == CASCGameType.WoW)
                {
                    if (_casc.FileExists("DBFilesClient\\SoundEntries.db2"))
                    {
                        using (Stream stream = _casc.OpenFile("DBFilesClient\\SoundEntries.db2"))
                        {
                            DB2Reader se = new DB2Reader(stream);

                            foreach (var row in se)
                            {
                                string name = row.Value.GetField<string>(2);

                                int type = row.Value.GetField<int>(1);

                                bool many = row.Value.GetField<int>(4) > 0;

                                for (int i = 3; i < 23; i++)
                                    idToName[row.Value.GetField<int>(i)] = "unknown\\sound\\" + name + (many ? "_" + (i - 2).ToString("D2") : "") + (type == 28 ? ".mp3" : ".ogg");
                            }
                        }
                    }

                    if (_casc.FileExists("DBFilesClient\\SoundKit.db2") && _casc.FileExists("DBFilesClient\\SoundKitEntry.db2"))
                    {
                        using (Stream skStream = _casc.OpenFile("DBFilesClient\\SoundKit.db2"))
                        using (Stream skeStream = _casc.OpenFile("DBFilesClient\\SoundKitEntry.db2"))
                        {
                            DB5Reader sk = new DB5Reader(skStream);
                            DB5Reader ske = new DB5Reader(skeStream);

                            Dictionary<int, List<int>> lookup = new Dictionary<int, List<int>>();

                            foreach (var row in ske)
                            {
                                int soundKitId = row.Value.GetField<int>(3);

                                if (!lookup.ContainsKey(soundKitId))
                                    lookup[soundKitId] = new List<int>();

                                lookup[soundKitId].Add(row.Value.GetField<int>(0));
                            }

                            foreach (var row in sk)
                            {
                                string name = row.Value.GetField<string>(0).Replace(':', '_');

                                int type = row.Value.GetField<byte>(12);

                                List<int> ske_entries;

                                if (!lookup.TryGetValue(row.Key, out ske_entries))
                                    continue;

                                bool many = ske_entries.Count > 1;

                                int i = 0;

                                foreach (var fid in ske_entries)
                                {
                                    idToName[fid] = "unknown\\sound\\" + name + (many ? "_" + (i + 1).ToString("D2") : "") + (type == 28 ? ".mp3" : ".ogg");
                                    i++;
                                }
                            }
                        }
                    }
                }

                CASCFolder unknownFolder = _root.GetEntry("unknown") as CASCFolder;

                if (unknownFolder == null)
                    return;

                IEnumerable<CASCFile> files = CASCFolder.GetFiles(unknownFolder.Entries.Select(kv => kv.Value), null, true);
                int numTotal = files.Count();
                int numDone = 0;

                WowRootHandler wowRoot = _casc.Root as WowRootHandler;

                foreach (var unknownEntry in files)
                {
                    CASCFile unknownFile = unknownEntry as CASCFile;

                    string name;
                    if (idToName.TryGetValue(wowRoot.GetFileDataIdByHash(unknownFile.Hash), out name))
                        unknownFile.FullName = name;
                    else
                    {
                        string ext = scanner.GetFileExtension(unknownFile);
                        unknownFile.FullName += ext;

                        if (ext == ".m2")
                        {
                            using (var m2file = _casc.OpenFile(unknownFile.Hash))
                            using (var br = new BinaryReader(m2file))
                            {
                                m2file.Position = 0x138;
                                string m2name = br.ReadCString();

                                unknownFile.FullName = "unknown\\" + m2name + ".m2";
                            }
                        }
                    }

                    progress.Report((int)(++numDone / (float)numTotal * 100));
                }

                _casc.Root.Dump();
            });
        }

        public void ScanFiles()
        {
            if (_casc == null || _root == null)
                return;

            if (scanForm == null)
            {
                scanForm = new ScanForm();
                scanForm.Initialize(_casc, _root);
            }

            scanForm.Reset();
            scanForm.ShowDialog();
        }

        public void UpdateListView(CASCFolder baseEntry, NoFlickerListView fileList, string filter)
        {
            Wildcard wildcard = new Wildcard(filter, false, RegexOptions.IgnoreCase);

            // Sort
            _displayedEntries = baseEntry.Entries.Where(v => v.Value is CASCFolder || (v.Value is CASCFile && wildcard.IsMatch(v.Value.Name))).
                OrderBy(v => v.Value, Sorter).Select(kv => kv.Value).ToList();

            _currentFolder = baseEntry;

            // Update
            fileList.VirtualListSize = 0;
            fileList.VirtualListSize = _displayedEntries.Count;

            if (fileList.VirtualListSize > 0)
            {
                fileList.EnsureVisible(0);
                fileList.SelectedIndex = 0;
                fileList.FocusedItem = fileList.Items[0];
            }
        }

        public void CreateTreeNodes(TreeNode node)
        {
            CASCFolder baseEntry = node.Tag as CASCFolder;

            // check if we have dummy node
            if (node.Nodes["tempnode"] != null)
            {
                // remove dummy node
                node.Nodes.Clear();

                var orderedEntries = baseEntry.Entries.OrderBy(v => v.Value.Name);

                // Create nodes dynamically
                foreach (var it in orderedEntries)
                {
                    CASCFolder entry = it.Value as CASCFolder;

                    if (entry != null && node.Nodes[entry.Name] == null)
                    {
                        TreeNode newNode = node.Nodes.Add(entry.Name);
                        newNode.Tag = entry;
                        newNode.Name = entry.Name;

                        if (entry.Entries.Count(v => v.Value is CASCFolder) > 0)
                            newNode.Nodes.Add(new TreeNode() { Name = "tempnode" }); // add dummy node
                    }
                }
            }
        }

        public void OpenStorage(string arg, bool online)
        {
            Cleanup();

            using (var initForm = new InitForm())
            {
                if (online)
                    initForm.LoadOnlineStorage(arg);
                else
                    initForm.LoadLocalStorage(arg);

                DialogResult res = initForm.ShowDialog();

                if (res != DialogResult.OK)
                    return;

                _casc = initForm.CASC;
                _root = initForm.Root;
            }

            Sorter.CASC = _casc;

            OnStorageChanged?.Invoke();
        }

        public void ChangeLocale(string locale)
        {
            if (_casc == null)
                return;

            OnCleanup?.Invoke();

            Settings.Default.LocaleFlags = (LocaleFlags)Enum.Parse(typeof(LocaleFlags), locale);

            _root = _casc.Root.SetFlags(Settings.Default.LocaleFlags, Settings.Default.ContentFlags);
            _casc.Root.MergeInstall(_casc.Install);

            OnStorageChanged?.Invoke();
        }

        public void ChangeContentFlags(bool set)
        {
            if (_casc == null)
                return;

            OnCleanup?.Invoke();

            if (set)
                Settings.Default.ContentFlags |= ContentFlags.LowViolence;
            else
                Settings.Default.ContentFlags &= ~ContentFlags.LowViolence;

            _root = _casc.Root.SetFlags(Settings.Default.LocaleFlags, Settings.Default.ContentFlags);
            _casc.Root.MergeInstall(_casc.Install);

            OnStorageChanged?.Invoke();
        }

        public void SetSort(int column)
        {
            Sorter.SortColumn = column;
            Sorter.Order = Sorter.Order == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        }

        public void GetSize(NoFlickerListView fileList)
        {
            if (_currentFolder == null)
                return;

            if (!fileList.HasSelection)
                return;

            var files = CASCFolder.GetFiles(_displayedEntries, fileList.SelectedIndices.Cast<int>());

            long size = files.Sum(f => (long)f.GetSize(_casc));

            MessageBox.Show(string.Format(sizeNumberFmt, "{0:N} bytes", size));
        }

        public void PreviewFile(NoFlickerListView fileList)
        {
            if (_currentFolder == null)
                return;

            if (!fileList.HasSingleSelection)
                return;

            var file = _displayedEntries[fileList.SelectedIndex] as CASCFile;

            var extension = Path.GetExtension(file.Name);

            if (extension != null)
            {
                switch (extension.ToLower())
                {
                    case ".blp":
                        {
                            PreviewBlp(file);
                            break;
                        }
                    case ".txt":
                    case ".ini":
                    case ".wtf":
                    case ".lua":
                    case ".toc":
                    case ".xml":
                    case ".htm":
                    case ".html":
                    case ".lst":
                        {
                            PreviewText(file);
                            break;
                        }
                    //case ".wav":
                    //case ".ogg":
                    //    {
                    //        PreviewSound(file);
                    //        break;
                    //    }
                    default:
                        {
                            MessageBox.Show(string.Format("Preview of {0} is not supported yet", extension), "Not supported file");
                            break;
                        }
                }
            }
        }

        private void PreviewText(CASCFile file)
        {
            using (var stream = _casc.OpenFile(file.Hash))
            {
                var text = new StreamReader(stream).ReadToEnd();
                var form = new Form { FormBorderStyle = FormBorderStyle.SizableToolWindow, StartPosition = FormStartPosition.CenterParent };
                form.Controls.Add(new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    Dock = DockStyle.Fill,
                    Text = text,
                    ScrollBars = ScrollBars.Both
                });
                form.Show();
            }
        }

        private void PreviewBlp(CASCFile file)
        {
            using (var stream = _casc.OpenFile(file.Hash))
            {
                var blp = new BlpFile(stream);
                var bitmap = blp.GetBitmap(0);
                var form = new ImagePreviewForm(bitmap);
                form.Show();
            }
        }

        public void CreateListViewItem(RetrieveVirtualItemEventArgs e)
        {
            if (_currentFolder == null)
                return;

            if (e.ItemIndex < 0 || e.ItemIndex >= _displayedEntries.Count)
                return;

            ICASCEntry entry = _displayedEntries[e.ItemIndex];

            var localeFlags = LocaleFlags.None;
            var contentFlags = ContentFlags.None;
            var size = "<DIR>";

            if (entry is CASCFile)
            {
                var rootInfosLocale = _casc.Root.GetEntries(entry.Hash);

                if (rootInfosLocale.Any())
                {
                    EncodingEntry enc;

                    if (_casc.Encoding.GetEntry(rootInfosLocale.First().MD5, out enc))
                    {
                        size = enc.Size.ToString("N", sizeNumberFmt) ?? "0";
                    }
                    else
                    {
                        size = "NYI";

                        if (_casc.Root is OwRootHandler)
                        {
                            OWRootEntry rootEntry;

                            if ((_casc.Root as OwRootHandler).GetEntry(entry.Hash, out rootEntry))
                            {
                                size = rootEntry.pkgIndexRec.Size.ToString("N", sizeNumberFmt) ?? "0";
                            }
                        }
                    }

                    foreach (var rootInfo in rootInfosLocale)
                    {
                        localeFlags |= rootInfo.LocaleFlags;
                        contentFlags |= rootInfo.ContentFlags;
                    }
                }
                else
                {
                    var installInfos = _casc.Install.GetEntries(entry.Hash);

                    if (installInfos.Any())
                    {
                        EncodingEntry enc;

                        if (_casc.Encoding.GetEntry(installInfos.First().MD5, out enc))
                        {
                            size = enc.Size.ToString("N", sizeNumberFmt) ?? "0";

                            //foreach (var rootInfo in rootInfosLocale)
                            //{
                            //    if (rootInfo.Block != null)
                            //    {
                            //        localeFlags |= rootInfo.Block.LocaleFlags;
                            //        contentFlags |= rootInfo.Block.ContentFlags;
                            //    }
                            //}
                        }
                    }
                }
            }

            e.Item = new ListViewItem(new string[]
            {
                entry.Name,
                entry is CASCFolder ? "Folder" : Path.GetExtension(entry.Name),
                localeFlags.ToString(),
                contentFlags.ToString(),
                size
            })
            { ImageIndex = entry is CASCFolder ? 0 : 2 };
        }

        public void Cleanup()
        {
            Sorter.CASC = null;

            _currentFolder = null;
            _root = null;

            _displayedEntries?.Clear();
            _displayedEntries = null;

            _casc?.Clear();
            _casc = null;

            OnCleanup?.Invoke();
        }

        public void Search(NoFlickerListView fileList, SearchForVirtualItemEventArgs e)
        {
            bool ignoreCase = true;
            bool searchUp = false;
            int SelectedIndex = fileList.SelectedIndex;

            var comparisonType = ignoreCase
                                    ? StringComparison.InvariantCultureIgnoreCase
                                    : StringComparison.InvariantCulture;

            if (searchUp)
            {
                for (var i = SelectedIndex - 1; i >= 0; --i)
                {
                    var op = _displayedEntries[i].Name;
                    if (op.IndexOf(e.Text, comparisonType) != -1)
                    {
                        e.Index = i;
                        break;
                    }
                }
            }
            else
            {
                for (int i = SelectedIndex + 1; i < fileList.Items.Count; ++i)
                {
                    var op = _displayedEntries[i].Name;
                    if (op.IndexOf(e.Text, comparisonType) != -1)
                    {
                        e.Index = i;
                        break;
                    }
                }
            }
        }

        public void ExportListFile()
        {
            using (StreamWriter sw = new StreamWriter("listfile_export.txt"))
            {
                foreach (var file in CASCFolder.GetFiles(_root.Entries.Select(kv => kv.Value), null, true).OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    if (CASC.FileExists(file.Hash))
                        sw.WriteLine(file.FullName);
                }
            }
        }

        public void ExtractCASCSystemFiles()
        {
            if (_casc == null)
                return;

            EncodingEntry enc;

            _casc.SaveFileTo(_casc.Config.EncodingKey, ".", "encoding");

            //_casc.SaveFileTo(_casc.Config.PatchKey, ".", "patch");

            if (_casc.Encoding.GetEntry(_casc.Config.RootMD5, out enc))
                _casc.SaveFileTo(enc.Key, ".", "root");

            if (_casc.Encoding.GetEntry(_casc.Config.InstallMD5, out enc))
                _casc.SaveFileTo(enc.Key, ".", "install");

            if (_casc.Encoding.GetEntry(_casc.Config.DownloadMD5, out enc))
                _casc.SaveFileTo(enc.Key, ".", "download");

            if (_casc.Encoding.GetEntry(_casc.Config.PartialPriorityMD5, out enc))
                _casc.SaveFileTo(enc.Key, ".", "partial-priority");
        }
    }
}
