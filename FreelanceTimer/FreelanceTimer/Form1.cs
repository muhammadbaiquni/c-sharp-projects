using System.Numerics;
using System.Timers;

namespace Waktu
{
    public partial class formTimer : Form
    {
        System.Timers.Timer timer;
        int h, m, s;

        const string Start = "Start";
        const string Stop = "Stop";

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

                txtHour.Text = h.ToString().PadLeft(2, '0');
                txtMinute.Text = m.ToString().PadLeft(2, '0');
                txtSecond.Text = s.ToString().PadLeft(2, '0');

                txtScore.Text = $"{Calculation()}";
            }));
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (btnStart.Text == Start)
            {
                btnStart.Text = Stop;

                timer.Start();
                btnReset.Enabled = false;

                //txtScore.Text = string.Empty;
            }
            else
            {
                timer.Stop();

                btnStart.Text = Start;
                btnReset.Enabled = true;

                //txtScore.Text = $"{Calculation()}";
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            h = 0;
            m = 0;
            s = 0;

            txtScore.Text = "0";

            txtHour.Text = h.ToString().PadLeft(2, '0');
            txtMinute.Text = m.ToString().PadLeft(2, '0');
            txtSecond.Text = s.ToString().PadLeft(2, '0');
        }

        private decimal Calculation()
        {
            var totalSeconds = (h * 3600) + (m * 60) + s;

            return Math.Round((decimal)totalSeconds / 3600, 2);
        }
    }
}
