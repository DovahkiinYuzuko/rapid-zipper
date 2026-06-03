using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SevenZip;

namespace rapid_zipper
{
    public partial class RapidZipper : Form
    {
        public RapidZipper()
        {
            InitializeComponent();
            InitializeFormatComboBox();

            try
            {
                // 1. 前回の実行で作成された古い一時フォルダ (RapidZipper_7z_*) を自動クリーンアップ
                string tempParent = Path.GetTempPath();
                foreach (var dir in Directory.GetDirectories(tempParent, "RapidZipper_7z_*"))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch
                    {
                        // 使用中（他のインスタンス実行中）などの場合は握りつぶす
                    }
                }

                // 2. 7z.dllのパスをUnicodeを含まないTempフォルダに退避させてロード (SevenZipSharpのUnicodeパスバグ回避策)
                // 予測不可能なGUID付きのフォルダ名にすることで、VULN-001のDLL上書き脆弱性を防止
                string sourceDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Environment.Is64BitProcess ? "x64" : "x86", "7z.dll");
                if (File.Exists(sourceDllPath))
                {
                    string randomFolderName = "RapidZipper_7z_" + Guid.NewGuid().ToString("N");
                    string tempDir = Path.Combine(tempParent, randomFolderName, Environment.Is64BitProcess ? "x64" : "x86");
                    Directory.CreateDirectory(tempDir);
                    string destDllPath = Path.Combine(tempDir, "7z.dll");

                    // 最新のDLLを一時フォルダにコピーしてロード
                    File.Copy(sourceDllPath, destDllPath, overwrite: true);
                    SevenZipBase.SetLibraryPath(destDllPath);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("警告: 7z.dll が見つかりません。");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"7z.dllロードエラー: {ex.Message}");
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void InitializeFormatComboBox()
        {
            FormatComboBox.Items.Clear();
            FormatComboBox.Items.Add("ZIP");
            FormatComboBox.Items.Add("7Z");
            FormatComboBox.Items.Add("TAR");
            FormatComboBox.Items.Add("TGZ (tar.gz)");
            FormatComboBox.SelectedIndex = 0;
        }

        private void RapidZipper_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private bool IsArchiveFile(string path)
        {
            if (!File.Exists(path)) return false;
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".zip" || ext == ".7z" || ext == ".rar" || ext == ".tar" || ext == ".gz" || ext == ".tgz";
        }

        private async void RapidZipper_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;

