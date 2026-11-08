using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HljuML.App.Views;
using HljuML.Core.GamePlay;
using HljuML.Core.GamePlay.Auth;
using HljuML.Core.Infrastructure.Downloads;
using HljuML.Core.Model.Entity;
using HljuML.Shared.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HljuML.Core.Infrastructure.Settings;

namespace HljuML.App.Core;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly GameInstanceService _gameInstances;
    private readonly GameInstallationService _installations;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IServiceScopeFactory _scopeFactory;
    private IReadOnlyList<ProfileOption> _profiles = [];
    private GameInstanceData? _selectedInstance;
    private ProfileOption? _selectedProfile;
    private readonly Dictionary<Guid, Bitmap> _playerAvatars = new();
    private readonly HashSet<Guid> _avatarLoads = new();
    private bool _disposed;
    private Guid? _preferredProfileId;
    private bool _isLoading;
    private bool _isLaunching;
    private bool _installedRefreshPending;
    private int _launchAttempt;
    private string? _optionsError;
    private GameLaunchProgress? _progress;

    public MainViewModel(GameInstanceService gameInstances, IHostApplicationLifetime appLifetime,
        IServiceScopeFactory scopeFactory, DownloadManagementService downloads, JavaEnvService java,
        GameCatalogService catalog, GameInstallationService installations, ModpackImportService imports,
        JavaRuntimeService runtimes, ILogger<ModpackImportViewModel> importLogger,
        LauncherSettingsService settings, ILogger<SettingsViewModel> settingsLogger)
    {
        _gameInstances = gameInstances;
        _appLifetime = appLifetime;
        _scopeFactory = scopeFactory;
        _installations = installations;
        Downloads = new DownloadQueueViewModel(downloads);
        GameDownloads = new GameDownloadViewModel(catalog, installations, appLifetime.ApplicationStopping);
        DownloadHub = new DownloadHubViewModel(GameDownloads,
            new CommunityResourceViewModel(gameInstances, appLifetime.ApplicationStopping));
        ModpackImport = new ModpackImportViewModel(imports, runtimes, importLogger, appLifetime.ApplicationStopping);
        JavaSettings = new JavaSettingsViewModel(java, appLifetime.ApplicationStopping);
        Settings = new SettingsViewModel(settings, JavaSettings, settingsLogger);
        RunningInstances = _gameInstances.GetRunningInstances();
        _gameInstances.RunningInstancesChanged += OnRunningInstancesChanged;
        _installations.Installed += OnInstalled;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? GameStarted;
    public SettingsViewModel Settings { get; }

    public DownloadQueueViewModel Downloads { get; }
    public GameDownloadViewModel GameDownloads { get; }
    public DownloadHubViewModel DownloadHub { get; }
    public JavaSettingsViewModel JavaSettings { get; }
    public ModpackImportViewModel ModpackImport { get; }

    public string PlayerName => _selectedProfile?.Name ?? "选择玩家";
    public object PlayerAvatar => GetPlayerAvatar(_selectedProfile?.Id);
    public string InstanceName => _selectedInstance?.Name ?? "选择实例";
    public string InstanceVersion => _selectedInstance?.Version ?? "在底部选择游戏实例";
    public bool HasSelectedInstance => _selectedInstance is not null;
    public string AuthenticationMethod => _selectedProfile?.AuthenticationMethod ?? "未选择档案";
    internal IReadOnlyList<GameInstanceData> AvailableInstances { get; private set; } = [];
    internal IReadOnlyList<GameFolder> AvailableFolders { get; private set; } = [];
    internal Guid? SelectedInstanceId => _selectedInstance?.Id;
    internal string? LaunchOptionsError => _optionsError;
    public IReadOnlyList<RunningGameInstance> RunningInstances { get; private set; }
    public IReadOnlyList<RunningInstanceRow> RunningInstanceRows => RunningInstances
        .Select(instance => new RunningInstanceRow(instance, GetPlayerAvatar(instance.ProfileId))).ToArray();
    public bool HasRunningInstances => RunningInstances.Count > 0;
    public bool IsLaunching => _isLaunching;
    public bool CanChangeLaunchSelection => !_isLaunching && !_isLoading;
    public bool CanLaunchGame => !_isLaunching && !_isLoading && _selectedInstance is not null
        && _selectedProfile is not null && !IsSelectedInstanceRunning;

    public string LaunchLabel => _isLaunching
        ? _progress?.Operation ?? "正在启动"
        : IsSelectedInstanceRunning ? "运行中" : "启动游戏";

    public string? LaunchDetail => _progress switch
    {
        { TotalFiles: > 0 } progress when progress.ProcessedFiles is { } processed =>
            $"已处理 {processed}/{progress.TotalFiles} 个文件",
        { FileName: not null } progress =>
            DownloadQueueViewModel.FormatTransfer(progress.DownloadedBytes, progress.FileBytes),
        _ => null
    };

    public bool HasLaunchDetail => LaunchDetail is not null;

    public string? LaunchStatus => _progress is { } progress ? FormatProgress(progress)
        : _optionsError ?? (_selectedInstance is not null && _selectedProfile is null ? "请选择玩家档案" : null);

    private bool IsSelectedInstanceRunning => _selectedInstance is { } selected
        && RunningInstances.Any(instance => instance.Id == selected.Id);

    internal async Task RefreshLaunchOptionsAsync(Guid? preferredProfileId = null)
    {
        if (preferredProfileId is not null)
            _preferredProfileId = preferredProfileId;
        if (_isLoading || _isLaunching)
            return;

        _isLoading = true;
        _installedRefreshPending = false;
        NotifyLaunchState();
        try
        {
            var token = _appLifetime.ApplicationStopping;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var profiles = await scope.ServiceProvider.GetRequiredService<PlayerProfileService>().GetAllPlayerProfiles(token);
            AvailableInstances = await _gameInstances.GetGameInstances(token);
            AvailableFolders = await _gameInstances.GetGameFolders(token);
            _profiles = (profiles.Data ?? []).Select(profile => new ProfileOption(profile.Id, profile.PlayerName,
                PlayerProfileService.DescribeAuthentication(profile), profile.AuthMethod, profile.AuthServerId)).ToArray();
            _optionsError = null;
            ApplySelection(AvailableInstances.FirstOrDefault(item => item.Id == _selectedInstance?.Id),
                refreshAvatar: preferredProfileId is not null);
        }
        catch (OperationCanceledException) when (_appLifetime.ApplicationStopping.IsCancellationRequested) { }
        catch (DbException)
        {
            _optionsError = "无法读取启动数据，请重新打开实例选择。";
        }
        finally
        {
            _isLoading = false;
            NotifyLaunchState();
            if (_installedRefreshPending && !_disposed)
                _ = RefreshLaunchOptionsAsync();
        }
    }

    internal void SelectInstance(GameInstanceData instance)
    {
        if (CanChangeLaunchSelection)
            ApplySelection(instance);
    }

    internal Task<ServiceResult<GameFolder>> AddGameFolderAsync(string path) =>
        _gameInstances.AddGameFolderAsync(path, _appLifetime.ApplicationStopping);

    private void ApplySelection(GameInstanceData? instance, bool refreshAvatar = false)
    {
        var profile = _profiles.FirstOrDefault(item => item.Id == _preferredProfileId)
            ?? _profiles.FirstOrDefault(item => item.Id == instance?.LastUsedPlayerId)
            ?? (_profiles.Count == 1 ? _profiles[0] : null);
        var profileChanged = _selectedProfile != profile;
        _selectedInstance = instance;
        _selectedProfile = profile;
        Notify(nameof(InstanceName));
        Notify(nameof(InstanceVersion));
        Notify(nameof(HasSelectedInstance));
        Notify(nameof(PlayerName));
        Notify(nameof(PlayerAvatar));
        Notify(nameof(AuthenticationMethod));
        NotifyLaunchState();

        if ((profileChanged || refreshAvatar) && profile is { AuthMethod: AuthMethod.Custom })
            _ = LoadPlayerAvatarAsync(profile.Id);
    }

    private object GetPlayerAvatar(Guid? profileId) =>
        profileId is { } id && _playerAvatars.TryGetValue(id, out var avatar) ? avatar : PlayerSkinAvatar.DefaultSource;

    private async Task LoadPlayerAvatarAsync(Guid profileId)
    {
        if (_disposed || _playerAvatars.ContainsKey(profileId) || !_avatarLoads.Add(profileId))
            return;

        var token = _appLifetime.ApplicationStopping;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<PlayerProfileService>();
            if ((await service.GetPlayerProfile(profileId, token)).Data is not { AuthMethod: AuthMethod.Custom } profile
                || token.IsCancellationRequested || _disposed
                || await PlayerSkinAvatar.LoadAsync(profile, token) is not { } avatar)
                return;

            if (_disposed || token.IsCancellationRequested)
            {
                avatar.Dispose();
                return;
            }

            _playerAvatars.Add(profileId, avatar);
            if (_selectedProfile?.Id == profileId)
                Notify(nameof(PlayerAvatar));
            if (RunningInstances.Any(instance => instance.ProfileId == profileId))
                Notify(nameof(RunningInstanceRows));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (DbException) { return; }
        finally
        {
            _avatarLoads.Remove(profileId);
        }
    }

    internal async Task<string?> LaunchGameAsync(Func<Guid, Task<bool>>? reauthenticate = null)
    {
        if (!CanLaunchGame || _selectedProfile is not { } profile || _selectedInstance is not { } instance)
            return null;

        _isLaunching = true;
        _progress = new("正在准备启动");
        var attempt = ++_launchAttempt;
        NotifyLaunchState();

        try
        {
            var progress = new Progress<GameLaunchProgress>(update =>
            {
                if (_isLaunching && _launchAttempt == attempt)
                {
                    _progress = update;
                    NotifyLaunchStatus();
                }
            });
            var result = await _gameInstances.LaunchGameInstance(profile.Id, instance.Id,
                _appLifetime.ApplicationStopping, progress);
            // 一次用户启动最多恢复一次；仅明确的会话失效触发登录，网络错误不弹密码框。
            if (result.ErrorCode == ServiceErrorCode.AuthenticationRequired && profile.AuthMethod == AuthMethod.Custom
                && reauthenticate is not null && !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                _progress = new("请重新登录玩家档案");
                NotifyLaunchStatus();
                if (!await reauthenticate(profile.Id)) return null;
                _installedRefreshPending = true;
                _progress = new("登录成功，正在继续启动");
                NotifyLaunchStatus();
                result = await _gameInstances.LaunchGameInstance(profile.Id, instance.Id,
                    _appLifetime.ApplicationStopping, progress);
            }
            if (result.Status == ServiceStatus.Success && !_appLifetime.ApplicationStopping.IsCancellationRequested)
                GameStarted?.Invoke();
            return result.Status == ServiceStatus.Success || _appLifetime.ApplicationStopping.IsCancellationRequested
                ? null : result.Message ?? "无法启动游戏，请重试。";
        }
        finally
        {
            _isLaunching = false;
            _progress = null;
            RefreshRunningInstances();
            if (_installedRefreshPending && !_disposed)
                _ = RefreshLaunchOptionsAsync();
        }
    }

    internal ServiceResult<object> StopGameInstance(RunningGameInstance instance) =>
        _gameInstances.StopGameInstance(instance);

    private static string FormatProgress(GameLaunchProgress progress)
    {
        var text = progress.Operation;
        if (progress.TotalFiles is > 0 && progress.ProcessedFiles is { } processed)
            text += $" · 已处理 {processed}/{progress.TotalFiles} 个文件";

        if (!string.IsNullOrWhiteSpace(progress.FileName))
            text += $" · {progress.FileName} · {DownloadQueueViewModel.FormatTransfer(progress.DownloadedBytes, progress.FileBytes)}";

        return text;
    }

    private void OnRunningInstancesChanged() => Dispatcher.UIThread.Post(RefreshRunningInstances);

    private void OnInstalled() => Dispatcher.UIThread.Post(async () =>
    {
        if (_disposed)
            return;
        _installedRefreshPending = true;
        await RefreshLaunchOptionsAsync();
    });

    private void RefreshRunningInstances()
    {
        if (_disposed)
            return;

        RunningInstances = _gameInstances.GetRunningInstances();
        Notify(nameof(RunningInstances));
        Notify(nameof(RunningInstanceRows));
        Notify(nameof(HasRunningInstances));
        NotifyLaunchState();
        foreach (var profileId in RunningInstances.Select(instance => instance.ProfileId).Distinct())
            _ = LoadPlayerAvatarAsync(profileId);
    }

    private void NotifyLaunchState()
    {
        Notify(nameof(IsLaunching));
        Notify(nameof(CanLaunchGame));
        Notify(nameof(CanChangeLaunchSelection));
        NotifyLaunchStatus();
    }

    private void NotifyLaunchStatus()
    {
        Notify(nameof(LaunchLabel));
        Notify(nameof(LaunchDetail));
        Notify(nameof(HasLaunchDetail));
        Notify(nameof(LaunchStatus));
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _disposed = true;
        ModpackImport.Dispose();
        JavaSettings.Dispose();
        Settings.Dispose();
        DownloadHub.Dispose();
        GameDownloads.Dispose();
        _gameInstances.RunningInstancesChanged -= OnRunningInstancesChanged;
        _installations.Installed -= OnInstalled;
        foreach (var avatar in _playerAvatars.Values)
            avatar.Dispose();
        _playerAvatars.Clear();
    }

    private sealed record ProfileOption(Guid Id, string Name, string AuthenticationMethod, AuthMethod AuthMethod,
        Guid? AuthServerId);
}

public sealed record RunningInstanceRow(RunningGameInstance Instance, object PlayerAvatar);
