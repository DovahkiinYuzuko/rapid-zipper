using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace rapid_zipper
{
    public partial class RapidZipper : Form
    {
        public RapidZipper()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
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

        private async void RapidZipper_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;

            try
            {
                SetUIProcessing(true);

                // 単一のフォルダがドロップされた場合：自動圧縮
                if (paths.Length == 1 && Directory.Exists(paths[0]))
                {
                    await CompressFolderAsync(paths[0]);
                }
                // 単一のZIPファイルがドロップされた場合：並列展開
                else if (paths.Length == 1 && File.Exists(paths[0]) && Path.GetExtension(paths[0]).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    await DecompressZipAsync(paths[0]);
                }
                // それ以外（単一ファイル、複数ファイル、複数フォルダ、混在など）の場合：まとめて圧縮
                else
                {
                    string defaultDir = Path.GetDirectoryName(paths[0]) ?? string.Empty;
                    string defaultZipName = "archive.zip";
                    if (paths.Length == 1)
                    {
                        defaultZipName = Path.GetFileNameWithoutExtension(paths[0]) + ".zip";
                    }

                    string destZipPath = string.Empty;

                    if (InvokeRequired)
                    {
                        Invoke(new Action(() =>
                        {
                            using (var sfd = new SaveFileDialog())
                            {
                                sfd.Filter = "ZIPファイル (*.zip)|*.zip";
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
                            sfd.Filter = "ZIPファイル (*.zip)|*.zip";
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
                        await CompressMultipleItemsAsync(paths, destZipPath);
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

        private async Task CompressFolderAsync(string folderPath)
        {
            string parentDir = Path.GetDirectoryName(folderPath) ?? string.Empty;
            string folderName = Path.GetFileName(folderPath);
            string destZipPath = Path.Combine(parentDir, folderName + ".zip");

            UpdateStatus($"圧縮中: {folderName}.zip");

            await Task.Run(() =>
            {
                if (File.Exists(destZipPath))
                {
                    File.Delete(destZipPath);
                }
                ZipFile.CreateFromDirectory(
                    folderPath,
                    destZipPath,
                    CompressionLevel.Fastest,
                    includeBaseDirectory: false
                );
            });

            UpdateStatus("圧縮が完了しました。");
        }

        private async Task CompressMultipleItemsAsync(string[] sourcePaths, string destZipPath)
        {
            UpdateStatus("圧縮処理を準備中...");

            await Task.Run(() =>
            {
                if (File.Exists(destZipPath))
                {
                    File.Delete(destZipPath);
                }

                int ansiCodePage = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                var encoding = System.Text.Encoding.GetEncoding(ansiCodePage);

                using (var archive = ZipFile.Open(destZipPath, ZipArchiveMode.Create, encoding))
                {
                    foreach (var path in sourcePaths)
                    {
                        if (Directory.Exists(path))
                        {
                            AddDirectoryToArchive(archive, path, path, Path.GetFileName(path));
                        }
                        else if (File.Exists(path))
                        {
                            string entryName = Path.GetFileName(path);
                            UpdateStatus($"圧縮中: {entryName}");
                            archive.CreateEntryFromFile(path, entryName, CompressionLevel.Fastest);
                        }
                    }
                }
            });

            UpdateStatus("圧縮が完了しました。");
        }

        private void AddDirectoryToArchive(ZipArchive archive, string sourceRootDir, string currentDir, string archivePathPrefix)
        {
            // ディレクトリ内のファイルを追加
            foreach (var file in Directory.GetFiles(currentDir))
            {
                string relativePath = Path.GetRelativePath(sourceRootDir, file);
                string entryName = Path.Combine(archivePathPrefix, relativePath).Replace('\\', '/');
                UpdateStatus($"圧縮中: {Path.GetFileName(file)}");
                archive.CreateEntryFromFile(file, entryName, CompressionLevel.Fastest);
            }

            // 子ディレクトリを再帰追加
            foreach (var subDir in Directory.GetDirectories(currentDir))
            {
                AddDirectoryToArchive(archive, sourceRootDir, subDir, archivePathPrefix);
            }
        }

        private async Task DecompressZipAsync(string zipFilePath)
        {
            string parentDir = Path.GetDirectoryName(zipFilePath) ?? string.Empty;
            string zipFileNameWithoutExt = Path.GetFileNameWithoutExtension(zipFilePath);

            UpdateStatus("展開先を選択中...");

            string selectedParentDir = string.Empty;

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    using (var dialog = new FolderBrowserDialog())
                    {
                        dialog.Description = "解凍先フォルダを選択してください";
                        dialog.InitialDirectory = parentDir;
                        dialog.SelectedPath = parentDir;

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
                    dialog.InitialDirectory = parentDir;
                    dialog.SelectedPath = parentDir;

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

            string destDirBase = Path.Combine(selectedParentDir, zipFileNameWithoutExt);
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
                            "展開先フォルダが既に存在します。上書きしますか？\n\n「はい」：既存のフォルダを削除して上書き\n「いいえ」：別の名前で保存\n「キャンセル」：処理を中止",
                            "展開先の重複",
                            MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question
                        );
                    }));
                }
                else
                {
                    result = MessageBox.Show(
                        "展開先フォルダが既に存在します。上書きしますか？\n\n「はい」：既存のフォルダを削除して上書き\n「いいえ」：別の名前で保存\n「キャンセル」：処理を中止",
                        "展開先の重複",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question
                    );
                }

                if (result == DialogResult.Yes)
                {
                    UpdateStatus("既存のフォルダを退避中...");
                    
                    // 同一ドライブ内にユニークな一時名を作成して移動
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

            int ansiCodePage = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            var encoding = System.Text.Encoding.GetEncoding(ansiCodePage);

            // まず展開先ルートディレクトリを作成
            Directory.CreateDirectory(destDir);

            // ZIPファイルのエントリー名一覧を取得
            var entryNames = new System.Collections.Generic.List<string>();

            await Task.Run(() =>
            {
                using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Read, encoding))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Name)) // ファイルエントリー
                        {
                            entryNames.Add(entry.FullName);
                        }
                        else
                        {
                            // ディレクトリのみのエントリーを事前作成
                            var dirPath = Path.Combine(destDir, entry.FullName);
                            Directory.CreateDirectory(dirPath);
                        }
                    }
                }
            });

            UpdateStatus($"展開中: {Path.GetFileName(destDir)}");

            // 並列で各ファイルを解凍 (NVMe環境用のマルチコア最大並列化)
            await Task.Run(() =>
            {
                Parallel.ForEach(
                    entryNames,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    entryName =>
                    {
                        using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Read, encoding))
                        {
                            var entry = archive.GetEntry(entryName);
                            if (entry != null)
                            {
                                string targetFilePath = Path.Combine(destDir, entry.FullName);
                                string? targetFileDir = Path.GetDirectoryName(targetFilePath);
                                if (targetFileDir != null)
                                {
                                    Directory.CreateDirectory(targetFileDir);
                                }

                                entry.ExtractToFile(targetFilePath, overwrite: true);
                            }
                        }
                    }
                );
            });

            UpdateStatus("展開が完了しました。");

            // 完了後のフォルダオープン確認
            DialogResult openFolderResult = DialogResult.None;

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    openFolderResult = MessageBox.Show(
                        "展開が完了しました。フォルダを開きますか？",
                        "完了",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );
                }));
            }
            else
            {
                openFolderResult = MessageBox.Show(
                    "展開が完了しました。フォルダを開きますか？",
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
                        FileName = destDir,
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
    }
}