            try
            {
                SetUIProcessing(true);

                // ドロップされたファイルからアーカイブファイルのみを抽出
                var archiveFiles = new System.Collections.Generic.List<string>();
                foreach (var path in paths)
                {
                    if (IsArchiveFile(path))
                    {
                        archiveFiles.Add(path);
                    }
                }

                // 選択された圧縮形式を取得
                string selectedFormat = "ZIP";
                if (InvokeRequired)
                {
                    Invoke(new Action(() => selectedFormat = FormatComboBox.SelectedItem?.ToString() ?? "ZIP"));
                }
                else
                {
                    selectedFormat = FormatComboBox.SelectedItem?.ToString() ?? "ZIP";
                }

                // 1. アーカイブファイルの展開処理
                if (archiveFiles.Count > 0 && archiveFiles.Count == paths.Length)
                {
                    if (archiveFiles.Count == 1)
                    {
                        // 1つのアーカイブファイルを展開
                        string firstFileDir = Path.GetDirectoryName(archiveFiles[0]) ?? string.Empty;
                        string selectedParentDir = string.Empty;

                        if (InvokeRequired)
                        {
                            Invoke(new Action(() =>
                            {
                                using (var dialog = new FolderBrowserDialog())
                                {
                                    dialog.Description = "解凍先フォルダを選択してください";
                                    dialog.InitialDirectory = firstFileDir;
                                    dialog.SelectedPath = firstFileDir;
                                    if (dialog.ShowDialog() == DialogResult.OK)
                                    {
                                        selectedParentDir = dialog.SelectedPath;
                                    }
                                }
                            }));
                        }
                        else
                        {
                            using (var dialog = new FolderBrowserDialog())
                            {
                                dialog.Description = "解凍先フォルダを選択してください";
                                dialog.InitialDirectory = firstFileDir;
                                dialog.SelectedPath = firstFileDir;
                                if (dialog.ShowDialog() == DialogResult.OK)
                                {
                                    selectedParentDir = dialog.SelectedPath;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(selectedParentDir))
                        {
                            await DecompressArchiveAsync(archiveFiles[0], selectedParentDir);

                            DialogResult openFolderResult = MessageBox.Show(
                                "展開が完了しました。フォルダを開きますか？",
                                "完了",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question
                            );

                            if (openFolderResult == DialogResult.Yes)
                            {
                                string archiveNameWithoutExt = Path.GetFileNameWithoutExtension(archiveFiles[0]);
                                string destDir = Path.Combine(selectedParentDir, archiveNameWithoutExt);
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                                {
                                    FileName = Directory.Exists(destDir) ? destDir : selectedParentDir,
                                    UseShellExecute = true
                                });
                            }
                        }
                        else
                        {
                            UpdateStatus("展開処理をキャンセルしました。");
                        }
                    }
                    else
                    {
                        // 複数のアーカイブファイルを順次展開
                        await DecompressMultipleArchivesAsync(archiveFiles.ToArray());
                    }
                }
                // 2. 単一のフォルダがドロップされた場合（自動圧縮）
                else if (paths.Length == 1 && Directory.Exists(paths[0]))
                {
                    SevenZip.CompressionLevel sevenZipLevel = SevenZip.CompressionLevel.Normal;
                    if (selectedFormat == "7Z")
                    {
                        if (InvokeRequired)
                        {
                            Invoke(new Action(() => sevenZipLevel = PromptCompressionLevel()));
                        }
                        else
                        {
                            sevenZipLevel = PromptCompressionLevel();
                        }
                    }
                    await CompressFolderAsync(paths[0], selectedFormat, sevenZipLevel);
                }
                // 3. それ以外（複数アイテムを1つのアーカイブに圧縮）
                else
                {
                    string defaultDir = Path.GetDirectoryName(paths[0]) ?? string.Empty;
                    
                    string ext = ".zip";
                    if (selectedFormat == "TAR") ext = ".tar";
                    else if (selectedFormat.StartsWith("TGZ")) ext = ".tar.gz";
                    else if (selectedFormat == "7Z") ext = ".7z";

                    string defaultZipName = "archive" + ext;
                    if (paths.Length == 1)
                    {
                        defaultZipName = Path.GetFileNameWithoutExtension(paths[0]) + ext;
                    }

                    string destZipPath = string.Empty;

                    if (InvokeRequired)
                    {
                        Invoke(new Action(() =>
                        {
                            using (var sfd = new SaveFileDialog())
                            {
                                sfd.Filter = $"圧縮ファイル (*{ext})|*{ext}";
                                sfd.InitialDirectory = defaultDir;
                                sfd.FileName = defaultZipName;
                                sfd.Title = "圧縮ファイルの保存先を選択してください";

                                if (sfd.ShowDialog() == DialogResult.OK)
                                {
                                    destZipPath = sfd.FileName;
                                }
                            }
                        }));
                    }
                    else
                    {
                        using (var sfd = new SaveFileDialog())
                        {
                            sfd.Filter = $"圧縮ファイル (*{ext})|*{ext}";
                            sfd.InitialDirectory = defaultDir;
                            sfd.FileName = defaultZipName;
                            sfd.Title = "圧縮ファイルの保存先を選択してください";

                            if (sfd.ShowDialog() == DialogResult.OK)
                            {
                                destZipPath = sfd.FileName;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(destZipPath))
                    {
                        SevenZip.CompressionLevel sevenZipLevel = SevenZip.CompressionLevel.Normal;
                        if (selectedFormat == "7Z")
                        {
                            if (InvokeRequired)
                            {
                                Invoke(new Action(() => sevenZipLevel = PromptCompressionLevel()));
                            }
                            else
                            {
                                sevenZipLevel = PromptCompressionLevel();
                            }
                        }
                        await CompressMultipleItemsAsync(paths, destZipPath, selectedFormat, sevenZipLevel);
                    }
                    else
                    {
                        UpdateStatus("圧縮処理をキャンセルしました。");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"エラーが発生しました: {ex.Message}");
            }
            finally
            {
                SetUIProcessing(false);
            }
        }

        private void UpdateStatus(string message)
        {
            if (Statuslabel.InvokeRequired)
            {
                Statuslabel.Invoke(new Action(() => UpdateStatus(message)));
            }
            else
            {
                Statuslabel.Text = message;
            }
        }

        private void SetUIProcessing(bool isProcessing)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetUIProcessing(isProcessing)));
                return;
            }

