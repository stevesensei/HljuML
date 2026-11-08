using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;
using Button = TKCode.Arc.Button;

namespace HljuML.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnSectionClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section } && DataContext is SettingsViewModel model)
        {
            model.SelectSection(section);
            SettingsScroll.Offset = default;
        }
    }
}