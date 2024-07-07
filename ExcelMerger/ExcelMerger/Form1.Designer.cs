namespace ExcelMerger
{
    partial class ExcelMerger
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExcelMerger));
            btnSelect = new Button();
            Merge = new Button();
            btnReset = new Button();
            listFiles = new ListBox();
            mergeIntoSingleWorksheet = new CheckBox();
            SuspendLayout();
            // 
            // btnSelect
            // 
            btnSelect.Location = new Point(12, 522);
            btnSelect.Name = "btnSelect";
            btnSelect.Size = new Size(152, 34);
            btnSelect.TabIndex = 0;
            btnSelect.Text = "Select Files";
            btnSelect.UseVisualStyleBackColor = true;
            btnSelect.Click += btnSelect_Click;
            // 
            // Merge
            // 
            Merge.Location = new Point(12, 562);
            Merge.Name = "Merge";
            Merge.Size = new Size(607, 34);
            Merge.TabIndex = 1;
            Merge.Text = "Merge";
            Merge.UseVisualStyleBackColor = true;
            Merge.Click += Merge_Click;
            // 
            // btnReset
            // 
            btnReset.Location = new Point(170, 522);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(152, 34);
            btnReset.TabIndex = 2;
            btnReset.Text = "Reset";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += btnReset_Click;
            // 
            // listFiles
            // 
            listFiles.FormattingEnabled = true;
            listFiles.ItemHeight = 25;
            listFiles.Location = new Point(12, 12);
            listFiles.Name = "listFiles";
            listFiles.Size = new Size(607, 504);
            listFiles.TabIndex = 3;
            // 
            // mergeIntoSingleWorksheet
            // 
            mergeIntoSingleWorksheet.AutoSize = true;
            mergeIntoSingleWorksheet.Location = new Point(330, 522);
            mergeIntoSingleWorksheet.Name = "mergeIntoSingleWorksheet";
            mergeIntoSingleWorksheet.Size = new Size(289, 29);
            mergeIntoSingleWorksheet.TabIndex = 4;
            mergeIntoSingleWorksheet.Text = "Combine Into Single Worksheet";
            mergeIntoSingleWorksheet.UseVisualStyleBackColor = true;
            // 
            // ExcelMerger
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(631, 607);
            Controls.Add(mergeIntoSingleWorksheet);
            Controls.Add(listFiles);
            Controls.Add(btnReset);
            Controls.Add(Merge);
            Controls.Add(btnSelect);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "ExcelMerger";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Excel Merger";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnSelect;
        private Button Merge;
        private Button btnReset;
        private ListBox listFiles;
        private CheckBox mergeIntoSingleWorksheet;
        private ProgressBar progressBar;
    }
}