            if (isProcessing)
            {
                ProcessingBar.Style = ProgressBarStyle.Marquee;
                ProcessingBar.MarqueeAnimationSpeed = 30;
                DragDropPanel.Enabled = false;
            }
            else
            {
                ProcessingBar.Style = ProgressBarStyle.Blocks;
                ProcessingBar.Value = 0;
                DragDropPanel.Enabled = true;
            }
        }

        private async Task CompressFolderAsync(string folderPath, string format, SevenZip.CompressionLevel sevenZipLevel = SevenZip.CompressionLevel.Normal)
        {
            string parentDir = Path.GetDirectoryName(folderPath) ?? string.Empty;
            string folderName = Path.GetFileName(folderPath);

            string ext = ".zip";
            ArchiveType archiveType = ArchiveType.Zip;

            if (format == "TAR")
            {
                ext = ".tar";
                archiveType = ArchiveType.Tar;
            }
            else if (format.StartsWith("TGZ"))
            {
                ext = ".tar.gz";
                archiveType = ArchiveType.Tar;
            }
            else if (format == "7Z")
            {
                ext = ".7z";
            }

            string destZipPath = Path.Combine(parentDir, folderName + ext);
            UpdateStatus($"圧縮中: {folderName}{ext}");

            await Task.Run(() =>
            {
                if (File.Exists(destZipPath))
                {
                    File.Delete(destZipPath);
                }

                if (format == "ZIP")
                {
                    ZipFile.CreateFromDirectory(folderPath, destZipPath, System.IO.Compression.CompressionLevel.Fastest, includeBaseDirectory: false);
                }
                else if (format == "7Z")
                {
                    UpdateStatus("フォルダ内をスキャン中...");
                    var filesToCompress = new System.Collections.Generic.Dictionary<string, string>();
                    AddDirectoryToDictionary(filesToCompress, folderPath, folderPath, string.Empty);

                    UpdateStatus("7z圧縮中...");
                    var compressor = new SevenZipCompressor();
                    compressor.ArchiveFormat = OutArchiveFormat.SevenZip;
                    compressor.CompressionLevel = sevenZipLevel;
                    compressor.CompressionMethod = CompressionMethod.Lzma2;
                    compressor.FastCompression = true;

                    int maxThreads = Math.Max(2, Environment.ProcessorCount / 2);
                    string dictSize = "8m";

                    if (sevenZipLevel == SevenZip.CompressionLevel.Low)
                    {
                        dictSize = "4m";
                    }
                    else if (sevenZipLevel == SevenZip.CompressionLevel.High)
                    {
                        dictSize = "32m";
                        maxThreads = Math.Min(4, maxThreads); // 高圧縮時はメモリ制限のためスレッド数を絞る
                    }
                    else
                    {
                        dictSize = "8m";
                    }

                    compressor.CustomParameters.Add("d", dictSize);
                    compressor.CustomParameters.Add("mt", maxThreads.ToString());
                    compressor.CompressFileDictionary(filesToCompress, destZipPath);
                }
                else
                {
                    using (var fs = File.OpenWrite(destZipPath))
                    {
                        var writerOptions = new WriterOptions(CompressionType.None);
                        if (format.StartsWith("TGZ"))
                        {
                            writerOptions = new WriterOptions(CompressionType.GZip);
                        }

                        using (var writer = WriterFactory.OpenWriter(fs, archiveType, writerOptions))
                        {
                            AddDirectoryToWriter(writer, folderPath, folderPath, string.Empty);
                        }
                    }
                }
            });

            UpdateStatus("圧縮が完了しました。");
        }

