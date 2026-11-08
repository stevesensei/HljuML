using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;

namespace HljuML.App.Views.Downloads;

public partial class GameInstallationView : UserControl
{
    public GameInstallationView() => InitializeComponent();
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var isNarrow = e.NewSize.Width < 560;

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
    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel) viewModel.BackToCatalog();
    }
    private void OnRemoveLoaderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel) viewModel.RemoveLoader();
    }
    private void OnLoaderExpandedChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel
            && sender is TKCode.Arc.ExpandableCard { IsExpanded: true, DataContext: GameLoaderChoice { IsSelected: false } choice })
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