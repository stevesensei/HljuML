using ColorMC.Core;
using ColorMC.Core.Objs;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.Infrastructure.Downloads;

public sealed partial class DownloadManagementService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        ColorMCCore.Error += LogColorMcError;
        try { await base.StartAsync(cancellationToken); }
        catch
        {
            ColorMCCore.Error -= LogColorMcError;
            throw;
        }
    }

    public override void Dispose()
    {
        ColorMCCore.Error -= LogColorMcError;
        base.Dispose();
    }

    private void LogColorMcError(CoreErrorEventArgs error)
    {
        switch (error)
        {
            case DownloadExceptionErrorEventArgs failure:
                logger.LogWarning(failure.Exception,
                    "下载异常：{FileName}；来源 {Source}；目标 {Destination}；累计异常次数 {FailureCount}；已下载 {DownloadedBytes}/{TotalBytes} 字节。ColorMC 将按内部策略重试或切换下载源。",
                    failure.File.Name, GetLogSource(failure.File.Url), failure.File.Local,
                    failure.File.ErrorTime, failure.File.NowSize, failure.File.AllSize);
                break;
            case DownloadSizeErrorEvnetArgs failure:
                logger.LogError(
                    "下载大小校验失败：{FileName}；来源 {Source}；目标 {Destination}；预期 {ExpectedBytes} 字节，实际 {ActualBytes} 字节；累计异常次数 {FailureCount}",
                    failure.File.Name, GetLogSource(failure.File.Url), failure.File.Local,
                    failure.File.AllSize, failure.Size, failure.File.ErrorTime);
                break;
            case DownloadHashErrorEventArgs failure:
                logger.LogError(
                    "下载哈希校验失败：{FileName}；来源 {Source}；目标 {Destination}；预期 {ExpectedHash}，实际 {ActualHash}；累计异常次数 {FailureCount}",
                    failure.File.Name, GetLogSource(failure.File.Url), failure.File.Local,
                    failure.Hash, failure.Now, failure.File.ErrorTime);
                break;
        }
    }

    private static string GetLogSource(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) return "无效下载地址";
        return new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
    }
}
