using System;
using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace clipper.Services
{
    public class TrayService : IDisposable
    {
        private readonly Forms.NotifyIcon notifyIcon;

        public event Action? ShowRequested;
        public event Action? ExitRequested;

        public TrayService()
        {
            string iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "original",
                "clippy.ico"
            );

            notifyIcon = new Forms.NotifyIcon
            {
                Icon = new Icon(iconPath),
                Text = "Clipper",
                Visible = true
            };

            Forms.ContextMenuStrip menu = new();

            Forms.ToolStripMenuItem showItem =
                new("Показать Скрепыша");

            Forms.ToolStripMenuItem exitItem =
                new("Убить Скрепыша");

            showItem.Click += (_, _) =>
            {
                ShowRequested?.Invoke();
            };

            exitItem.Click += (_, _) =>
            {
                ExitRequested?.Invoke();
            };

            menu.Items.Add(showItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(exitItem);

            notifyIcon.ContextMenuStrip = menu;

            notifyIcon.DoubleClick += (_, _) =>
            {
                ShowRequested?.Invoke();
            };
        }

        public void Dispose()
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }
    }
}