using System;
using Avalonia.Controls;
using Avalonia.Threading;
using HljuML.App.Core;

namespace HljuML.App.Views;

public partial class DownloadManagerWindow : Window
{
    private readonly DispatcherTimer _refreshTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(400)
    };

    public DownloadManagerWindow()
    {
        InitializeComponent();
        _refreshTimer.Tick += OnRefresh;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        RefreshDownloads();
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefresh;
        base.OnClosed(e);
    }

    private void OnRefresh(object? sender, EventArgs e)
    {
        RefreshDownloads();
    }

    internal void RefreshDownloads()
    {
        if (DataContext is DownloadQueueViewModel downloads)
        {
            downloads.Refresh(true);
            if (downloads.HasActiveDownloads)
            {
                _refreshTimer.Start();
            }
            else
            {
                _refreshTimer.Stop();
            }
        }
    }
}