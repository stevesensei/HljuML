using System.Threading.Channels;
using ColorMC.Core.Objs;
using HljuML.Core.Infrastructure.Downloads;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Shared.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

public sealed record GameInstallationRequest(string Name, string Version,
    Loaders Loader = Loaders.Normal, string? LoaderVersion = null);

public sealed partial class GameInstallationService(IServiceScopeFactory scopeFactory,
    DownloadManagementService downloads, ColorMcOperationCoordinator coordinator,
    ILogger<GameInstallationService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _enqueueGate = new(1, 1);
    private readonly HashSet<string> _reservedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<Installation> _queue = Channel.CreateUnbounded<Installation>();
    public event Action? Installed;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        using (await coordinator.EnterAsync(cancellationToken))
            await RecoverAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public async Task<ServiceResult<Guid>> EnqueueAsync(GameInstallationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 128
            || request.Name.Any(char.IsControl))
            return ServiceResult<Guid>.FailureResult(message: "请输入有效的实例名称（最多 128 个字符）。");
        if (string.IsNullOrWhiteSpace(request.Version) || request.Version.Length > 128
            || request.Version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || request.Version.IndexOfAny(['/', '\\']) >= 0 || request.Version.Any(char.IsControl)
            || request.Version is "." or "..")
            return ServiceResult<Guid>.FailureResult(message: "请选择有效的 Minecraft 版本。");
        if (request.Loader is not (Loaders.Normal or Loaders.Forge or Loaders.NeoForge or Loaders.Fabric or Loaders.OptiFine)
            || request.Loader != Loaders.Normal && string.IsNullOrWhiteSpace(request.LoaderVersion))
            return ServiceResult<Guid>.FailureResult(message: "请选择受支持的加载器版本。");
        if (request.LoaderVersion is { } loaderVersion && (loaderVersion.Length > 128
            || loaderVersion.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || loaderVersion.IndexOfAny(['/', '\\']) >= 0 || loaderVersion.Any(char.IsControl)
            || loaderVersion is "." or ".."))
            return ServiceResult<Guid>.FailureResult(message: "加载器版本包含无效字符。");

        request = request with { Name = request.Name.Trim() };
        using var operation = await coordinator.EnterAsync(cancellationToken);
        await _enqueueGate.WaitAsync(cancellationToken);
        try
        {
            if (_reservedNames.Contains(request.Name) || await NameExistsAsync(request.Name, cancellationToken))
                return ServiceResult<Guid>.FailureResult(message: "默认目录中已有同名实例或安装任务，请更换名称。");
            var instanceId = Guid.NewGuid();
            var operationId = downloads.CreateOperation($"安装 {request.Name}", instanceId);
            _reservedNames.Add(request.Name);
            if (!_queue.Writer.TryWrite(new Installation(operationId, instanceId, request)))
            {
                _reservedNames.Remove(request.Name);
                downloads.CompleteOperation(operationId, DownloadStatus.Failed, "安装服务已停止。");
                return ServiceResult<Guid>.FailureResult(message: "安装服务已停止。");
            }
            return ServiceResult<Guid>.SuccessResult(operationId);
        }
        finally { _enqueueGate.Release(); }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var installation in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using (await coordinator.EnterAsync(stoppingToken))
                    {
                        await downloads.TrackOperationAsync(installation.OperationId, async () =>
                        {
                            await InstallAsync(installation, stoppingToken);
                            return true;
                        }, stoppingToken);
                    }
                    downloads.CompleteOperation(installation.OperationId, DownloadStatus.Succeeded);
                    if (downloads.GetDownload(installation.OperationId)?.Status == DownloadStatus.Succeeded)
                        NotifyInstalled();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    downloads.CompleteOperation(installation.OperationId, DownloadStatus.Canceled, "安装已取消。");
                }
                catch (Exception error)
                {
                    logger.LogError(error, "Installation failed for {InstanceId}", installation.InstanceId);
                    downloads.CompleteOperation(installation.OperationId, DownloadStatus.Failed, error.Message);
                }
                finally
                {
                    await _enqueueGate.WaitAsync(CancellationToken.None);
                    try { _reservedNames.Remove(installation.Request.Name); }
                    finally { _enqueueGate.Release(); }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            while (_queue.Reader.TryRead(out var pending))
                downloads.CompleteOperation(pending.OperationId, DownloadStatus.Canceled, "应用已退出，安装未开始。");
        }
    }

    private void NotifyInstalled()
    {
        foreach (var handler in Installed?.GetInvocationList() ?? [])
        {
            try { ((Action)handler)(); }
            catch (Exception error) { logger.LogWarning(error, "Installation observer failed"); }
        }
    }

    private async Task<bool> NameExistsAsync(string name, CancellationToken token)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HljuMLDbContext>();
        var rows = await db.GameInstances.AsNoTracking().Include(item => item.GameFolder).ToListAsync(token);
        if (rows.Any(item => (item.GameFolder is null || LauncherPaths.IsDefaultInstanceDirectory(item.GameFolder.AbsolutePath))
            && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            return true;
        return Directory.Exists(LauncherPaths.DefaultInstanceDirectory)
            && ColorMcInstanceFolder.ReadInstances(LauncherPaths.DefaultInstanceDirectory, token)
                .Any(game => string.Equals(game.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record Installation(Guid OperationId, Guid InstanceId, GameInstallationRequest Request);
}
