using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;

namespace HljuML.App.Views;

public partial class CommunityResourceView : UserControl
{
    public CommunityResourceView() => InitializeComponent();

    private async void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is CommunityResourceViewModel viewModel)
            await viewModel.InitializeAsync();
    }

    private void OnCreateVanillaClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommunityResourceViewModel viewModel)
            viewModel.RequestVanillaCreation();
    }

    private void OnBackToResultsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommunityResourceViewModel viewModel)
            viewModel.ClearSelection();
    }
}
