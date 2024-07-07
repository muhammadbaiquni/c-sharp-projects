using System.Numerics;
using System.Timers;

namespace Waktu
{
    public partial class formTimer : Form
    {
        System.Timers.Timer timer;
        int h, m, s;

        public formTimer()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            timer = new System.Timers.Timer();
            timer.Interval = 1000;
            timer.Elapsed += OnTimerEvent;
        }

        private void OnTimerEvent(object? sender, ElapsedEventArgs e)
        {
            Invoke(new Action(() =>
            {
                s += 1;

                if (s == 60)
                {
                    s = 0;
                    m += 1;
                }

                if (m == 60)
                {
                    m = 0;
                    h += 1;
                }

                txtTimer.Text = string.Format("{0}:{1}:{2}",
                    h.ToString().PadLeft(2, '0'),
                    m.ToString().PadLeft(2, '0'),
                    s.ToString().PadLeft(2, '0'));
            }));
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            timer.Start();
            btnStart.Enabled = false;
            btnReset.Enabled = false;

            txtScore.Text = string.Empty;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            timer.Stop();
            btnStart.Enabled = true;
            btnReset.Enabled = true;

            txtScore.Text = $"{Calculation()}";
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            h = 0;
            m = 0;
            s = 0;

            txtScore.Text = string.Empty;

            txtTimer.Text = string.Format("{0}:{1}:{2}",
                h.ToString().PadLeft(2, '0'),
                m.ToString().PadLeft(2, '0'),
                s.ToString().PadLeft(2, '0'));
        }

        private decimal Calculation()
        {
            var totalSeconds = (h * 3600) + (m * 60) + s;
            
            return Math.Round((decimal)totalSeconds / 3600, 2);
        }
    }
}
