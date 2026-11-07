using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data.Common;
using ColorMC.Core;
using ColorMC.Core.Config;
using ColorMC.Core.Game;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Net;
using ColorMC.Core.Objs;
using HljuML.Core.GamePlay.Auth;
using HljuML.Core.Infrastructure.Downloads;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Core.Model.Entity;
using HljuML.Shared.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

public sealed class GameInstanceService(IServiceScopeFactory scopeFactory, JavaEnvService javaEnvironments,
    DownloadManagementService downloads, ColorMcOperationCoordinator coordinator,
    ILogger<GameInstanceService> logger) : IHostedService
{
    private readonly ConcurrentDictionary<Guid, RunningProcess> _running = new();

    public event Action? RunningInstancesChanged;

    public IReadOnlyList<RunningGameInstance> GetRunningInstances() =>
        _running.Values.Select(process => process.Instance).OrderBy(instance => instance.ProcessId).ToArray();

    public async Task<IReadOnlyList<GameInstanceData>> GetGameInstances(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HljuMLDbContext>();
        var instances = await db.GameInstances.AsNoTracking().Include(instance => instance.GameFolder)
            .OrderByDescending(instance => instance.LastLogin)
            .ThenBy(instance => instance.Name).ThenBy(instance => instance.Id).ToListAsync(cancellationToken);
        return instances.Where(instance => !File.Exists(Path.Combine(
            instance.GameFolder?.AbsolutePath ?? LauncherPaths.DefaultInstanceDirectory,
            $"hljuml-{instance.Id:N}", GameInstallationService.PendingMarker))).ToArray();
    }

    public async Task<IReadOnlyList<GameFolder>> GetGameFolders(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HljuMLDbContext>();
        return await db.GameFolders.AsNoTracking().OrderBy(folder => folder.Name).ThenBy(folder => folder.AbsolutePath)
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<GameFolder>> AddGameFolderAsync(string path, CancellationToken token = default)
    {
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path))
            return ServiceResult<GameFolder>.FailureResult(message: "请选择可访问的游戏文件夹。");

        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        try
        {
            using var operation = await coordinator.EnterAsync(token);
            var games = await Task.Run(() => ColorMcInstanceFolder.ReadInstances(path, token).ToArray(), token);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HljuMLDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            var folders = await db.GameFolders.ToListAsync(token);
            var folder = folders.FirstOrDefault(item => LauncherPaths.GameFolderPathsEqual(item.AbsolutePath, path));
            if (folder is null)
            {
                var name = LauncherPaths.IsDefaultInstanceDirectory(path) ? "默认实例目录" : Path.GetFileName(path);
                folder = new GameFolder { Id = Guid.NewGuid(), AbsolutePath = path,
                    Name = string.IsNullOrEmpty(name) ? path : name };
                db.GameFolders.Add(folder);
            }

            var registered = (await db.GameInstances.Select(instance => instance.Id).ToListAsync(token)).ToHashSet();
            foreach (var group in games.GroupBy(game => game.UUID).Where(group => group.Count() == 1))
            {
                var game = group.First();
                if (!registered.Add(game.UUID))
                    continue;

                db.GameInstances.Add(new GameInstanceData { Id = game.UUID, GameFolderId = folder.Id,
                    Name = string.IsNullOrWhiteSpace(game.Name) ? Path.GetFileName(game.DirName) : game.Name,
                    Version = game.Version, Loader = game.Loader });
            }

            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return ServiceResult<GameFolder>.SuccessResult(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DbException or DbUpdateException)
        {
            logger.LogWarning(error, "Failed to register a game folder");
            return ServiceResult<GameFolder>.FailureResult(message: "无法添加目录，请检查目录权限后重试。");
        }
    }

    public async Task<ServiceResult<int?>> LaunchGameInstance(Guid profileId, Guid instanceId,
        CancellationToken stoppingToken = default, IProgress<GameLaunchProgress>? progress = null)
    {
        try
        {
            using var operation = await coordinator.EnterAsync(stoppingToken);
            if (_running.ContainsKey(instanceId))
                return ServiceResult<int?>.FailureResult(message: "该实例已经在运行。");

            progress?.Report(new("正在读取实例和玩家档案"));
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HljuMLDbContext>();
            var profiles = scope.ServiceProvider.GetRequiredService<PlayerProfileService>();
            var instanceData = await db.GameInstances.AsNoTracking().Include(item => item.GameFolder)
                .SingleOrDefaultAsync(item => item.Id == instanceId, stoppingToken);
            if (instanceData is null || instanceId == Guid.Empty)
                return ServiceResult<int?>.FailureResult(message: "找不到所选游戏实例。");

            var profileResult = await profiles.GetPlayerProfile(profileId, stoppingToken);
            if (profileResult.Data is not { } profile)
                return ServiceResult<int?>.FailureResult(message: profileResult.Message);

            if (profile.AuthMethod == AuthMethod.Custom)
                progress?.Report(new("正在刷新外置登录"));
            var loginResult = await profiles.GetLaunchLogin(profile, stoppingToken);
            if (loginResult.Data is not { } login)
                return ServiceResult<int?>.FailureResult(message: loginResult.Message);

            var gameResult = await ResolveGame(instanceData, progress, stoppingToken);
            if (gameResult.Data is not { } game)
                return ServiceResult<int?>.FailureResult(message: gameResult.Message);

            using var resources = instanceData.GameFolder is { } folder
                && ColorMcInstanceFolder.GetResourceDirectory(folder.AbsolutePath) is { } resourceDirectory
                ? MinecraftPath.UseResourceDirectory(resourceDirectory)
                : null;

            if (string.IsNullOrWhiteSpace(game.JvmLocal)
                && string.IsNullOrWhiteSpace(game.JvmName))
            {
                if (javaEnvironments.DefaultJavaPath is { Length: > 0 } defaultJava)
                {
                    if (!File.Exists(defaultJava))
                        return ServiceResult<int?>.FailureResult(message: "默认 Java 已不可用，请在设置中重新扫描并选择。");
                    game = game with { JvmLocal = defaultJava };
                }
                else if (game.Loader == Loaders.Normal)
                {
                    progress?.Report(new("正在匹配 Java 运行环境"));
                    var java = await javaEnvironments.FindForMinecraftVersionAsync(game.Version, stoppingToken);
                    if (java is not null)
                        game = game with { JvmLocal = java.Path };
                }
            }

            var reporter = progress is null ? null : new GameLaunchReporter(progress);
            var args = new GameLaunchArg { Auth = login, Gui = reporter };
            var trackRefresh = profile.AuthMethod != AuthMethod.Offline;
            var refreshed = false;
            void OnGameLog(GameLogEventArgs evt)
            {
                // StartGameAsync refreshes the token before starting the process and emits LoginTime after success.
                if (evt.Game.UUID == instanceId && evt.LogItem?.LogType == GameSystemLog.LoginTime)
                    refreshed = true;
            }

            if (trackRefresh)
                ColorMCCore.GameLog += OnGameLog;

            GameHandle? handle;
            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                handle = await downloads.TrackGameDownloadsAsync(instanceId, instanceData.Name,
                    () => game.StartGameAsync(args, stoppingToken), progress, stoppingToken);
            }
            finally
            {
                if (trackRefresh)
                {
                    ColorMCCore.GameLog -= OnGameLog;
                    if (refreshed)
                        await SaveLoginAfterLaunch(profiles, profile, args);
                }
            }

            if (handle is null)
            {
                stoppingToken.ThrowIfCancellationRequested();
                return ServiceResult<int?>.FailureResult(message: "游戏进程未启动。");
            }

            var instance = new RunningGameInstance(instanceId, profile.Id, handle.Process.Id, instanceData.Name, args.Auth.UserName,
                PlayerProfileService.DescribeAuthentication(profile));
            var running = new RunningProcess(instance, handle);
            handle.Process.Exited += (_, _) => RemoveRunningProcess(running);
            _running[instance.Id] = running;

            if (handle.IsExit)
            {
                RemoveRunningProcess(running);
                return ServiceResult<int?>.FailureResult(message: "游戏进程在启动后立即退出。");
            }

            RunningInstancesChanged?.Invoke();
            await SaveLastPlayed(db, instanceId, profileId);
            return ServiceResult<int?>.SuccessResult(instance.ProcessId, "游戏实例启动成功");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return ServiceResult<int?>.FailureResult(message: "启动已取消。");
        }
        catch (LaunchException e)
        {
            return ServiceResult<int?>.FailureResult(message: e.State switch
            {
                LaunchError.AuthLoginFail => "玩家认证失败，请重新登录。",
                LaunchError.JavaNotFound  => "找不到所需的 Java，请配置兼容的运行环境。",
                LaunchError.SelectJavaNotFound => "找不到所选的 Java，请检查运行环境配置。",
                LaunchError.DownloadFileError => "游戏文件下载失败，请检查网络后重试。",
                LaunchError.VersionError => "实例缺少可用的版本或加载器配置。",
                _ => $"启动失败（{e.State}）。"
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to launch instance {InstanceId}", instanceId);
            return ServiceResult<int?>.InternalErrorResult(message: "启动实例时发生错误。");
        }
    }

    private static async Task<ServiceResult<GameSettingObj>> ResolveGame(GameInstanceData instance,
        IProgress<GameLaunchProgress>? progress, CancellationToken cancellationToken)
    {
        if (File.Exists(Path.Combine(instance.GameFolder?.AbsolutePath ?? LauncherPaths.DefaultInstanceDirectory,
            $"hljuml-{instance.Id:N}", GameInstallationService.PendingMarker)))
            return ServiceResult<GameSettingObj>.FailureResult(message: "实例尚未完成安装登记，请检查下载管理器。");
        if (instance.GameFolder is { } folder && !LauncherPaths.IsDefaultInstanceDirectory(folder.AbsolutePath))
            return await ColorMcInstanceFolder.ResolveAsync(instance, folder.AbsolutePath, progress, cancellationToken);

        if (InstancesPath.GetGame(instance.Id) is { } existing)
        {
            return existing.Version == instance.Version && existing.Loader == instance.Loader
                ? ServiceResult<GameSettingObj>.SuccessResult(existing)
                : ServiceResult<GameSettingObj>.FailureResult(message: "实例记录与 ColorMC 的版本或加载器配置不一致。");
        }

        if (Directory.Exists(LauncherPaths.DefaultInstanceDirectory)
            && ColorMcInstanceFolder.ReadInstances(LauncherPaths.DefaultInstanceDirectory, cancellationToken)
                .Any(game => game.UUID == instance.Id))
            return await ColorMcInstanceFolder.ResolveAsync(instance, LauncherPaths.DefaultInstanceDirectory,
                progress, cancellationToken);

        if (instance.Loader != Loaders.Normal)
            return ServiceResult<GameSettingObj>.FailureResult(message: "请为此加载器准备同 ID 的 ColorMC 实例配置。");
        if (string.IsNullOrWhiteSpace(instance.Version))
            return ServiceResult<GameSettingObj>.FailureResult(message: "请填写实例的原版游戏版本。");

        var directory = $"hljuml-{instance.Id:N}";
        var newGame = new GameSettingObj
        {
            UUID = instance.Id,
            Name = directory,
            DirName = directory,
            Version = instance.Version,
            Loader = instance.Loader
        };
        if (Path.Exists(newGame.GetBasePath()) || InstancesPath.Games.Any(item =>
            string.Equals(item.DirName, directory, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Name, directory, StringComparison.OrdinalIgnoreCase)))
        {
            return ServiceResult<GameSettingObj>.FailureResult(message: "实例目录已存在，请检查 ColorMC 实例配置。");
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new("正在初始化原版实例"));
        var created = await InstancesPath.CreateGameAsync(newGame, null);
        return created?.UUID == instance.Id
            ? ServiceResult<GameSettingObj>.SuccessResult(created)
            : ServiceResult<GameSettingObj>.FailureResult(message: "无法创建对应的 ColorMC 实例。");
    }

    private async Task SaveLoginAfterLaunch(PlayerProfileService profiles, PlayerProfile profile, GameLaunchArg args)
    {
        try
        {
            if (!await profiles.SaveRefreshedLogin(profile, args.Auth, CancellationToken.None))
                logger.LogWarning("Could not save refreshed login for profile {ProfileId}", profile.Id);
        }
        catch (DbException e)
        {
            logger.LogWarning(e, "Could not save refreshed login for profile {ProfileId}", profile.Id);
        }
    }

    private async Task SaveLastPlayed(HljuMLDbContext db, Guid instanceId, Guid profileId)
    {
        try
        {
            var playedAt = DateTime.UtcNow;
            await db.GameInstances.Where(item => item.Id == instanceId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LastLogin, playedAt)
                    .SetProperty(item => item.LastUsedPlayerId, item =>
                        db.PlayerProfiles.Any(profile => profile.Id == profileId) ? profileId : item.LastUsedPlayerId));
        }
        catch (DbException e)
        {
            logger.LogWarning(e, "Could not save last played data for instance {InstanceId}", instanceId);
        }
    }

    public ServiceResult<object> StopGameInstance(RunningGameInstance instance)
    {
        if (!_running.TryGetValue(instance.Id, out var running) || !ReferenceEquals(running.Instance, instance))
        {
            return ServiceResult<object>.SuccessResult(message: "实例已退出。");
        }

        try
        {
            if (!running.Handle.Process.CloseMainWindow())
            {
                running.Handle.Kill();
            }

            return ServiceResult<object>.SuccessResult(message: "已请求结束实例。");
        }
        catch (InvalidOperationException) when (running.Handle.IsExit)
        {
            return ServiceResult<object>.SuccessResult(message: "实例已退出。");
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return ServiceResult<object>.FailureResult(message: $"无法结束实例：{e.Message}");
        }
    }

    private void RemoveRunningProcess(RunningProcess process)
    {
        if (_running.TryRemove(new KeyValuePair<Guid, RunningProcess>(process.Instance.Id, process)))
        {
            RunningInstancesChanged?.Invoke();
        }
    }

    private sealed record RunningProcess(RunningGameInstance Instance, GameHandle Handle);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        //核心初始化
        ColorMCCore.Init(new()
        {
            Local = LauncherPaths.ColorMcDirectory,
        });
        
        var http = ConfigLoad.Config.Http;
        http.Source = SourceLocal.BMCLAPI;
        http.DownloadThread = 8;
        CoreHttpClient.Source = http.Source;
        ConfigLoad.Save(); // 保存到 Core 数据目录下的 config.json
        
        ColorMCCore.Init1();
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        ColorMCCore.Close();
        return Task.CompletedTask;
    }
}

public sealed record RunningGameInstance(
    Guid Id,
    Guid ProfileId,
    int ProcessId,
    string InstanceName,
    string PlayerName,
    string AuthenticationMethod);
