using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HljuML.App.Core;
using TKCode.Arc;

namespace HljuML.App.Views;

public partial class JavaSettingsView : UserControl
{
    public JavaSettingsView()
    {
        InitializeComponent();
    }

    private async void OnJavaSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is JavaSettingsViewModel viewModel
            && sender is Select select
            && ReferenceEquals(select.SelectedItem, JavaSettingsViewModel.BrowseJavaOption))
        {
            await viewModel.ChooseJavaAsync(SelectJavaFileAsync);
        }
    }

    private async Task<string?> SelectJavaFileAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || !storage.CanOpen)
        {
            throw new NotSupportedException("当前环境无法打开文件选择器，请在桌面环境中重试。");
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Java 可执行文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Java 可执行文件")
                {
                    Patterns = OperatingSystem.IsWindows() ? ["javaw.exe", "java.exe"] : ["java"]
                }
            ]
        });
        if (files.Count == 0)
        {
            return null;
        }

        using var file = files[0];
        return file.TryGetLocalPath()
            ?? throw new ArgumentException("请选择本机上的 Java 可执行文件。");
    }

    private async void OnScanJavaClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is JavaSettingsViewModel viewModel)
        {
            await viewModel.ScanAsync();
        }
    }
}