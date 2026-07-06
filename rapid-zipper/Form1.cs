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
        private string[]? _startupArgs;

        public RapidZipper(string[]? args = null)
        {
            InitializeComponent();
            try
            {
                this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                // アイコンの抽出に失敗した場合はデフォルトアイコンのままにする
            }
            InitializeFormatComboBox();
            InitializeEncodingComboBox();
            _startupArgs = args;

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

                // 2. 埋め込みリソースから 7z.dll を Unicode を含まない Temp フォルダに展開してロード
                // (SevenZipSharpのUnicodeパスバグ回避策 & ポータブルなシングルファイル化 & VULN-001のDLL上書き脆弱性防止)
                string resourceName = Environment.Is64BitProcess 
                    ? "rapid_zipper.Resources.x64.7z.dll" 
                    : "rapid_zipper.Resources.x86.7z.dll";

                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream? resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream != null)
                    {
                        string randomFolderName = "RapidZipper_7z_" + Guid.NewGuid().ToString("N");
                        string tempDir = Path.Combine(tempParent, randomFolderName, Environment.Is64BitProcess ? "x64" : "x86");
                        Directory.CreateDirectory(tempDir);
                        string destDllPath = Path.Combine(tempDir, "7z.dll");

                        // 埋め込みリソースのデータを一時ファイルに書き出す
                        using (FileStream fileStream = new FileStream(destDllPath, FileMode.Create, FileAccess.Write))
                        {
                            resourceStream.CopyTo(fileStream);
                        }

                        SevenZipBase.SetLibraryPath(destDllPath);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"警告: 埋め込みリソース {resourceName} が見つかりません。");
                    }
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

        private void InitializeEncodingComboBox()
        {
            EncodingComboBox.Items.Clear();
            EncodingComboBox.Items.Add("Auto-detect / 自動判定");
            EncodingComboBox.Items.Add("UTF-8");
            EncodingComboBox.Items.Add("Shift-JIS (Japanese)");
            EncodingComboBox.Items.Add("GB2312 (Simplified Chinese)");
            EncodingComboBox.Items.Add("Big5 (Traditional Chinese)");
            EncodingComboBox.Items.Add("EUC-KR (Korean)");
            EncodingComboBox.SelectedIndex = 0;
        }

        private System.Text.Encoding? GetSelectedEncoding()
        {
            int index = 0;
            if (InvokeRequired)
            {
                Invoke(new Action(() => index = EncodingComboBox.SelectedIndex));
            }
            else
            {
                index = EncodingComboBox.SelectedIndex;
            }

            switch (index)
            {
                case 1: return System.Text.Encoding.UTF8;
                case 2: return System.Text.Encoding.GetEncoding(932); // Shift-JIS
                case 3: return System.Text.Encoding.GetEncoding(936); // GB2312
                case 4: return System.Text.Encoding.GetEncoding(950); // Big5
                case 5: return System.Text.Encoding.GetEncoding(949); // EUC-KR
                default: return null; // 自動判定
            }
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
                await ProcessPathsAsync(paths);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error occurred: {ex.Message} / エラーが発生しました: {ex.Message}");
            }
            finally
            {
                SetUIProcessing(false);
            }
        }

        private async void RapidZipper_Shown(object sender, EventArgs e)
        {
            if (_startupArgs != null && _startupArgs.Length > 0)
            {
                try
                {
                    SetUIProcessing(true);
                    await ProcessPathsAsync(_startupArgs);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error occurred: {ex.Message} / エラーが発生しました: {ex.Message}",
                        "Error / エラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
                finally
                {
                    SetUIProcessing(false);
                    // 起動引数で動かした場合は、処理後に自動でアプリを閉じる
                    Application.Exit();
                }
            }
        }

        private async Task ProcessPathsAsync(string[] paths)
        {
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
                                dialog.Description = "Select destination folder / 解凍先フォルダを選択してください";
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
                            dialog.Description = "Select destination folder / 解凍先フォルダを選択してください";
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
                            "Extraction completed. Open the folder?\r\n展開が完了しました。フォルダを開きますか？",
                            "Completed / 完了",
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
                        UpdateStatus("Extraction cancelled. / 展開処理をキャンセルしました。");
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
                            sfd.Filter = $"Compressed File (*{ext})|*{ext}";
                            sfd.InitialDirectory = defaultDir;
                            sfd.FileName = defaultZipName;
                            sfd.Title = "Select save destination for compressed file / 圧縮ファイルの保存先を選択してください";

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
                        sfd.Filter = $"Compressed File (*{ext})|*{ext}";
                        sfd.InitialDirectory = defaultDir;
                        sfd.FileName = defaultZipName;
                        sfd.Title = "Select save destination for compressed file / 圧縮ファイルの保存先を選択してください";

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
                    UpdateStatus("Compression cancelled. / 圧縮処理をキャンセルしました。");
                }
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
            UpdateStatus($"Compressing: {folderName}{ext} / 圧縮中");

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
                    UpdateStatus("Scanning folder... / フォルダ内をスキャン中...");
                    var filesToCompress = new System.Collections.Generic.Dictionary<string, string>();
                    AddDirectoryToDictionary(filesToCompress, folderPath, folderPath, string.Empty);

                    UpdateStatus("7z compressing... / 7z圧縮中...");
                    var compressor = ConfigureSevenZipCompressor(sevenZipLevel);
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

            UpdateStatus("Compression completed. / 圧縮が完了しました。");
        }

        private async Task CompressMultipleItemsAsync(string[] sourcePaths, string destZipPath, string format, SevenZip.CompressionLevel sevenZipLevel = SevenZip.CompressionLevel.Normal)
        {
            UpdateStatus("Preparing compression... / 圧縮処理を準備中...");

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
                    UpdateStatus("Scanning files/folders... / ファイル・フォルダをスキャン中...");
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

                    UpdateStatus("7z compressing... / 7z圧縮中...");
                    var compressor = ConfigureSevenZipCompressor(sevenZipLevel);
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
                                    UpdateStatus($"Compressing: {entryName} / 圧縮中: {entryName}");
                                    writer.Write(entryName, path);
                                }
                            }
                        }
                    }
                }
            });

            UpdateStatus("Compression completed. / 圧縮が完了しました。");
        }

        private SevenZip.CompressionLevel PromptCompressionLevel()
        {
            using (var prompt = new Form())
            {
                prompt.Width = 400;
                prompt.Height = 230;
                prompt.Text = "Select 7z Compression Level / 7z 圧縮レベルの選択";
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

                var label = new Label()
                {
                    Left = 20,
                    Top = 15,
                    Width = 360,
                    Height = 50,
                    Text = "Select 7z compression level / 7zの圧縮レベルを選択してください\r\n(Higher level is more compressed but takes more memory and time)"
                };

                var radioLow = new RadioButton() { Left = 30, Top = 70, Width = 340, Text = "Low / 低 (Fast / High Speed - Dictionary 4MB)", Checked = false };
                var radioNormal = new RadioButton() { Left = 30, Top = 95, Width = 340, Text = "Normal / 普通 (Balanced - Dictionary 8MB)", Checked = true };
                var radioHigh = new RadioButton() { Left = 30, Top = 120, Width = 340, Text = "High / 高 (High Compression - Dictionary 32MB)", Checked = false };

                var buttonOk = new Button() { Text = "OK / 決定", Left = 280, Top = 155, Width = 80, DialogResult = DialogResult.OK };

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

        private SevenZipCompressor ConfigureSevenZipCompressor(SevenZip.CompressionLevel level)
        {
            var compressor = new SevenZipCompressor();
            compressor.ArchiveFormat = OutArchiveFormat.SevenZip;
            compressor.CompressionLevel = level;
            compressor.CompressionMethod = CompressionMethod.Lzma2;
            compressor.FastCompression = true;

            int maxThreads = Math.Max(2, Environment.ProcessorCount / 2);
            string dictSize = "8m";

            if (level == SevenZip.CompressionLevel.Low)
            {
                dictSize = "4m";
            }
            else if (level == SevenZip.CompressionLevel.High)
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
            return compressor;
        }

        private bool IsProtectedDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // ドライブのルートディレクトリ（例: C:\ など）は保護する
                string root = Path.GetPathRoot(fullPath) ?? string.Empty;
                if (fullPath.Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // 保護対象の特別フォルダリスト
                var protectedFolders = new System.Collections.Generic.List<string>
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), // Documents
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),   // C:\Users\Username
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),       // C:\Windows
                    Environment.GetFolderPath(Environment.SpecialFolder.System),        // C:\Windows\System32
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),  // C:\Program Files
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // Roaming AppData
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), // Local AppData
                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                };

                foreach (var folder in protectedFolders)
                {
                    if (string.IsNullOrEmpty(folder)) continue;

                    string fullFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    // パスが完全に一致するか、システムフォルダの直上の親フォルダになっていないかをチェック
                    if (fullPath.Equals(fullFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // パス解析に失敗した場合は安全のため保護対象とする
                return true;
            }

            return false;
        }

        private void AddDirectoryToDictionary(System.Collections.Generic.Dictionary<string, string> dict, string sourceRootDir, string currentDir, string archivePathPrefix)
        {
            // ディレクトリ内のファイルを追加
            foreach (var file in Directory.GetFiles(currentDir))
            {
                // シンボリックリンク/再解析ポイントは除外
                if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

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
                // シンボリックリンク/ジャンクション/再解析ポイントは除外
                if (File.GetAttributes(subDir).HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

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
                // シンボリックリンク/再解析ポイントは除外
                if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

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
                // シンボリックリンク/ジャンクション/再解析ポイントは除外
                if (File.GetAttributes(subDir).HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                string dirName = Path.GetFileName(subDir);
                string newPrefix = string.IsNullOrEmpty(archivePathPrefix) ? dirName : Path.Combine(archivePathPrefix, dirName);
                AddDirectoryToWriter(writer, sourceRootDir, subDir, newPrefix);
            }
        }

        private async Task DecompressArchiveAsync(string archiveFilePath, string destParentDir)
        {
            const long MaxUncompressedSizeLimit = 100L * 1024 * 1024 * 1024; // 100 GB
            const int MaxFileCountLimit = 500000; // 500,000 ファイル

            // 展開に使用するエンコーディングの決定
            System.Text.Encoding selectedEncoding = System.Text.Encoding.UTF8;
            var targetEncoding = GetSelectedEncoding();
            if (targetEncoding != null)
            {
                selectedEncoding = targetEncoding;
            }
            else
            {
                // 自動判定
                selectedEncoding = DetectEncoding(archiveFilePath);
            }

            string archiveFileNameWithoutExt = Path.GetFileNameWithoutExtension(archiveFilePath);
            string destDirBase = Path.Combine(destParentDir, archiveFileNameWithoutExt);
            string destDir = destDirBase;

            // 1. システムディレクトリの保護チェック
            if (IsProtectedDirectory(destDir))
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        MessageBox.Show(
                            $"The specified extraction folder is protected and cannot be overwritten or deleted.\n指定された展開先フォルダはシステム保護対象のため、上書き・削除できません。\n\nPath: {destDir}",
                            "Security Warning / セキュリティ警告",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    }));
                }
                else
                {
                    MessageBox.Show(
                        $"The specified extraction folder is protected and cannot be overwritten or deleted.\n指定された展開先フォルダはシステム保護対象のため、上書き・削除できません。\n\nPath: {destDir}",
                        "Security Warning / セキュリティ警告",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
                return;
            }

            bool isOverwriting = false;
            string realDestDir = destDir;
            string extractionTargetDir = destDir;

            // 2. 重複チェック
            if (Directory.Exists(destDir))
            {
                DialogResult result = DialogResult.None;

                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        result = MessageBox.Show(
                            $"The destination folder already exists. Overwrite it?\n展開先フォルダが既に存在します。上書きしますか？\n\nTarget: {archiveFileNameWithoutExt}\n\n[Yes]: Overwrite existing (safe replacement after success) / はい: 上書き\n[No]: Save as a different name / いいえ: 別の名前で保存\n[Cancel]: Cancel process / キャンセル: 中止",
                            "Destination Duplicate / 展開先の重複",
                            MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question
                        );
                    }));
                }
                else
                {
                    result = MessageBox.Show(
                        $"The destination folder already exists. Overwrite it?\n展開先フォルダが既に存在します。上書きしますか？\n\nTarget: {archiveFileNameWithoutExt}\n\n[Yes]: Overwrite existing (safe replacement after success) / はい: 上書き\n[No]: Save as a different name / いいえ: 別の名前で保存\n[Cancel]: Cancel process / キャンセル: 中止",
                        "Destination Duplicate / 展開先の重複",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question
                    );
                }

                if (result == DialogResult.Yes)
                {
                    isOverwriting = true;
                    // 一時フォルダに展開して、成功後に置換する (ロールバック安全策)
                    extractionTargetDir = destDir + "_temp_" + Guid.NewGuid().ToString("N");
                }
                else if (result == DialogResult.No)
                {
                    int index = 1;
                    while (Directory.Exists(destDir))
                    {
                        destDir = $"{destDirBase} ({index})";
                        index++;
                    }
                    realDestDir = destDir;
                    extractionTargetDir = destDir;
                }
                else
                {
                    UpdateStatus("Extraction cancelled. / 展開処理をキャンセルしました。");
                    return;
                }
            }

            string encodingName = selectedEncoding.WebName.ToUpper();
            UpdateStatus($"Preparing extraction [{encodingName}]... / 展開処理を準備中 ({encodingName})...");

            try
            {
                // まず展開先フォルダを作成
                Directory.CreateDirectory(extractionTargetDir);

                string ext = Path.GetExtension(archiveFilePath).ToLower();

                await Task.Run(() =>
                {
                    if (ext == ".zip")
                    {
                        // 1. ZIP形式: ZipArchive を開いて Zip Slip 防止と容量チェックを行いながら展開
                        using (var archive = System.IO.Compression.ZipFile.Open(archiveFilePath, ZipArchiveMode.Read, selectedEncoding))
                        {
                            long totalSizeEstimate = 0;
                            int fileCountEstimate = 0;
                            foreach (var entry in archive.Entries)
                            {
                                if (!string.IsNullOrEmpty(entry.Name))
                                {
                                    fileCountEstimate++;
                                    totalSizeEstimate += entry.Length;
                                }
                            }

                            if (fileCountEstimate > MaxFileCountLimit || totalSizeEstimate > MaxUncompressedSizeLimit)
                            {
                                throw new InvalidOperationException($"Extraction limits exceeded. / 展開制限を超えています。\nEstimated Size: {totalSizeEstimate / 1024 / 1024}MB (Limit: {MaxUncompressedSizeLimit / 1024 / 1024}MB) / 解凍後推定サイズ\nFile Count: {fileCountEstimate} (Limit: {MaxFileCountLimit}) / ファイル数");
                            }

                            long currentTotalWritten = 0;
                            foreach (var entry in archive.Entries)
                            {
                                if (string.IsNullOrEmpty(entry.Name)) continue;

                                // 相対パスのサニタイズ（Windows禁止文字の除去）
                                string sanitizedRelative = SanitizeRelativePathForWindows(entry.FullName);

                                // Path Traversal (Zip Slip) 防止のパス正規化検証
                                string entryFullPath = Path.GetFullPath(Path.Combine(extractionTargetDir, sanitizedRelative));
                                string targetDirFullPath = Path.GetFullPath(extractionTargetDir) + Path.DirectorySeparatorChar;

                                if (!entryFullPath.StartsWith(targetDirFullPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Security Warning: Zip Slip path detected and skipped: {entry.FullName} / セキュリティ警告");
                                    continue;
                                }

                                string? parentDir = Path.GetDirectoryName(entryFullPath);
                                if (parentDir != null && !Directory.Exists(parentDir))
                                {
                                    Directory.CreateDirectory(parentDir);
                                }

                                entry.ExtractToFile(entryFullPath, overwrite: true);
                                currentTotalWritten += entry.Length;

                                if (currentTotalWritten > MaxUncompressedSizeLimit)
                                {
                                    throw new InvalidOperationException("Extraction size limit (100GB) exceeded. / 解凍サイズ制限（100GB）を超えました。");
                                }
                            }
                        }
                    }
                    else if (ext == ".7z")
                    {
                        // 2. 7Z形式: 7z.dll (SevenZipExtractor)
                        using (var extractor = new SevenZip.SevenZipExtractor(archiveFilePath))
                        {
                            long totalSizeEstimate = 0;
                            int fileCountEstimate = 0;
                            foreach (var fileInfo in extractor.ArchiveFileData)
                            {
                                if (!fileInfo.IsDirectory)
                                {
                                    fileCountEstimate++;
                                    totalSizeEstimate += (long)fileInfo.Size;
                                }
                            }

                            if (fileCountEstimate > MaxFileCountLimit || totalSizeEstimate > MaxUncompressedSizeLimit)
                            {
                                throw new InvalidOperationException($"Extraction limits exceeded. / 展開制限を超えています。\nEstimated Size: {totalSizeEstimate / 1024 / 1024}MB (Limit: {MaxUncompressedSizeLimit / 1024 / 1024}MB) / 解凍後推定サイズ\nFile Count: {fileCountEstimate} (Limit: {MaxFileCountLimit}) / ファイル数");
                            }

                            // 7z の Zip Slip パス検証
                            string targetDirFullPath = Path.GetFullPath(extractionTargetDir) + Path.DirectorySeparatorChar;
                            foreach (var fileInfo in extractor.ArchiveFileData)
                            {
                                if (string.IsNullOrEmpty(fileInfo.FileName)) continue;
                                string entryFullPath = Path.GetFullPath(Path.Combine(extractionTargetDir, fileInfo.FileName));
                                if (!entryFullPath.StartsWith(targetDirFullPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    throw new InvalidOperationException($"Security Warning: Zip Slip path detected: {fileInfo.FileName} / セキュリティ警告");
                                }
                            }

                            extractor.ExtractArchive(extractionTargetDir);
                        }
                    }
                    else
                    {
                        // 3. その他 (TAR, TGZ, RAR等): SharpCompress の IReader
                        var options = new ReaderOptions
                        {
                            ArchiveEncoding = new ArchiveEncoding { Default = selectedEncoding }
                        };

                        using (Stream stream = File.OpenRead(archiveFilePath))
                        {
                            using (var reader = ReaderFactory.OpenReader(stream, options))
                            {
                                // 事前チェック
                                using (Stream checkStream = File.OpenRead(archiveFilePath))
                                using (var checkArchive = SharpCompress.Archives.ArchiveFactory.OpenArchive(checkStream))
                                {
                                    long totalSizeEstimate = 0;
                                    int fileCountEstimate = 0;
                                    foreach (var entry in checkArchive.Entries)
                                    {
                                        if (!entry.IsDirectory)
                                        {
                                            fileCountEstimate++;
                                            totalSizeEstimate += entry.Size;
                                        }
                                    }

                                    if (fileCountEstimate > MaxFileCountLimit || totalSizeEstimate > MaxUncompressedSizeLimit)
                                    {
                                        throw new InvalidOperationException($"Extraction limits exceeded. / 展開制限を超えています。\nEstimated Size: {totalSizeEstimate / 1024 / 1024}MB (Limit: {MaxUncompressedSizeLimit / 1024 / 1024}MB) / 解凍後推定サイズ\nFile Count: {fileCountEstimate} (Limit: {MaxFileCountLimit}) / ファイル数");
                                    }
                                }

                                long currentTotalWritten = 0;
                                while (reader.MoveToNextEntry())
                                {
                                    if (reader.Entry.IsDirectory) continue;

                                    // 相対パスのサニタイズ（Windows禁止文字の除去）
                                    string sanitizedRelative = SanitizeRelativePathForWindows(reader.Entry.Key ?? string.Empty);

                                    // Path Traversal (Zip Slip) 防止のパス正規化検証
                                    string entryFullPath = Path.GetFullPath(Path.Combine(extractionTargetDir, sanitizedRelative));
                                    string targetDirFullPath = Path.GetFullPath(extractionTargetDir) + Path.DirectorySeparatorChar;

                                    if (!entryFullPath.StartsWith(targetDirFullPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Security Warning: Zip Slip path detected and skipped: {reader.Entry.Key} / セキュリティ警告");
                                        continue;
                                    }

                                    string? parentDir = Path.GetDirectoryName(entryFullPath);
                                    if (parentDir != null && !Directory.Exists(parentDir))
                                    {
                                        Directory.CreateDirectory(parentDir);
                                    }

                                    // 自前ファイル書き出しによるサニタイズ適用
                                    using (var entryStream = reader.OpenEntryStream())
                                    using (var fs = File.Create(entryFullPath))
                                    {
                                        entryStream.CopyTo(fs);
                                    }

                                    currentTotalWritten += reader.Entry.Size;
                                    if (currentTotalWritten > MaxUncompressedSizeLimit)
                                    {
                                        throw new InvalidOperationException("Extraction size limit (100GB) exceeded. / 解凍サイズ制限（100GB）を超えました。");
                                    }
                                }
                            }
                        }
                    }
                });

                // 3. 正常に展開完了した後の安全なリプレース（成功後置換）
                if (isOverwriting)
                {
                    UpdateStatus("Replacing existing folder... / 既存のフォルダを置換中...");
                    string tempBackupDir = realDestDir + "_backup_" + Guid.NewGuid().ToString("N");
                    
                    try
                    {
                        // 既存のフォルダをバックアップ名にリネーム
                        Directory.Move(realDestDir, tempBackupDir);
                        // 一時フォルダを正式名にリネーム
                        Directory.Move(extractionTargetDir, realDestDir);

                        // 古いフォルダを非同期で完全削除
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                Directory.Delete(tempBackupDir, true);
                            }
                            catch
                            {
                                // 握りつぶす
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus("Replacement failed. Cleaning up temp files... / 置換に失敗しました。一時ファイルをクリーンアップ中...");
                        try { Directory.Delete(extractionTargetDir, true); } catch { }
                        throw new IOException($"Failed to replace existing folder: {ex.Message} / 既存フォルダの置換に失敗しました: {ex.Message}");
                    }
                }

                UpdateStatus("Extraction completed. / 展開が完了しました。");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Extraction failed: {ex.Message} / 展開に失敗しました: {ex.Message}");
                // 途中で失敗した一時ファイルをクリーンアップ
                if (isOverwriting && Directory.Exists(extractionTargetDir))
                {
                    try { Directory.Delete(extractionTargetDir, true); } catch { }
                }

                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        MessageBox.Show(
                            $"An error occurred during extraction. / 展開中にエラーが発生しました。\n\nDetail: {ex.Message} / 詳細",
                            "Extraction Error / 展開エラー",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    }));
                }
                else
                {
                    MessageBox.Show(
                        $"An error occurred during extraction. / 展開中にエラーが発生しました。\n\nDetail: {ex.Message} / 詳細",
                        "Extraction Error / 展開エラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }

        private async Task DecompressMultipleArchivesAsync(string[] archiveFilePaths)
        {
            string firstFileDir = Path.GetDirectoryName(archiveFilePaths[0]) ?? string.Empty;
            UpdateStatus("Selecting destination folder... / 展開先を選択中...");

            string selectedParentDir = string.Empty;

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    using (var dialog = new FolderBrowserDialog())
                    {
                        dialog.Description = "Select destination parent folder for all archives / すべてのアーカイブファイルの解凍先親フォルダを選択してください";
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
                    dialog.Description = "Select destination parent folder for all archives / すべてのアーカイブファイルの解凍先親フォルダを選択してください";
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
                UpdateStatus("Extraction cancelled. / 展開をキャンセルしました。");
                return;
            }

            int totalFiles = archiveFilePaths.Length;
            for (int i = 0; i < totalFiles; i++)
            {
                string filePath = archiveFilePaths[i];
                UpdateStatus($"[{i + 1}/{totalFiles}] Extracting: {Path.GetFileName(filePath)}... / 展開中");

                try
                {
                    await DecompressArchiveAsync(filePath, selectedParentDir);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error ({Path.GetFileName(filePath)}): {ex.Message} / エラー");
                    MessageBox.Show($"An error occurred during extraction of {Path.GetFileName(filePath)}:\n{ex.Message} / 展開中にエラーが発生しました", "Error / エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            UpdateStatus("All extractions completed. / すべての展開処理が完了しました。");

            // 完了後の親フォルダオープン確認
            DialogResult openFolderResult = DialogResult.None;

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    openFolderResult = MessageBox.Show(
                        "All extractions completed. Open the destination folder?\nすべての展開が完了しました。展開先フォルダを開きますか？",
                        "Completed / 完了",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );
                }));
            }
            else
            {
                openFolderResult = MessageBox.Show(
                    "All extractions completed. Open the destination folder?\nすべての展開が完了しました。展開先フォルダを開きますか？",
                    "Completed / 完了",
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
                    UpdateStatus($"Error opening folder: {ex.Message} / フォルダを開く際にエラーが発生しました");
                }
            }
        }

        private static byte[] GetFileNameBytesFromZip(string zipPath, int maxEntries = 15)
        {
            var allBytes = new System.Collections.Generic.List<byte>();
            try
            {
                using (var fs = File.OpenRead(zipPath))
                {
                    if (fs.Length < 22) return allBytes.ToArray();
                    
                    long eocdOffset = -1;
                    long maxRead = Math.Min(fs.Length, 65557);
                    fs.Seek(-maxRead, SeekOrigin.End);
                    byte[] buffer = new byte[maxRead];
                    fs.ReadExactly(buffer, 0, buffer.Length);
                    
                    for (int i = buffer.Length - 22; i >= 0; i--)
                    {
                        if (buffer[i] == 0x50 && buffer[i + 1] == 0x4B && buffer[i + 2] == 0x05 && buffer[i + 3] == 0x06)
                        {
                            eocdOffset = fs.Length - maxRead + i;
                            break;
                        }
                    }
                    
                    if (eocdOffset == -1) return allBytes.ToArray();
                    
                    fs.Seek(eocdOffset + 12, SeekOrigin.Begin);
                    using (var br = new BinaryReader(fs, System.Text.Encoding.ASCII, true))
                    {
                        uint cdSize = br.ReadUInt32();
                        uint cdOffset = br.ReadUInt32();
                        
                        if (cdOffset + cdSize > fs.Length) return allBytes.ToArray();
                        
                        fs.Seek(cdOffset, SeekOrigin.Begin);
                        int entriesCount = 0;
                        while (fs.Position + 46 <= cdOffset + cdSize)
                        {
                            uint sig = br.ReadUInt32();
                            if (sig != 0x02014b50) // PK\x01\x02
                            {
                                break;
                            }
                            
                            fs.Seek(24, SeekOrigin.Current);
                            ushort fileNameLen = br.ReadUInt16();
                            ushort extraFieldLen = br.ReadUInt16();
                            ushort fileCommentLen = br.ReadUInt16();
                            
                            fs.Seek(12, SeekOrigin.Current);
                            
                            if (fileNameLen > 0 && fs.Position + fileNameLen <= fs.Length)
                            {
                                byte[] nameBytes = br.ReadBytes(fileNameLen);
                                allBytes.AddRange(nameBytes);
                                entriesCount++;
                                if (entriesCount >= maxEntries) break;
                            }
                            
                            fs.Seek(extraFieldLen + fileCommentLen, SeekOrigin.Current);
                        }
                    }
                }
            }
            catch
            {
                // エラー時は部分取得データを使用
            }
            return allBytes.ToArray();
        }

        private static System.Text.Encoding DetectEncoding(string zipPath)
        {
            byte[] bytes = GetFileNameBytesFromZip(zipPath);
            if (bytes == null || bytes.Length == 0)
            {
                return System.Text.Encoding.UTF8;
            }

            try
            {
                var utf8Strict = new System.Text.UTF8Encoding(false, true);
                utf8Strict.GetString(bytes);
                return System.Text.Encoding.UTF8;
            }
            catch (ArgumentException)
            {
                // UTF-8ではない
            }

            if (IsValidShiftJIS(bytes))
            {
                return System.Text.Encoding.GetEncoding(932);
            }
            if (IsValidGB2312(bytes))
            {
                return System.Text.Encoding.GetEncoding(936);
            }
            if (IsValidBig5(bytes))
            {
                return System.Text.Encoding.GetEncoding(950);
            }
            if (IsValidEucKr(bytes))
            {
                return System.Text.Encoding.GetEncoding(949);
            }

            return System.Text.Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        }

        private static bool IsValidShiftJIS(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte b1 = bytes[i];
                if (b1 <= 0x7F)
                {
                    i++;
                    continue;
                }
                if (b1 >= 0xA1 && b1 <= 0xDF)
                {
                    i++;
                    continue;
                }
                if ((b1 >= 0x81 && b1 <= 0x9F) || (b1 >= 0xE0 && b1 <= 0xFC))
                {
                    if (i + 1 >= bytes.Length) return false;
                    byte b2 = bytes[i + 1];
                    if ((b2 >= 0x40 && b2 <= 0x7E) || (b2 >= 0x80 && b2 <= 0xFC))
                    {
                        i += 2;
                        continue;
                    }
                    return false;
                }
                return false;
            }
            return true;
        }

        private static bool IsValidGB2312(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte b1 = bytes[i];
                if (b1 <= 0x7F)
                {
                    i++;
                    continue;
                }
                if (b1 >= 0x81 && b1 <= 0xFE)
                {
                    if (i + 1 >= bytes.Length) return false;
                    byte b2 = bytes[i + 1];
                    if ((b2 >= 0x40 && b2 <= 0x7E) || (b2 >= 0x80 && b2 <= 0xFE))
                    {
                        i += 2;
                        continue;
                    }
                    return false;
                }
                return false;
            }
            return true;
        }

        private static bool IsValidBig5(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte b1 = bytes[i];
                if (b1 <= 0x7F)
                {
                    i++;
                    continue;
                }
                if (b1 >= 0x81 && b1 <= 0xFE)
                {
                    if (i + 1 >= bytes.Length) return false;
                    byte b2 = bytes[i + 1];
                    if ((b2 >= 0x40 && b2 <= 0x7E) || (b2 >= 0xA1 && b2 <= 0xFE))
                    {
                        i += 2;
                        continue;
                    }
                    return false;
                }
                return false;
            }
            return true;
        }

        private static bool IsValidEucKr(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte b1 = bytes[i];
                if (b1 <= 0x7F)
                {
                    i++;
                    continue;
                }
                if (b1 >= 0x81 && b1 <= 0xFE)
                {
                    if (i + 1 >= bytes.Length) return false;
                    byte b2 = bytes[i + 1];
                    if ((b2 >= 0x41 && b2 <= 0x5A) || (b2 >= 0x61 && b2 <= 0x7A) || (b2 >= 0x81 && b2 <= 0xFE))
                    {
                        i += 2;
                        continue;
                    }
                    return false;
                }
                return false;
            }
            return true;
        }

        private static string SanitizeRelativePathForWindows(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return "_";

            var parts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var invalidNameChars = Path.GetInvalidFileNameChars();
            var reserved = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CON","PRN","AUX","NUL",
                "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
                "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
            };

            for (int i = 0; i < parts.Length; i++)
            {
                var sb = new System.Text.StringBuilder(parts[i].Length);
                foreach (char c in parts[i])
                {
                    // Windows で使用禁止の文字を '_' に置換
                    if (System.Array.IndexOf(invalidNameChars, c) >= 0 || c == ':' || c == '|')
                    {
                        sb.Append('_');
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                var sanitized = sb.ToString().TrimEnd(' ', '.');
                if (string.IsNullOrEmpty(sanitized)) sanitized = "_";
                if (reserved.Contains(sanitized)) sanitized += "_";
                parts[i] = sanitized;
            }

            return Path.Combine(parts);
        }
    }
}