        private async Task CompressMultipleItemsAsync(string[] sourcePaths, string destZipPath, string format, SevenZip.CompressionLevel sevenZipLevel = SevenZip.CompressionLevel.Normal)
        {
            UpdateStatus("圧縮処理を準備中...");

            ArchiveType archiveType = ArchiveType.Zip;
            CompressionType compressionType = CompressionType.Deflate;

            if (format == "TAR")
            {
                archiveType = ArchiveType.Tar;
                compressionType = CompressionType.None;
            }
            else if (format.StartsWith("TGZ"))
            {
                archiveType = ArchiveType.Tar;
                compressionType = CompressionType.GZip;
            }

            await Task.Run(() =>
            {
                if (File.Exists(destZipPath))
                {
                    File.Delete(destZipPath);
                }

                if (format == "7Z")
                {
                    UpdateStatus("ファイル・フォルダをスキャン中...");
                    var filesToCompress = new System.Collections.Generic.Dictionary<string, string>();
                    foreach (var path in sourcePaths)
                    {
                        if (Directory.Exists(path))
                        {
                            AddDirectoryToDictionary(filesToCompress, path, path, Path.GetFileName(path));
                        }
                        else if (File.Exists(path))
                        {
                            string entryName = Path.GetFileName(path);
                            filesToCompress[entryName] = path;
                        }
                    }

                    UpdateStatus("7z圧縮中...");
                    var compressor = new SevenZipCompressor();
                    compressor.ArchiveFormat = OutArchiveFormat.SevenZip;
                    compressor.CompressionLevel = sevenZipLevel;
                    compressor.CompressionMethod = CompressionMethod.Lzma2;
                    compressor.FastCompression = true;

                    int maxThreads = Math.Max(2, Environment.ProcessorCount / 2);
                    string dictSize = "8m";

                    if (sevenZipLevel == SevenZip.CompressionLevel.Low)
                    {
                        dictSize = "4m";
                    }
                    else if (sevenZipLevel == SevenZip.CompressionLevel.High)
                    {
                        dictSize = "32m";
                        maxThreads = Math.Min(4, maxThreads); // 高圧縮時はメモリ制限のためスレッド数を絞る
                    }
                    else
                    {
                        dictSize = "8m";
                    }

                    compressor.CustomParameters.Add("d", dictSize);
                    compressor.CustomParameters.Add("mt", maxThreads.ToString());
                    compressor.CompressFileDictionary(filesToCompress, destZipPath);
                }
                else
                {
                    using (var fs = File.OpenWrite(destZipPath))
                    {
                        using (var writer = WriterFactory.OpenWriter(fs, archiveType, new WriterOptions(compressionType)))
                        {
                            foreach (var path in sourcePaths)
                            {
                                if (Directory.Exists(path))
                                {
                                    AddDirectoryToWriter(writer, path, path, Path.GetFileName(path));
                                }
                                else if (File.Exists(path))
                                {
                                    string entryName = Path.GetFileName(path);
                                    UpdateStatus($"圧縮中: {entryName}");
                                    writer.Write(entryName, path);
                                }
                            }
                        }
                    }
                }
            });

            UpdateStatus("圧縮が完了しました。");
        }

        private SevenZip.CompressionLevel PromptCompressionLevel()
        {
            using (var prompt = new Form())
            {
                prompt.Width = 400;
                prompt.Height = 210;
                prompt.Text = "7z 圧縮レベルの選択";
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

                var label = new Label() 
                { 
                    Left = 20, 
                    Top = 15, 
                    Width = 360, 
                    Height = 35,
                    Text = "7zの圧縮レベルを選択してください\n(高レベルほど高圧縮ですが、メモリと時間がかかります)" 
                };
                
                var radioLow = new RadioButton() { Left = 30, Top = 55, Width = 340, Text = "低 (高速・省メモリ - 辞書4MB)", Checked = false };
                var radioNormal = new RadioButton() { Left = 30, Top = 80, Width = 340, Text = "普通 (バランス - 辞書8MB)", Checked = true };
                var radioHigh = new RadioButton() { Left = 30, Top = 105, Width = 340, Text = "高 (高圧縮 - 辞書32MB - 最大4スレッド)", Checked = false };

                var buttonOk = new Button() { Text = "決定", Left = 280, Top = 135, Width = 80, DialogResult = DialogResult.OK };
                
                prompt.Controls.Add(label);
                prompt.Controls.Add(radioLow);
                prompt.Controls.Add(radioNormal);
                prompt.Controls.Add(radioHigh);
                prompt.Controls.Add(buttonOk);
                prompt.AcceptButton = buttonOk;

                buttonOk.Click += (sender, e) => { prompt.Close(); };

                prompt.ShowDialog();

                if (radioLow.Checked) return SevenZip.CompressionLevel.Low;
                if (radioHigh.Checked) return SevenZip.CompressionLevel.High;
                return SevenZip.CompressionLevel.Normal;
            }
        }

