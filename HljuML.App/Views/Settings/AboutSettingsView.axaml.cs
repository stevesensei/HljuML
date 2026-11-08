using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc;

namespace HljuML.App.Views.Settings;

public partial class AboutSettingsView : UserControl
{
    public AboutSettingsView()
    {
        InitializeComponent();
    }

    private void OnDeveloperClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/stevesensei") { UseShellExecute = true });
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception)
        {
            Toast.Show(this, "无法打开浏览器", ToastKind.Error, "请手动访问 https://github.com/stevesensei。");
        }
    }
}