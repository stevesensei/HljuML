using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;

namespace HljuML.App.Views;

public partial class GameDownloadView : UserControl
{
    public GameDownloadView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
        SizeChanged += OnSizeChanged;
    }

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

        ConfigurationHeader.ColumnDefinitions = ColumnDefinitions.Parse(isNarrow ? "Auto,*" : "Auto,Auto,*");
        ConfigurationHeader.RowDefinitions = RowDefinitions.Parse(isNarrow ? "Auto,Auto" : "Auto");
        Grid.SetColumnSpan(ConfigurationHeader.Children[0], isNarrow ? 2 : 1);
        Grid.SetColumn(ConfigurationIcon, isNarrow ? 0 : 1);
        Grid.SetRow(ConfigurationIcon, isNarrow ? 1 : 0);
        Grid.SetColumn(ConfigurationTitle, isNarrow ? 1 : 2);
        Grid.SetRow(ConfigurationTitle, isNarrow ? 1 : 0);

        InstallFooter.ColumnDefinitions = ColumnDefinitions.Parse(isNarrow ? "*" : "*,Auto");
        InstallFooter.RowDefinitions = RowDefinitions.Parse(isNarrow ? "Auto,Auto" : "Auto");
        Grid.SetColumn(InstallButton, isNarrow ? 0 : 1);
        Grid.SetRow(InstallButton, isNarrow ? 1 : 0);
        InstallButton.HorizontalAlignment = isNarrow ? Avalonia.Layout.HorizontalAlignment.Stretch
            : Avalonia.Layout.HorizontalAlignment.Right;
    }

    private async void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel)
            await viewModel.InitializeAsync();
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

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel) viewModel.BackToCatalog();
    }

    private void OnRemoveLoaderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel) viewModel.RemoveLoader();
    }

    private void OnLoaderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel
            && sender is Control { DataContext: GameLoaderChoice choice })
            viewModel.SelectLoader(choice);
    }

    private async void OnRetryLoadersClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel) await viewModel.RetryLoadersAsync();
    }

    private async void OnInstallClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel) await viewModel.InstallAsync();
    }
}
