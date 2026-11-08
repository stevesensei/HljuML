using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using HljuML.App.Core;
using TKCode.Arc;

namespace HljuML.App;

public partial class MainWindow
{
    private bool _importingModpacks;

    /// <summary>主窗口的任何页面均可接收包文件；模态框打开时禁止叠加导入。</summary>
    private void OnModpackDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File)) return;
        var files = e.DataTransfer.TryGetFiles();
        e.DragEffects = !_importingModpacks && !SelectionScrim.IsVisible
            && files is { Length: > 0 } && files.All(IsModpackFile) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>文件内容由服务解析，扩展名只用于判断拖入是否适用；不接受目录或远程路径。</summary>
    private async void OnModpackDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File)) return;
        e.Handled = true;
        var files = e.DataTransfer.TryGetFiles();
        if (_importingModpacks || SelectionScrim.IsVisible || files is not { Length: > 0 }) return;
        if (!files.All(IsModpackFile))
        {
            Toast.Show(this, "无法导入", ToastKind.Error, "请拖入本地 .mrpack 或整合包 .zip 文件。");
            return;
        }
        await ImportModpacksAsync(files.Select(file => file.TryGetLocalPath()!).ToArray());
    }

    /// <summary>拖入和文件选择器共享的唯一入口；多个包逐个确认，不同时创建多个模态框。</summary>
    internal async Task ImportModpacksAsync(IReadOnlyList<string> files)
    {
        if (_closed || _importingModpacks || DataContext is not MainViewModel viewModel) return;
        _importingModpacks = true;
        try
        {
            foreach (var file in files.Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
            {
                if (_closed) break;
                await viewModel.ModpackImport.OpenAsync(file);
            }
        }
        finally { _importingModpacks = false; }
    }

    private static bool IsModpackFile(IStorageItem file) => file is IStorageFile
        && file.TryGetLocalPath() is { } path && Path.IsPathFullyQualified(path)
        && Path.GetExtension(path).ToLowerInvariant() is ".mrpack" or ".zip";
}
