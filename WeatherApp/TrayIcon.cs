using System;
using System.Collections.Generic;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WeatherApp;

internal sealed class TrayMenuItem
{
    public string Text { get; }
    public Action Handler { get; }

    public TrayMenuItem(string text, Action handler)
    {
        Text = text;
        Handler = handler;
    }
}

internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _disposed;

    public Drawing.Icon Icon
    {
        get => _notifyIcon.Icon;
        set => _notifyIcon.Icon = value;
    }

    public string ToolTipText
    {
        get => _notifyIcon.Text;
        set => _notifyIcon.Text = value;
    }

    public List<TrayMenuItem> MenuItems { get; } = new();

    public event EventHandler LeftClick;

    public TrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon { Visible = true };
        _notifyIcon.MouseClick += OnMouseClick;
    }

    private void OnMouseClick(object sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            LeftClick?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Button == Forms.MouseButtons.Right)
        {
            ShowContextMenu();
        }
    }

    private void ShowContextMenu()
    {
        if (MenuItems.Count == 0) return;

        var menu = new Forms.ContextMenuStrip();
        foreach (var item in MenuItems)
        {
            menu.Items.Add(item.Text, null, (_, _) => item.Handler?.Invoke());
        }

        menu.Show(Forms.Cursor.Position);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
