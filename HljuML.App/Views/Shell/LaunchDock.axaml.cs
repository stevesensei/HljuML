using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HljuML.App.Views.Shell;

public partial class LaunchDock : UserControl
{
    public LaunchDock() => InitializeComponent();
    public event EventHandler<RoutedEventArgs>? AccountRequested;
    public event EventHandler<RoutedEventArgs>? InstanceRequested;
    public event EventHandler<RoutedEventArgs>? LaunchRequested;
    private void OnAccountClicked(object? sender, RoutedEventArgs e) => AccountRequested?.Invoke(this, e);
    private void OnChooseInstanceClicked(object? sender, RoutedEventArgs e) => InstanceRequested?.Invoke(this, e);
    private void OnLaunchClicked(object? sender, RoutedEventArgs e) => LaunchRequested?.Invoke(this, e);
    private void OnLaunchDockSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is Border dock) dock.Classes.Set("compact-dock", e.NewSize.Width < 600);
    }
}
