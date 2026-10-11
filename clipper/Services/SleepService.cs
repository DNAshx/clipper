using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace clipper.Services
{
    public class SleepService
    {
        private readonly DispatcherTimer timer;
        private readonly Func<TimeSpan> getIdleTime;

        private bool idleTriggered;

        private readonly TimeSpan sleepAfter =
            TimeSpan.FromSeconds(20);

        public event Action? IdleReached;
        public event Action? ActivityDetected;

        public SleepService(Func<TimeSpan>? getIdleTime = null)
        {
            this.getIdleTime = getIdleTime ?? GetIdleTime;
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
            TimeSpan idleTime = getIdleTime();

            if (!idleTriggered &&
                idleTime >= sleepAfter)
            {
                idleTriggered = true;

                IdleReached?.Invoke();

                return;
            }

            if (idleTriggered &&
                idleTime < TimeSpan.FromSeconds(2))
            {
                // Повторяем проверку при активности, пока архетип не разрешит пробуждение.
                ActivityDetected?.Invoke();
            }
        }

        public void ResetIdleCycle()
        {
            idleTriggered = false;
        }

        private static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO info = new()
            {
                cbSize =
                    (uint)Marshal.SizeOf<LASTINPUTINFO>()
            };

            if (!GetLastInputInfo(ref info))
                return TimeSpan.Zero;

            uint idleMilliseconds =
                unchecked(
                    (uint)Environment.TickCount -
                    info.dwTime
                );

            return TimeSpan.FromMilliseconds(
                idleMilliseconds
            );
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