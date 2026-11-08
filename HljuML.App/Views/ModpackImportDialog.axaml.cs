using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;

namespace HljuML.App.Views;

public partial class ModpackImportDialog : UserControl
{
    public ModpackImportDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ModpackImportViewModel { CanDismiss: true } viewModel)
        {
            viewModel.IsOpen = false;
        }
    }

    private async void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ModpackImportViewModel viewModel)
        {
            await viewModel.ConfirmAsync();
        }
    }

    private async void OnRetryClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ModpackImportViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }
}