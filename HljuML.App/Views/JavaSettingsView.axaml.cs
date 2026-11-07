using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;

namespace HljuML.App.Views;

public partial class JavaSettingsView : UserControl
{
    public JavaSettingsView() => InitializeComponent();

    private async void OnScanJavaClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is JavaSettingsViewModel viewModel)
            await viewModel.ScanAsync();
    }
}
