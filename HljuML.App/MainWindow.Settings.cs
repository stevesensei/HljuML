using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Views;
using HljuML.Core.Infrastructure.Logging;
using HljuML.Core.Infrastructure.Settings;

namespace HljuML.App;

public partial class MainWindow
{
    private bool _closeAfterDownloads;
    private LogWindow? _logsWindow;
    private readonly LauncherSettingsService? _settings;
    private readonly LauncherFileLoggerProvider? _logs;
    private void OnGameStarted()
    {
        if (_closed || _settings is null) return;
        var behavior = _settings.Value.AfterGameStart;
        if (behavior == LauncherStartBehavior.KeepOpen) return;
        if (behavior == LauncherStartBehavior.Minimize) { WindowState = WindowState.Minimized; return; }
        _closeAfterDownloads = true;
        WindowState = WindowState.Minimized;
        // 启动事件在启动流程内触发，延后至下一次轮询，避免关闭时中断本次启动收尾。
    }

    private void TryDeferredClose()
    {
        if (!_closeAfterDownloads || _closed || _settings is null) return;
        if (_settings.Value.AfterGameStart != LauncherStartBehavior.Close)
        { _closeAfterDownloads = false; return; }
        if (_downloads?.GetActiveDownloadCount() == 0 && DataContext is Core.MainViewModel { IsLaunching: false }) Close();
    }

    private void OnLogsClicked(object? sender, RoutedEventArgs e)
    {
        if (_logs is null) return;
        if (_logsWindow is { } existing)
        {
            existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }
        var window = new LogWindow(_logs);
        _logsWindow = window;
        window.Closed += (_, _) => _logsWindow = null;
        window.Show(this);
    }
}
