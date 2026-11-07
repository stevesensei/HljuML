using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HljuML.App.Core;
using HljuML.App.Views;
using HljuML.Core.Model.Entity;
using HljuML.Shared.Model;
using Microsoft.Extensions.DependencyInjection;
using TKCode.Arc;
using ArcButton = TKCode.Arc.Button;

namespace HljuML.App;

public partial class MainWindow : Window
{
    private readonly IServiceScopeFactory? _scopes;
    private readonly DispatcherTimer _downloadsTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(400)
    };

    public MainWindow()
    {
        InitializeComponent();
        PropertyChanged += OnWindowPropertyChanged;
        _downloadsTimer.Tick += (_, _) => RefreshDownloads();
    }

    internal MainWindow(IServiceScopeFactory scopes) : this() => _scopes = scopes;

    public IList<Control> HomeCards => HomeFeed.Children;

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainViewModel viewModel)
        {
            RefreshDownloads();
            _downloadsTimer.Start();
            await viewModel.RefreshLaunchOptionsAsync();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _downloadsTimer.Stop();
        base.OnClosed(e);
    }

    private void OnHomeClicked(object? sender, RoutedEventArgs e) => ShowPage(HomePage, HomeButton);

    private void OnDownloadsClicked(object? sender, RoutedEventArgs e)
    {
        ShowPage(DownloadsPage, DownloadsButton);
        RefreshDownloads();
        if (sender != DownloadsButton)
            DownloadsButton.Focus();
    }

    private void RefreshDownloads()
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Downloads.Refresh(DownloadsPage.IsVisible);
    }

    private void OnLaunchDockSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is Border dock)
            dock.Classes.Set("compact-dock", e.NewSize.Width < 600);
    }

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

    private async void OnAccountClicked(object? sender, RoutedEventArgs e)
    {
        if (_scopes is null)
            return;

        var accounts = new AccountWindow(_scopes)
        {
            Width = Math.Clamp(Bounds.Width - 32, 540, 860),
            Height = Math.Clamp(Bounds.Height - 32, 420, 552)
        };
        var selectedProfileId = await ShowSelectionDialog<Guid?>(accounts);
        if (DataContext is MainViewModel viewModel)
            await viewModel.RefreshLaunchOptionsAsync(selectedProfileId);
    }

    private async void OnChooseInstanceClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { CanChangeLaunchSelection: true } viewModel)
            return;

        await viewModel.RefreshLaunchOptionsAsync();
        if (!IsVisible)
            return;

        var picker = new InstancePickerWindow(viewModel)
        {
            Width = Math.Clamp(Bounds.Width - 32, 520, 880),
            Height = Math.Clamp(Bounds.Height - 32, 400, 552)
        };
        if (await ShowSelectionDialog<GameInstanceData?>(picker) is { } selected)
            viewModel.SelectInstance(selected);
    }

    private async Task<TResult> ShowSelectionDialog<TResult>(Window window)
    {
        SelectionScrim.IsVisible = true;
        try
        {
            return await window.ShowDialog<TResult>(this);
        }
        finally
        {
            SelectionScrim.IsVisible = false;
        }
    }

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
        if (DataContext is MainViewModel viewModel && await viewModel.LaunchGameAsync() is { } error && IsVisible)
            Toast.Show(this, "启动失败", ToastKind.Error, error);
    }

    private void OnStopInstanceClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not ArcButton { DataContext: RunningInstanceRow row } button ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var result = viewModel.StopGameInstance(row.Instance);
        button.Content = result.Status == ServiceStatus.Success ? "结束中…" : "结束失败";
        ToolTip.SetTip(button, result.Message);
    }
}
