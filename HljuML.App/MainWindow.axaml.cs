using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using HljuML.App.Core;
using HljuML.App.Views;
using HljuML.Core.Model.Entity;
using HljuML.Core.Infrastructure.Downloads;
using HljuML.Shared.Model;
using Microsoft.Extensions.DependencyInjection;
using TKCode.Arc;

namespace HljuML.App;

public partial class MainWindow : Window
{
    private readonly IServiceScopeFactory? _scopes;
    private readonly DownloadManagementService? _downloads;
    private readonly HashSet<Guid> _openedDownloadTasks = new();
    private DownloadManagerWindow? _downloadManager;
    private bool _closed;
    private bool _openDownloadsWhenOpened;
    private readonly DispatcherTimer _downloadsTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(400)
    };

    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnModpackDragOver, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DropEvent, OnModpackDrop, RoutingStrategies.Tunnel);
        PropertyChanged += OnWindowPropertyChanged;
        _downloadsTimer.Tick += OnDownloadsTick;
    }

    internal MainWindow(IServiceScopeFactory scopes, DownloadManagementService downloads,
        HljuML.Core.Infrastructure.Settings.LauncherSettingsService settings,
        HljuML.Core.Infrastructure.Logging.LauncherFileLoggerProvider logs) : this()
    {
        _scopes = scopes;
        _downloads = downloads;
        _settings = settings;
        _logs = logs;
        _downloads.TaskCreated += OnDownloadTaskCreated;
    }

    public IList<Control> HomeCards => HomePage.Cards;

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.GameStarted += OnGameStarted;
            RefreshDownloads();
            _downloadsTimer.Start();
            if (_openDownloadsWhenOpened)
                ShowDownloadManager();
            await viewModel.RefreshLaunchOptionsAsync();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _downloadsTimer.Stop();
        _downloadsTimer.Tick -= OnDownloadsTick;
        _closed = true;
        if (DataContext is MainViewModel mainModel) mainModel.GameStarted -= OnGameStarted;
        _logsWindow?.Close();
        if (DataContext is MainViewModel viewModel) viewModel.ModpackImport.IsOpen = false;
        if (_downloads is not null)
            _downloads.TaskCreated -= OnDownloadTaskCreated;
        _downloadManager?.Close();
        PropertyChanged -= OnWindowPropertyChanged;
        base.OnClosed(e);
    }

    private void OnHomeClicked(object? sender, RoutedEventArgs e) => ShowPage(HomePage, "Home");

    private void OnDownloadsClicked(object? sender, RoutedEventArgs e)
    {
        ShowPage(DownloadsPage, "Downloads");
    }

    private void OnDownloadsTick(object? sender, EventArgs e)
    {
        RefreshDownloads();
        TryDeferredClose();
    }

    private void RefreshDownloads()
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Downloads.Refresh(includeDetails: false);
    }

    private void OnDownloadTaskCreated(Guid id) => Dispatcher.UIThread.Post(() =>
    {
        if (_closed || !_openedDownloadTasks.Add(id))
            return;
        RefreshDownloads();
        if (IsVisible)
            ShowDownloadManager();
        else
            _openDownloadsWhenOpened = true;
    });

    private void OnDownloadManagerClicked(object? sender, RoutedEventArgs e) => ShowDownloadManager();

    private void ShowDownloadManager()
    {
        if (_closed || DataContext is not MainViewModel viewModel)
            return;
        if (_downloadManager is { } existing)
        {
            existing.RefreshDownloads();
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }
        var window = new DownloadManagerWindow { DataContext = viewModel.Downloads };
        _downloadManager = window;
        window.Closed += OnDownloadManagerClosed;
        window.Show(this);
    }

    private void OnDownloadManagerClosed(object? sender, EventArgs e)
    {
        if (sender is DownloadManagerWindow window)
            window.Closed -= OnDownloadManagerClosed;
        if (ReferenceEquals(_downloadManager, sender))
            _downloadManager = null;
        RefreshDownloads();
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e) => ShowPage(SettingsPage, "Settings");

    private void ShowPage(Control page, string activePage)
    {
        if (page.IsVisible)
        {
            return;
        }

        HomePage.IsVisible = page == HomePage;
        DownloadsPage.IsVisible = page == DownloadsPage;
        SettingsPage.IsVisible = page == SettingsPage;
        PageScroll.IsVisible = page == HomePage;
        TitleBar.SetActivePage(activePage);
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
        TitleBar.SetMaximized(maximized);
    }

    private async void OnLaunchClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && await viewModel.LaunchGameAsync(ReauthenticateProfileAsync) is { } error && IsVisible)
            Toast.Show(this, "启动失败", ToastKind.Error, error);
    }

    /// <summary>只恢复原启动选中的档案；用户关闭窗口时取消恢复，不切换账号或离线启动。</summary>
    private async Task<bool> ReauthenticateProfileAsync(Guid profileId)
    {
        if (_closed || !IsVisible || _scopes is null) return false;
        var accounts = new AccountWindow(_scopes, profileId)
        {
            Width = Math.Clamp(Bounds.Width - 32, 540, 860),
            Height = Math.Clamp(Bounds.Height - 32, 420, 552)
        };
        var selected = await ShowSelectionDialog<Guid?>(accounts);
        return selected == profileId && accounts.ReauthenticatedProfileId == profileId;
    }

}
