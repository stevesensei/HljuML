using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;

namespace HljuML.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();
    private void OnSectionClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is TKCode.Arc.Button { Tag: string section } && DataContext is SettingsViewModel model)
        {
            model.SelectSection(section);
            SettingsScroll.Offset = default;
        }
    }
}
