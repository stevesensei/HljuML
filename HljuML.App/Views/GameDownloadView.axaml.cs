using Avalonia;
using Avalonia.Controls;
using HljuML.App.Core;

namespace HljuML.App.Views;

public partial class GameDownloadView : UserControl
{
    public GameDownloadView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
    }

    private async void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is GameDownloadViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}