        private void AddDirectoryToDictionary(System.Collections.Generic.Dictionary<string, string> dict, string sourceRootDir, string currentDir, string archivePathPrefix)
        {
            // ディレクトリ内のファイルを追加
            foreach (var file in Directory.GetFiles(currentDir))
            {
                string relativePath = Path.GetRelativePath(sourceRootDir, file);
                string entryName = string.IsNullOrEmpty(archivePathPrefix)
                    ? relativePath
                    : Path.Combine(archivePathPrefix, relativePath);
                entryName = entryName.Replace('\\', '/');

                UpdateStatus($"圧縮中: {Path.GetFileName(file)}");
                dict[entryName] = file;
            }

            // 子ディレクトリを再帰追加
            foreach (var subDir in Directory.GetDirectories(currentDir))
            {
                string dirName = Path.GetFileName(subDir);
                string newPrefix = string.IsNullOrEmpty(archivePathPrefix) ? dirName : Path.Combine(archivePathPrefix, dirName);
                AddDirectoryToDictionary(dict, sourceRootDir, subDir, newPrefix);
            }
        }

        private void AddDirectoryToWriter(IWriter writer, string sourceRootDir, string currentDir, string archivePathPrefix)
        {
            // ディレクトリ内のファイルを追加
            foreach (var file in Directory.GetFiles(currentDir))
            {
                string relativePath = Path.GetRelativePath(sourceRootDir, file);
                string entryName = string.IsNullOrEmpty(archivePathPrefix)
                    ? relativePath
                    : Path.Combine(archivePathPrefix, relativePath);
                entryName = entryName.Replace('\\', '/');

                UpdateStatus($"圧縮中: {Path.GetFileName(file)}");
                writer.Write(entryName, file);
            }

            // 子ディレクトリを再帰追加
            foreach (var subDir in Directory.GetDirectories(currentDir))
            {
                string dirName = Path.GetFileName(subDir);
                string newPrefix = string.IsNullOrEmpty(archivePathPrefix) ? dirName : Path.Combine(archivePathPrefix, dirName);
                AddDirectoryToWriter(writer, sourceRootDir, subDir, newPrefix);
            }
        }

