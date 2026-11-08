using System;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc;

namespace HljuML.App.Views.Shell;

public partial class LauncherTitleBar : UserControl
{
    public LauncherTitleBar()
    {
        InitializeComponent();
    }

    public event EventHandler<RoutedEventArgs>? HomeRequested;
    public event EventHandler<RoutedEventArgs>? DownloadsRequested;
    public event EventHandler<RoutedEventArgs>? SettingsRequested;
    public event EventHandler<RoutedEventArgs>? LogsRequested;
    public event EventHandler<RoutedEventArgs>? MinimizeRequested;
    public event EventHandler<RoutedEventArgs>? MaximizeRequested;
    public event EventHandler<RoutedEventArgs>? CloseRequested;

    private void OnHomeClicked(object? sender, RoutedEventArgs e)
    {
        HomeRequested?.Invoke(this, e);
    }

    private void OnDownloadsClicked(object? sender, RoutedEventArgs e)
    {
        DownloadsRequested?.Invoke(this, e);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, e);
    }

    private void OnLogsClicked(object? sender, RoutedEventArgs e)
    {
        LogsRequested?.Invoke(this, e);
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        MinimizeRequested?.Invoke(this, e);
    }

    private void OnMaximizeClicked(object? sender, RoutedEventArgs e)
    {
        MaximizeRequested?.Invoke(this, e);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, e);
    }

    public void SetActivePage(string page)
    {
        HomeButton.Variant = page == "Home" ? ButtonVariant.Secondary : ButtonVariant.Ghost;
        DownloadsButton.Variant = page == "Downloads" ? ButtonVariant.Secondary : ButtonVariant.Ghost;
        SettingsButton.Variant = page == "Settings" ? ButtonVariant.Secondary : ButtonVariant.Ghost;
    }

    public void SetMaximized(bool maximized)
    {
        MaximizeIcon.IsVisible = !maximized;
        RestoreIcon.IsVisible = maximized;
        var action = maximized ? "还原窗口" : "最大化";
        AutomationProperties.SetName(MaximizeButton, action);
        ToolTip.SetTip(MaximizeButton, action);
    }
}