using System.Windows.Threading;

namespace clipper.Services
{
    public sealed class BlinkService
    {
        private readonly Action<bool> render;
        private readonly DispatcherTimer blinkTimer;
        private readonly DispatcherTimer reopenTimer;
        private readonly Random random = new();

        public BlinkService(Action<bool> render)
        {
            this.render = render;
            blinkTimer = new DispatcherTimer();
            blinkTimer.Tick += BlinkTimer_Tick;
            reopenTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(140)
            };
            reopenTimer.Tick += ReopenTimer_Tick;
        }

        public void Start()
        {
            Stop();
            render(false);
            ScheduleNextBlink();
        }

        public void Stop()
        {
            blinkTimer.Stop();
            reopenTimer.Stop();
        }

        private void ScheduleNextBlink()
        {
            blinkTimer.Interval = TimeSpan.FromSeconds(random.NextDouble() * 5 + 4);
            blinkTimer.Start();
        }

        private void BlinkTimer_Tick(object? sender, EventArgs e)
        {
            blinkTimer.Stop();
            render(true);
            reopenTimer.Start();
        }

        private void ReopenTimer_Tick(object? sender, EventArgs e)
        {
            reopenTimer.Stop();
            render(false);
            ScheduleNextBlink();
        }
    }
}