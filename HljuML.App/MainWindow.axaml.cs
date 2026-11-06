using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;
using HljuML.Shared.Model;

namespace HljuML.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnLaunchClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { IsEnabled: true } button || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        button.IsEnabled = false;
        LaunchStatus.Text = "正在准备并启动游戏…";

        try
        {
            var result = await viewModel.LaunchGameAsync();
            LaunchStatus.Text = result is { Status: ServiceStatus.Success, Data: int processId }
                ? $"游戏进程已启动（PID {processId}）"
                : result.Message;
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
