using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace clipper.Services
{
    public sealed class CursorTrackingService
    {
        private readonly FrameworkElement canvas;
        private readonly CharacterSpriteService sprites;
        private readonly DispatcherTimer timer;

        public CursorTrackingService(FrameworkElement canvas, CharacterSpriteService sprites)
        {
            this.canvas = canvas;
            this.sprites = sprites;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            timer.Tick += (_, _) =>
            {
                if (!canvas.IsVisible || !sprites.CanTrackCursor || !GetCursorPos(out var cursor))
                    return;
                sprites.UpdateGaze(canvas.PointFromScreen(new Point(cursor.X, cursor.Y)));
            };
        }

        public void Start() => timer.Start();
        public void Stop() => timer.Stop();

        [StructLayout(LayoutKind.Sequential)]
        private struct CursorPoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out CursorPoint point);
    }
}