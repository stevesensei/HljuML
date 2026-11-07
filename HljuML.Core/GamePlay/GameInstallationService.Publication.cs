using System.Text.Json;
using ColorMC.Core;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Objs;
using ColorMC.Core.Utils;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Core.Model.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

public sealed partial class GameInstallationService
{
    internal const string PendingMarker = ".hljuml-install.pending.json";
    private const string HiddenGameFile = ".hljuml-game.pending.json";

    private async Task InstallAsync(Installation installation, CancellationToken token)
    {
        var root = LauncherPaths.DefaultInstanceDirectory;
        Directory.CreateDirectory(root);
        var stage = Path.Combine(root, $".hljuml-install-{installation.InstanceId:N}");
        var final = Path.Combine(root, $"hljuml-{installation.InstanceId:N}");
        EnsureInstancePath(root, stage);
        EnsureInstancePath(root, final);
        if (Path.Exists(stage) || Path.Exists(final))
            throw new IOException("实例的安装目录已存在，未覆盖任何文件。");
        Directory.CreateDirectory(stage);
        await WriteNewJsonAsync(Path.Combine(stage, PendingMarker),
            new PendingInstallation(installation.OperationId, installation.InstanceId, []), token);
        var game = new GameSettingObj
        {
            UUID = installation.InstanceId, Name = installation.Request.Name, DirName = Path.GetFileName(final),
            Version = installation.Request.Version, Loader = installation.Request.Loader,
            LoaderVersion = installation.Request.LoaderVersion,
            Encoding = OperatingSystem.IsWindows() ? LogEncoding.GBK : default,
            Mods = [], CustomJson = [], LaunchData = new() { AddTime = DateTime.Now }
        };
        IReadOnlyList<Artifact> artifacts;
        try { artifacts = await PrepareFilesAsync(installation.OperationId, game, token); }
        catch
        {
            try
            {
                if (await IsOwnedAsync(stage, installation.InstanceId)
                    && Directory.EnumerateFileSystemEntries(stage).All(path => Path.GetFileName(path) == PendingMarker))
                {
                    File.Delete(Path.Combine(stage, PendingMarker));
                    Directory.Delete(stage);
                }
            }
            catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException)
            { logger.LogWarning(cleanupError, "Could not remove empty failed installation staging directory {Directory}", stage); }
            throw;
        }
        downloads.SetOperationPhase(installation.OperationId, "正在校验全部必需文件");
        foreach (var artifact in artifacts)
            await RequireValidAsync(artifact, token);
        var marker = new PendingInstallation(installation.OperationId, installation.InstanceId, artifacts);
        var markerUpdate = Path.Combine(stage, PendingMarker + ".tmp");
        await WriteNewJsonAsync(markerUpdate, marker, token);
        File.Move(markerUpdate, Path.Combine(stage, PendingMarker), overwrite: true);
        await using (var stream = new FileStream(Path.Combine(stage, HiddenGameFile), FileMode.CreateNew, FileAccess.Write))
            await JsonSerializer.SerializeAsync(stream, game, JsonType.GameSettingObj, token);
        await PublishAsync(installation, game, stage, final, token);
    }

    private async Task PublishAsync(Installation installation, GameSettingObj game, string stage, string final,
        CancellationToken token)
    {
        downloads.SetOperationPhase(installation.OperationId, "正在发布实例并登记数据库");
        EnsureInstancePath(LauncherPaths.DefaultInstanceDirectory, stage, requireExisting: true);
        EnsureInstancePath(LauncherPaths.DefaultInstanceDirectory, final);
        if (await NameExistsAsync(game.Name, token) || Path.Exists(final))
            throw new IOException("发布前发现同名实例或目标目录冲突，未覆盖现有实例。");
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HljuMLDbContext>();
        if (await db.GameInstances.AnyAsync(item => item.Id == game.UUID, token))
            throw new IOException("实例 UUID 已登记，未覆盖现有实例。");
        var moved = false;
        var committed = false;
        InstancesPath.DisableWatcher = true;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            var folders = await db.GameFolders.ToListAsync(token);
            var folder = folders.FirstOrDefault(item => LauncherPaths.IsDefaultInstanceDirectory(item.AbsolutePath));
            if (folder is null)
            {
                folder = new GameFolder { Id = Guid.NewGuid(), Name = "默认实例目录",
                    AbsolutePath = LauncherPaths.DefaultInstanceDirectory };
                db.GameFolders.Add(folder);
            }
            File.Move(Path.Combine(stage, HiddenGameFile), Path.Combine(stage, Names.NameGameFile));
            Directory.Move(stage, final);
            moved = true;
            db.GameInstances.Add(new GameInstanceData { Id = game.UUID, Name = game.Name,
                Version = game.Version, Loader = game.Loader, GameFolderId = folder.Id });
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            committed = true;
            await DeleteCommittedMarkerAsync(Path.Combine(final, PendingMarker));
        }
        catch
        {
            if (!committed)
            {
                var owned = moved ? final : stage;
                try
                {
                    if (await IsOwnedAsync(owned, installation.InstanceId))
                    {
                        var config = Path.Combine(owned, Names.NameGameFile);
                        if (File.Exists(config))
                            File.Move(config, Path.Combine(owned, HiddenGameFile));
                        if (moved && !Path.Exists(stage))
                            Directory.Move(final, stage);
                    }
                }
                catch (Exception rollbackError) when (rollbackError is IOException or UnauthorizedAccessException)
                {
                    logger.LogError(rollbackError, "Publication rollback retained pending marker for {InstanceId}", game.UUID);
                }
            }
            throw;
        }
        finally { InstancesPath.DisableWatcher = false; }
    }

    private async Task RecoverAsync(CancellationToken token)
    {
        var root = LauncherPaths.DefaultInstanceDirectory;
        if (!Directory.Exists(root))
            return;
        EnsureInstancePath(root, Path.Combine(root, ".hljuml-install-validation"));
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HljuMLDbContext>();
        foreach (var directory in Directory.EnumerateDirectories(root, "*", new EnumerationOptions
        { AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            var markerFile = Path.Combine(directory, PendingMarker);
            if (!File.Exists(markerFile))
                continue;
            token.ThrowIfCancellationRequested();
            try
            {
                var marker = JsonSerializer.Deserialize<PendingInstallation>(await File.ReadAllTextAsync(markerFile, token));
                if (marker is null || marker.InstanceId == Guid.Empty
                    || Path.GetFileName(directory) is not { } name
                    || name != $"hljuml-{marker.InstanceId:N}" && name != $".hljuml-install-{marker.InstanceId:N}")
                    continue;
                var record = await db.GameInstances.AsNoTracking().Include(item => item.GameFolder)
                    .SingleOrDefaultAsync(item => item.Id == marker.InstanceId, token);
                var gameFile = Path.Combine(directory, Names.NameGameFile);
                var game = File.Exists(gameFile)
                    ? JsonSerializer.Deserialize(await File.ReadAllTextAsync(gameFile, token), JsonType.GameSettingObj) : null;
                if (record is not null && game?.UUID == record.Id && game.Version == record.Version
                    && game.Loader == record.Loader && record.GameFolder is { } folder
                    && LauncherPaths.IsDefaultInstanceDirectory(folder.AbsolutePath)
                    && name == $"hljuml-{marker.InstanceId:N}" && marker.Artifacts.Count > 0)
                {
                    foreach (var artifact in marker.Artifacts)
                        await RequireValidAsync(artifact, token);
                    File.Delete(markerFile);
                    continue;
                }
                if (File.Exists(gameFile) && !File.Exists(Path.Combine(directory, HiddenGameFile)))
                    File.Move(gameFile, Path.Combine(directory, HiddenGameFile));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                logger.LogWarning(error, "Retained incomplete installation at {Directory}", directory);
            }
        }
    }

    private static async Task<bool> IsOwnedAsync(string directory, Guid instanceId)
    {
        try { EnsureInstancePath(LauncherPaths.DefaultInstanceDirectory, directory, requireExisting: true); }
        catch (IOException) { return false; }
        var marker = Path.Combine(directory, PendingMarker);
        return File.Exists(marker)
            && JsonSerializer.Deserialize<PendingInstallation>(await File.ReadAllTextAsync(marker))?.InstanceId == instanceId;
    }

    private static async Task WriteNewJsonAsync<T>(string file, T value, CancellationToken token)
    {
        await using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write);
        await JsonSerializer.SerializeAsync(stream, value, cancellationToken: token);
        await stream.FlushAsync(token);
    }

    private static async Task DeleteCommittedMarkerAsync(string marker)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                File.Delete(marker);
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                lastError = error;
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)));
            }
        }
        throw new IOException("实例已登记，但发布标记暂时无法清理；实例将在下次启动恢复后可用。", lastError);
    }

    private static void EnsureInstancePath(string root, string path, bool requireExisting = false)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullPath = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(fullPath), fullRoot, comparison))
            throw new IOException("实例路径超出默认实例目录。");
        if (!Directory.Exists(fullRoot) || (File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("默认实例目录不可用或为重定向目录。");
        if (Path.Exists(fullPath))
        {
            if (!Directory.Exists(fullPath) || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("实例目录不是安全的本地目录。");
        }
        else if (requireExisting)
        {
            throw new IOException("实例暂存目录已不存在。");
        }
    }

    private sealed record PendingInstallation(Guid OperationId, Guid InstanceId, IReadOnlyList<Artifact> Artifacts);
}
