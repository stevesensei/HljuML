using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;
using HljuML.Core.GamePlay;
using HljuML.Shared.Model;
using TKCode.Arc;
using ArcButton = TKCode.Arc.Button;

namespace HljuML.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PropertyChanged += OnWindowPropertyChanged;
    }

    public IList<Control> HomeCards => HomeFeed.Children;

    private void OnHomeClicked(object? sender, RoutedEventArgs e) => ShowPage(HomePage, HomeButton);

    private void OnDownloadsClicked(object? sender, RoutedEventArgs e) => ShowPage(DownloadsPage, DownloadsButton);

    private void OnSettingsClicked(object? sender, RoutedEventArgs e) => ShowPage(SettingsPage, SettingsButton);

    private void ShowPage(Control page, ArcButton activeButton)
    {
        if (page.IsVisible)
        {
            return;
        }

        HomePage.IsVisible = page == HomePage;
        DownloadsPage.IsVisible = page == DownloadsPage;
        SettingsPage.IsVisible = page == SettingsPage;
        HomeButton.Variant = activeButton == HomeButton ? ButtonVariant.Secondary : ButtonVariant.Ghost;
        DownloadsButton.Variant = activeButton == DownloadsButton ? ButtonVariant.Secondary : ButtonVariant.Ghost;
        SettingsButton.Variant = activeButton == SettingsButton ? ButtonVariant.Secondary : ButtonVariant.Ghost;
        PageScroll.Offset = default;
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClicked(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != WindowStateProperty)
        {
            return;
        }

        var maximized = WindowState == WindowState.Maximized;
        ResizeGrips.IsVisible = WindowState == WindowState.Normal;
        WindowFrame.Classes.Set("rounded-window", WindowState == WindowState.Normal);
        MaximizeIcon.IsVisible = !maximized;
        RestoreIcon.IsVisible = maximized;
        var action = maximized ? "还原窗口" : "最大化";
        AutomationProperties.SetName(MaximizeButton, action);
        ToolTip.SetTip(MaximizeButton, action);
    }

    private async void OnLaunchClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.LaunchGameAsync();
        }
    }

    private void OnStopInstanceClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not ArcButton { DataContext: RunningGameInstance instance } button ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var result = viewModel.StopGameInstance(instance);
        button.Content = result.Status == ServiceStatus.Success ? "结束中…" : "结束失败";
        ToolTip.SetTip(button, result.Message);
    }
}
