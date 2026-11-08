using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HljuML.App.Views.Settings;

public partial class LauncherSettingsView : UserControl
{
    public LauncherSettingsView()
    {
        InitializeComponent();
    }

    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        SettingsEditing.Commit(DataContext, sender);
    }
}