        private async Task DecompressArchiveAsync(string archiveFilePath, string destParentDir)
        {
            string archiveFileNameWithoutExt = Path.GetFileNameWithoutExtension(archiveFilePath);
            string destDirBase = Path.Combine(destParentDir, archiveFileNameWithoutExt);
            string destDir = destDirBase;

            // 重複チェック
            if (Directory.Exists(destDir))
            {
                DialogResult result = DialogResult.None;

                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        result = MessageBox.Show(
                            $"展開先フォルダが既に存在します。上書きしますか？\n\n対象: {archiveFileNameWithoutExt}\n\n「はい」：既存のフォルダを削除して上書き\n「いいえ」：別の名前で保存\n「キャンセル」：処理を中止",
                            "展開先の重複",
                            MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question
                        );
                    }));
                }
                else
                {
                    result = MessageBox.Show(
                        $"展開先フォルダが既に存在します。上書きしますか？\n\n対象: {archiveFileNameWithoutExt}\n\n「はい」：既存のフォルダを削除して上書き\n「いいえ」：別の名前で保存\n「キャンセル」：処理を中止",
                        "展開先の重複",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question
                    );
                }

                if (result == DialogResult.Yes)
                {
                    UpdateStatus("既存のフォルダを退避中...");
                    string tempGarbageDir = destDir + "_to_delete_" + Guid.NewGuid().ToString("N");

                    try
                    {
                        Directory.Move(destDir, tempGarbageDir);

                        // バックグラウンド削除
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                Directory.Delete(tempGarbageDir, true);
                            }
                            catch
                            {
                                // 握りつぶす
                            }
                        });
                    }
                    catch (Exception)
                    {
                        UpdateStatus("退避に失敗したため、直接削除中...");
                        await Task.Run(() =>
                        {
                            Directory.Delete(destDir, true);
                        });
                    }
                }
                else if (result == DialogResult.No)
                {
                    int index = 1;
                    while (Directory.Exists(destDir))
                    {
                        destDir = $"{destDirBase} ({index})";
                        index++;
                    }
                }
                else
                {
                    UpdateStatus("展開処理をキャンセルしました。");
                    return;
                }
            }

            UpdateStatus("展開処理を準備中...");

            // まず展開先フォルダを作成
            Directory.CreateDirectory(destDir);

            string ext = Path.GetExtension(archiveFilePath).ToLower();

            await Task.Run(() =>
            {
                if (ext == ".zip")
                {
                    // 1. ZIP形式: .NET標準クラスを使用して高速一括展開
                    System.IO.Compression.ZipFile.ExtractToDirectory(archiveFilePath, destDir, overwriteFiles: true);
                }
                else if (ext == ".7z")
                {
                    // 2. 7Z形式: 7z.dll (SevenZipExtractor) を使用してネイティブ超高速展開
                    using (var extractor = new SevenZip.SevenZipExtractor(archiveFilePath))
                    {
                        extractor.ExtractArchive(destDir);
                    }
                }
                else
                {
                    // 3. その他 (TAR, TGZ, RAR等): SharpCompress の IReader を使って順次ストリーム読み込み (O(N^2)再シーク回避)
                    int ansiCodePage = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                    var encoding = System.Text.Encoding.GetEncoding(ansiCodePage);
                    var options = new ReaderOptions
                    {
                        ArchiveEncoding = new ArchiveEncoding { Default = encoding }
                    };

                    using (Stream stream = File.OpenRead(archiveFilePath))
                    {
                        using (var reader = ReaderFactory.OpenReader(stream, options))
                        {
                            while (reader.MoveToNextEntry())
                            {
                                if (!reader.Entry.IsDirectory)
                                {
                                    reader.WriteEntryToDirectory(destDir, new ExtractionOptions
                                    {
                                        ExtractFullPath = true,
                                        Overwrite = true
                                    });
                                }
                            }
                        }
                    }
                }
            });
        }

        private async Task DecompressMultipleArchivesAsync(string[] archiveFilePaths)
        {
            string firstFileDir = Path.GetDirectoryName(archiveFilePaths[0]) ?? string.Empty;
            UpdateStatus("展開先を選択中...");

            string selectedParentDir = string.Empty;

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    using (var dialog = new FolderBrowserDialog())
                    {
                        dialog.Description = "すべてのアーカイブファイルの解凍先親フォルダを選択してください";
                        dialog.InitialDirectory = firstFileDir;
                        dialog.SelectedPath = firstFileDir;

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            selectedParentDir = dialog.SelectedPath;
                        }
                    }
                }));
            }
            else
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "すべてのアーカイブファイルの解凍先親フォルダを選択してください";
                    dialog.InitialDirectory = firstFileDir;
                    dialog.SelectedPath = firstFileDir;

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        selectedParentDir = dialog.SelectedPath;
                    }
                }
            }

            if (string.IsNullOrEmpty(selectedParentDir))
            {
                UpdateStatus("展開をキャンセルしました。");
                return;
            }

            int totalFiles = archiveFilePaths.Length;
            for (int i = 0; i < totalFiles; i++)
            {
                string filePath = archiveFilePaths[i];
                UpdateStatus($"[{i + 1}/{totalFiles}] 展開中: {Path.GetFileName(filePath)}...");

                try
                {
                    await DecompressArchiveAsync(filePath, selectedParentDir);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"エラー ({Path.GetFileName(filePath)}): {ex.Message}");
                    MessageBox.Show($"展開中にエラーが発生しました: {Path.GetFileName(filePath)}\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            UpdateStatus("すべての展開処理が完了しました。");

            // 完了後の親フォルダオープン確認
            DialogResult openFolderResult = DialogResult.None;

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    openFolderResult = MessageBox.Show(
                        "すべての展開が完了しました。展開先フォルダを開きますか？",
                        "完了",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );
                }));
            }
            else
            {
                openFolderResult = MessageBox.Show(
                    "すべての展開が完了しました。展開先フォルダを開きますか？",
                    "完了",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
            }

            if (openFolderResult == DialogResult.Yes)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = selectedParentDir,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    UpdateStatus($"フォルダを開く際にエラーが発生しました: {ex.Message}");
                }
            }
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }

        private void progressBar1_Click(object sender, EventArgs e)
        {
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
        }
    }
}
