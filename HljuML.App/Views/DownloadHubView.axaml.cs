using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;

namespace HljuML.App.Views;

public partial class DownloadHubView : UserControl
{
    private const double CompactThreshold = 760;

    public DownloadHubView()
    {
        InitializeComponent();
    }

    private void OnNavigationClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DownloadHubViewModel viewModel
            && sender is Control { Tag: string route }
            && Enum.TryParse<DownloadSection>(route, out var section))
        {
            viewModel.Select(section);
        }
    }

    private void OnHubSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < CompactThreshold;
        Classes.Set("compact", compact);
    }
}