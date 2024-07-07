namespace Waktu
{
    partial class formTimer
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(formTimer));
            btnStart = new Button();
            btnStop = new Button();
            txtTimer = new TextBox();
            btnReset = new Button();
            txtScore = new TextBox();
            SuspendLayout();
            // 
            // btnStart
            // 
            btnStart.Location = new Point(230, 12);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(112, 34);
            btnStart.TabIndex = 0;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(348, 12);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(112, 34);
            btnStop.TabIndex = 1;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // txtTimer
            // 
            txtTimer.Location = new Point(12, 12);
            txtTimer.Name = "txtTimer";
            txtTimer.Size = new Size(137, 31);
            txtTimer.TabIndex = 2;
            txtTimer.Text = "00:00:00";
            txtTimer.TextAlign = HorizontalAlignment.Center;
            // 
            // btnReset
            // 
            btnReset.Location = new Point(466, 12);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(112, 34);
            btnReset.TabIndex = 3;
            btnReset.Text = "Reset";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += btnReset_Click;
            // 
            // txtScore
            // 
            txtScore.Location = new Point(155, 12);
            txtScore.Name = "txtScore";
            txtScore.Size = new Size(69, 31);
            txtScore.TabIndex = 4;
            txtScore.TextAlign = HorizontalAlignment.Center;
            // 
            // formTimer
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(591, 62);
            Controls.Add(txtScore);
            Controls.Add(btnReset);
            Controls.Add(txtTimer);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "formTimer";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Freelance Timer";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnStart;
        private Button btnStop;
        private TextBox txtTimer;
        private Button btnReset;
        private TextBox txtScore;
    }
}
