using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;
using HljuML.Shared.Model;
using ArcButton = TKCode.Arc.Button;

namespace HljuML.App.Views.Home;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    public IList<Control> Cards => HomeFeed.Children;

    private void OnStopInstanceClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not ArcButton { DataContext: RunningInstanceRow row } button ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var result = viewModel.StopGameInstance(row.Instance);
        button.Content = result.Status == ServiceStatus.Success ? "结束中…" : "结束失败";
        ToolTip.SetTip(button, result.Message);
    }
}