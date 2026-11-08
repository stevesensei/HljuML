using HljuML.Core.Model;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

/// <summary>确认窗口持有的包快照；取消时释放，提交成功后由安装队列负责释放。</summary>
public sealed class ModpackImportSession : IDisposable
{
    private readonly object _sync = new();
    private readonly ILogger _logger;
    private string? _archive;
    private bool _disposed;

    internal ModpackImportSession(string archive, ModpackInfo info, ILogger logger)
    { _archive = archive; Info = info; _logger = logger; }

    public ModpackInfo Info { get; }
    public string SuggestedName => $"{Info.Name}-{Info.PackVersion}";

    /// <summary>暂时移交快照所有权，防止重复提交或窗口关闭时误删正在排队的包。</summary>
    internal string ClaimArchive()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var archive = _archive ?? throw new InvalidOperationException("整合包正在提交或已提交。");
            _archive = null;
            return archive;
        }
    }

    /// <summary>名称校验失败等情况下归还快照；已关闭的窗口不再保留文件。</summary>
    internal void ReturnArchive(string archive)
    {
        lock (_sync)
        {
            if (!_disposed) { _archive = archive; return; }
        }
        ModpackImportService.ReleaseArchive(archive, _logger);
    }

    /// <summary>只清理仍属于确认窗口的快照，安装队列持有的文件不受影响。</summary>
    public void Dispose()
    {
        string? archive;
        lock (_sync) { _disposed = true; archive = _archive; _archive = null; }
        if (archive is not null) ModpackImportService.ReleaseArchive(archive, _logger);
    }
}
