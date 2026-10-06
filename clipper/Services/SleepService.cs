using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace clipper.Services
{
    public class SleepService
    {
        private readonly DispatcherTimer timer;

        private bool isSleeping;

        private readonly TimeSpan sleepAfter =
            TimeSpan.FromMinutes(3);

        public event Action? FellAsleep;
        public event Action? WokeUp;

        public SleepService()
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            TimeSpan idleTime = GetIdleTime();

            if (!isSleeping && idleTime >= sleepAfter)
            {
                isSleeping = true;
                FellAsleep?.Invoke();
            }
            else if (isSleeping && idleTime < TimeSpan.FromSeconds(2))
            {
                isSleeping = false;
                WokeUp?.Invoke();
            }
        }

        private static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO info = new()
            {
                cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
            };

            if (!GetLastInputInfo(ref info))
                return TimeSpan.Zero;

            uint idleMilliseconds =
                unchecked((uint)Environment.TickCount - info.dwTime);

            return TimeSpan.FromMilliseconds(idleMilliseconds);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(
            ref LASTINPUTINFO plii
        );
    }
}
