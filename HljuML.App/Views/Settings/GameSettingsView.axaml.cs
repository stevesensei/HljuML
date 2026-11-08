using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HljuML.App.Views.Settings;

public partial class GameSettingsView : UserControl
{
    public GameSettingsView()
    {
        InitializeComponent();
    }

    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        SettingsEditing.Commit(DataContext, sender);
    }
}