using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;

namespace HljuML.App.Views.Downloads;

public partial class GameCatalogView : UserControl
{
    public GameCatalogView() => InitializeComponent();
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var isNarrow = e.NewSize.Width < 560;

        LatestGrid.ColumnDefinitions = ColumnDefinitions.Parse(isNarrow ? "*" : "*,*");
        LatestGrid.RowDefinitions = RowDefinitions.Parse(isNarrow ? "Auto,Auto" : "Auto");
        Grid.SetColumn(LatestSnapshotCard, isNarrow ? 0 : 1);
        Grid.SetRow(LatestSnapshotCard, isNarrow ? 1 : 0);

        CatalogToolbar.ColumnDefinitions = ColumnDefinitions.Parse(isNarrow ? "*" : "*,Auto");
        CatalogToolbar.RowDefinitions = RowDefinitions.Parse(isNarrow ? "Auto,Auto" : "Auto");
        Grid.SetColumn(CatalogSegments, isNarrow ? 0 : 1);
        Grid.SetRow(CatalogSegments, isNarrow ? 1 : 0);
        CatalogSegments.HorizontalAlignment = isNarrow ? Avalonia.Layout.HorizontalAlignment.Stretch
            : Avalonia.Layout.HorizontalAlignment.Right;

    }

    private async void OnRefreshClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel)
            await viewModel.RefreshCatalogAsync();
    }
    private async void OnVersionClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel
            && sender is Control { DataContext: GameCatalogRow { Version: { } version } })
            await viewModel.SelectVersionAsync(version);
    }
    private void OnGroupClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel && sender is Control { DataContext: GameCatalogRow row })
            viewModel.ToggleGroup(row.Group);
    }
}