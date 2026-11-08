using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.App.Core;
using TKCode.Arc;

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
    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string field } && DataContext is SettingsViewModel model) model.Commit(field);
    }
    private void OnDeveloperClicked(object? sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://github.com/stevesensei") { UseShellExecute = true }); }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        { Toast.Show(this, "无法打开浏览器", ToastKind.Error, "请手动访问 https://github.com/stevesensei。"); }
    }
}
