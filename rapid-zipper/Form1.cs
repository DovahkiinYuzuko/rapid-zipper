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

            string targetPath = paths[0];

            try
            {
                SetUIProcessing(true);

                if (Directory.Exists(targetPath))
                {
                    await CompressFolderAsync(targetPath);
                }
                else if (File.Exists(targetPath) && Path.GetExtension(targetPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    await DecompressZipAsync(targetPath);
                }
                else
                {
                    UpdateStatus("フォルダまたはZIPファイルをドロップしてください。");
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

        private async Task DecompressZipAsync(string zipFilePath)
        {
            string parentDir = Path.GetDirectoryName(zipFilePath) ?? string.Empty;
            string zipFileNameWithoutExt = Path.GetFileNameWithoutExtension(zipFilePath);
            string destDirBase = Path.Combine(parentDir, zipFileNameWithoutExt + "_extracted");
            string destDir = destDirBase;

            UpdateStatus("展開先を確認中...");

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
                    UpdateStatus("既存のフォルダを削除中...");
                    await Task.Run(() =>
                    {
                        Directory.Delete(destDir, true);
                    });
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

            UpdateStatus($"展開中: {Path.GetFileName(destDir)}");

            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(zipFilePath, destDir);
            });

            UpdateStatus("展開が完了しました。");
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
