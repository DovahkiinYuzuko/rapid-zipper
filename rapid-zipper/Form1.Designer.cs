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
            DragDropPanel.Size = new Size(302, 147);
            DragDropPanel.TabIndex = 0;
            DragDropPanel.DragDrop += RapidZipper_DragDrop;
            DragDropPanel.DragEnter += RapidZipper_DragEnter;
            DragDropPanel.Paint += panel1_Paint;
            // 
            // FormatComboBox
            // 
            FormatComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            FormatComboBox.FormattingEnabled = true;
            FormatComboBox.Location = new Point(95, 49);
            FormatComboBox.Name = "FormatComboBox";
            FormatComboBox.Size = new Size(121, 23);
            FormatComboBox.TabIndex = 2;
            FormatComboBox.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // ProcessingBar
            // 
            ProcessingBar.Location = new Point(46, 111);
            ProcessingBar.Name = "ProcessingBar";
            ProcessingBar.Size = new Size(218, 24);
            ProcessingBar.TabIndex = 1;
            ProcessingBar.Click += progressBar1_Click;
            // 
            // Statuslabel
            // 
            Statuslabel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Statuslabel.AutoSize = true;
            Statuslabel.Location = new Point(46, 9);
            Statuslabel.Name = "Statuslabel";
            Statuslabel.Size = new Size(218, 15);
            Statuslabel.TabIndex = 0;
            Statuslabel.Text = "フォルダまたはZIPファイルをドロップしてください ";
            Statuslabel.TextAlign = ContentAlignment.MiddleCenter;
            Statuslabel.Click += label1_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(46, 52);
            label1.Name = "label1";
            label1.Size = new Size(43, 15);
            label1.TabIndex = 3;
            label1.Text = "圧縮先";
            label1.Click += label1_Click_1;
            // 
            // RapidZipper
            // 
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(302, 147);
            Controls.Add(DragDropPanel);
            Name = "RapidZipper";
            Text = "RapidZipper";
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
