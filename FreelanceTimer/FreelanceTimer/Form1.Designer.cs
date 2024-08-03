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
            btnReset = new Button();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            txtHour = new TextBox();
            txtMinute = new TextBox();
            txtSecond = new TextBox();
            txtScore = new TextBox();
            label4 = new Label();
            SuspendLayout();
            // 
            // btnStart
            // 
            btnStart.Location = new Point(12, 267);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(112, 34);
            btnStart.TabIndex = 0;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnReset
            // 
            btnReset.Location = new Point(134, 267);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(112, 34);
            btnReset.TabIndex = 3;
            btnReset.Text = "Reset";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += btnReset_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(24, 152);
            label1.Name = "label1";
            label1.Size = new Size(52, 25);
            label1.TabIndex = 5;
            label1.Text = "Hour";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(176, 152);
            label2.Name = "label2";
            label2.Size = new Size(67, 25);
            label2.TabIndex = 6;
            label2.Text = "Minute";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(95, 152);
            label3.Name = "label3";
            label3.Size = new Size(71, 25);
            label3.TabIndex = 7;
            label3.Text = "Second";
            // 
            // txtHour
            // 
            txtHour.BorderStyle = BorderStyle.FixedSingle;
            txtHour.Font = new Font("Segoe UI", 24F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtHour.Location = new Point(12, 180);
            txtHour.Name = "txtHour";
            txtHour.ReadOnly = true;
            txtHour.RightToLeft = RightToLeft.Yes;
            txtHour.Size = new Size(74, 71);
            txtHour.TabIndex = 8;
            txtHour.Text = "00";
            txtHour.TextAlign = HorizontalAlignment.Center;
            // 
            // txtMinute
            // 
            txtMinute.BorderStyle = BorderStyle.FixedSingle;
            txtMinute.Font = new Font("Segoe UI", 24F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtMinute.Location = new Point(92, 180);
            txtMinute.Name = "txtMinute";
            txtMinute.ReadOnly = true;
            txtMinute.RightToLeft = RightToLeft.Yes;
            txtMinute.Size = new Size(74, 71);
            txtMinute.TabIndex = 9;
            txtMinute.Text = "00";
            txtMinute.TextAlign = HorizontalAlignment.Center;
            // 
            // txtSecond
            // 
            txtSecond.BorderStyle = BorderStyle.FixedSingle;
            txtSecond.Font = new Font("Segoe UI", 24F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtSecond.Location = new Point(172, 180);
            txtSecond.Name = "txtSecond";
            txtSecond.ReadOnly = true;
            txtSecond.RightToLeft = RightToLeft.Yes;
            txtSecond.Size = new Size(74, 71);
            txtSecond.TabIndex = 10;
            txtSecond.Text = "00";
            txtSecond.TextAlign = HorizontalAlignment.Center;
            // 
            // txtScore
            // 
            txtScore.Font = new Font("Segoe UI", 24F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtScore.Location = new Point(12, 63);
            txtScore.Name = "txtScore";
            txtScore.Size = new Size(234, 71);
            txtScore.TabIndex = 11;
            txtScore.Text = "0";
            txtScore.TextAlign = HorizontalAlignment.Right;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label4.Location = new Point(13, 11);
            label4.Name = "label4";
            label4.Size = new Size(217, 38);
            label4.TabIndex = 12;
            label4.Text = "Freelance Timer";
            // 
            // formTimer
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(258, 314);
            Controls.Add(label4);
            Controls.Add(txtScore);
            Controls.Add(txtSecond);
            Controls.Add(txtMinute);
            Controls.Add(txtHour);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(btnReset);
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
        private Button btnReset;
        private Label label1;
        private Label label2;
        private Label label3;
        private TextBox txtHour;
        private TextBox txtMinute;
        private TextBox txtSecond;
        private TextBox txtScore;
        private Label label4;
    }
}
