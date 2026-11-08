using System;
using Avalonia.Controls;
using Avalonia.Threading;
using HljuML.Core.Infrastructure.Logging;

namespace HljuML.App.Views;

public partial class LogWindow : Window
{
    private readonly LauncherFileLoggerProvider? _logs;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public LogWindow()
    {
        InitializeComponent();
    }

    public LogWindow(LauncherFileLoggerProvider logs) : this()
    {
        _logs = logs;
        _timer.Tick += OnTick;
        Refresh();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        var text = _logs?.GetRecentText() ?? "";
        if (LogOutput.Text != text)
        {
            LogOutput.Text = text;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        base.OnClosed(e);
    }
}