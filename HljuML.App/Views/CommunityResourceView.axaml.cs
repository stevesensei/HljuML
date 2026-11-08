using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HljuML.App.Core;

namespace HljuML.App.Views;

public partial class CommunityResourceView : UserControl
{
    public CommunityResourceView()
    {
        InitializeComponent();
    }

    /// <summary>UI 只负责选文件，校验和安装由视图模型及导入服务处理。</summary>
    private async void OnImportModpackClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommunityResourceViewModel viewModel && TopLevel.GetTopLevel(this) is MainWindow window)
        {
            await viewModel.ImportModpackAsync(ChooseModpackAsync, file => window.ImportModpacksAsync([file]));
        }
    }

    /// <summary>通过系统文件选择器获取本地路径；取消选择不创建安装任务。</summary>
    private async Task<string?> ChooseModpackAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || !storage.CanOpen)
        {
            throw new NotSupportedException("当前环境无法打开文件选择器。");
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入 Modrinth / CurseForge 整合包", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Minecraft 整合包") { Patterns = ["*.mrpack", "*.zip"] }]
        });
        if (files.Count == 0)
        {
            return null;
        }

        using var file = files[0];
        return file.TryGetLocalPath() ?? throw new ArgumentException("请选择本地整合包文件。");
    }

    private async void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is CommunityResourceViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private void OnCreateVanillaClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommunityResourceViewModel viewModel)
        {
            viewModel.RequestVanillaCreation();
        }
    }

    private void OnBackToResultsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommunityResourceViewModel viewModel)
        {
            viewModel.ClearSelection();
        }
    }
}