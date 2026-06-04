namespace rapid_zipper
{
    partial class RapidZipper
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            DragDropPanel = new Panel();
            FormatComboBox = new ComboBox();
            ProcessingBar = new ProgressBar();
            Statuslabel = new Label();
            label1 = new Label();
            DragDropPanel.SuspendLayout();
            SuspendLayout();
            // 
            // DragDropPanel
            // 
            DragDropPanel.AllowDrop = true;
            DragDropPanel.Controls.Add(label1);
            DragDropPanel.Controls.Add(FormatComboBox);
            DragDropPanel.Controls.Add(ProcessingBar);
            DragDropPanel.Controls.Add(Statuslabel);
            DragDropPanel.Dock = DockStyle.Fill;
            DragDropPanel.Location = new Point(0, 0);
            DragDropPanel.Name = "DragDropPanel";
            DragDropPanel.Size = new Size(360, 150);
            DragDropPanel.TabIndex = 0;
            DragDropPanel.DragDrop += RapidZipper_DragDrop;
            DragDropPanel.DragEnter += RapidZipper_DragEnter;
            // 
            // FormatComboBox
            // 
            FormatComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            FormatComboBox.FormattingEnabled = true;
            FormatComboBox.Location = new Point(150, 49);
            FormatComboBox.Name = "FormatComboBox";
            FormatComboBox.Size = new Size(160, 23);
            FormatComboBox.TabIndex = 2;
            // 
            // ProcessingBar
            // 
            ProcessingBar.Location = new Point(30, 111);
            ProcessingBar.Name = "ProcessingBar";
            ProcessingBar.Size = new Size(300, 24);
            ProcessingBar.TabIndex = 1;
            // 
            // Statuslabel
            // 
            Statuslabel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Statuslabel.AutoSize = false;
            Statuslabel.Location = new Point(10, 9);
            Statuslabel.Name = "Statuslabel";
            Statuslabel.Size = new Size(340, 32);
            Statuslabel.TabIndex = 0;
            Statuslabel.Text = "Drop files or folders here\r\nフォルダまたはアーカイブをドロップしてください";
            Statuslabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            label1.AutoSize = false;
            label1.Location = new Point(30, 50);
            label1.Name = "label1";
            label1.Size = new Size(110, 20);
            label1.TabIndex = 3;
            label1.Text = "Format / 圧縮先";
            label1.TextAlign = ContentAlignment.MiddleRight;
            // 
            // RapidZipper
            // 
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(360, 150);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Controls.Add(DragDropPanel);
            Name = "RapidZipper";
            Text = "RapidZipper / ラピッド圧縮✧展開";
            Load += Form1_Load;
            DragDrop += RapidZipper_DragDrop;
            DragEnter += RapidZipper_DragEnter;
            DragDropPanel.ResumeLayout(false);
            DragDropPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel DragDropPanel;
        private Label Statuslabel;
        private ProgressBar ProcessingBar;
        private ComboBox FormatComboBox;
        private Label label1;
    }
}
