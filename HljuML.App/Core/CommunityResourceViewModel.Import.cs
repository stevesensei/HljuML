using System;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using HljuML.Core.GamePlay;
using HljuML.Shared.Model;

namespace HljuML.App.Core;

public sealed partial class CommunityResourceViewModel
{
    private readonly ModpackImportService _imports;
    public bool IsImporting { get; private set; }
    public string? ImportError { get; private set; }
    public bool HasImportError => ImportError is not null;

    /// <summary>选择本地整合包并提交安装队列；任务创建事件会打开已有下载管理器。</summary>
    public async Task ImportModpackAsync(Func<Task<string?>> chooseFile)
    {
        if (_disposed || IsImporting) return;
        IsImporting = true;
        ImportError = null;
        NotifyImportState();
        try
        {
            var file = await chooseFile();
            if (file is null || _disposed) return;
            var result = await _imports.EnqueueAsync(file, cancellationToken: _stopping);
            if (!_disposed && result.Status != ServiceStatus.Success) ImportError = result.Message;
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or DbException)
        { ImportError = $"无法导入整合包：{error.Message}"; }
        finally
        {
            IsImporting = false;
            if (!_disposed) NotifyImportState();
        }
    }

    /// <summary>在 UI 线程统一更新导入按钮和错误提示。</summary>
    private void NotifyImportState()
    {
        Notify(nameof(IsImporting));
        Notify(nameof(ImportError));
        Notify(nameof(HasImportError));
    }
}
