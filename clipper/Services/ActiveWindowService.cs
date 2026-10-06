using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace clipper.Services
{
    public class ActiveWindowInfo
    {
        public string Title { get; init; } = "";
        public string ProcessName { get; init; } = "";
        public string ProcessPath { get; init; } = "";
        public int ProcessId { get; init; }
    }

    public static class ActiveWindowService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(
            IntPtr hWnd,
            StringBuilder text,
            int count
        );

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr hWnd,
            out uint processId
        );

        public static bool IsClipper(ActiveWindowInfo window)
        {
            return window.ProcessId == Environment.ProcessId;
        }

        public static ActiveWindowInfo GetActiveWindow()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();

                if (handle == IntPtr.Zero)
                    return new ActiveWindowInfo();

                const int maxChars = 1024;
                StringBuilder buffer = new StringBuilder(maxChars);

                GetWindowText(handle, buffer, maxChars);

                GetWindowThreadProcessId(handle, out uint processId);

                using Process process = Process.GetProcessById((int)processId);

                string path = "";

                try
                {
                    path = process.MainModule?.FileName ?? "";
                }
                catch
                {
                    // Некоторые процессы могут не дать прочитать путь
                }

                return new ActiveWindowInfo
                {
                    Title = buffer.ToString(),
                    ProcessName = process.ProcessName,
                    ProcessPath = path,
                    ProcessId = (int)processId
                };
            }
            catch
            {
                return new ActiveWindowInfo();
            }
        }
    }
}
