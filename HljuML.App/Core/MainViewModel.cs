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
using HljuML.Core.Model.Entity;
using HljuML.Shared.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HljuML.App.Core;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly GameInstanceService _gameInstances;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IServiceScopeFactory _scopeFactory;
    private IReadOnlyList<ProfileOption> _profiles = [];
    private GameInstanceData? _selectedInstance;
    private ProfileOption? _selectedProfile;
    private Bitmap? _playerAvatar;
    private int _avatarRevision;
    private Guid? _preferredProfileId;
    private bool _isLoading;
    private bool _isLaunching;
    private int _launchAttempt;
    private string? _optionsError;
    private GameLaunchProgress? _progress;

    public MainViewModel(GameInstanceService gameInstances, IHostApplicationLifetime appLifetime,
        IServiceScopeFactory scopeFactory)
    {
        _gameInstances = gameInstances;
        _appLifetime = appLifetime;
        _scopeFactory = scopeFactory;
        RunningInstances = _gameInstances.GetRunningInstances();
        _gameInstances.RunningInstancesChanged += OnRunningInstancesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PlayerName => _selectedProfile?.Name ?? "选择玩家";
    public object PlayerAvatar => _playerAvatar is { } avatar ? avatar : PlayerSkinAvatar.DefaultSource;
    public string InstanceName => _selectedInstance?.Name ?? "选择实例";
    public string InstanceVersion => _selectedInstance?.Version ?? "在底部选择游戏实例";
    public bool HasSelectedInstance => _selectedInstance is not null;
    public string AuthenticationMethod => _selectedProfile?.AuthenticationMethod ?? "未选择档案";
    internal IReadOnlyList<GameInstanceData> AvailableInstances { get; private set; } = [];
    internal Guid? SelectedInstanceId => _selectedInstance?.Id;
    internal string? LaunchOptionsError => _optionsError;
    public IReadOnlyList<RunningGameInstance> RunningInstances { get; private set; }
    public bool HasRunningInstances => RunningInstances.Count > 0;
    public bool CanChangeLaunchSelection => !_isLaunching && !_isLoading;
    public bool CanLaunchGame => !_isLaunching && !_isLoading && _selectedInstance is not null
        && _selectedProfile is not null && !IsSelectedInstanceRunning;

    public string? LaunchError { get; private set; }

    public string LaunchLabel => _isLaunching
        ? "正在启动…"
        : IsSelectedInstanceRunning
            ? "运行中"
            : LaunchError is null ? "启动游戏" : "启动失败 · 重试";

    public string? LaunchStatus => _progress is { } progress ? FormatProgress(progress) : LaunchError ??
        (IsSelectedInstanceRunning ? "游戏已启动" : _optionsError ?? (
            _selectedInstance is not null && _selectedProfile is null ? "请选择玩家档案" : null));

    public bool HasLaunchStatus => LaunchStatus is not null;

    private bool IsSelectedInstanceRunning => _selectedInstance is { } selected
        && RunningInstances.Any(instance => instance.Id == selected.Id);

    internal async Task RefreshLaunchOptionsAsync(Guid? preferredProfileId = null)
    {
        if (preferredProfileId is not null)
            _preferredProfileId = preferredProfileId;
        if (_isLoading || _isLaunching)
            return;

        _isLoading = true;
        NotifyLaunchState();
        try
        {
            var token = _appLifetime.ApplicationStopping;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var profiles = await scope.ServiceProvider.GetRequiredService<PlayerProfileService>().GetAllPlayerProfiles(token);
            AvailableInstances = await _gameInstances.GetGameInstances(token);
            _profiles = (profiles.Data ?? []).Select(profile => new ProfileOption(profile.Id, profile.PlayerName,
                PlayerProfileService.DescribeAuthentication(profile), profile.AuthMethod, profile.AuthServerId)).ToArray();
            _optionsError = null;
            ApplySelection(AvailableInstances.FirstOrDefault(item => item.Id == _selectedInstance?.Id),
                refreshAvatar: preferredProfileId is not null && _playerAvatar is null);
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
        }
    }

    internal void SelectInstance(GameInstanceData instance)
    {
        if (CanChangeLaunchSelection)
            ApplySelection(instance);
    }

    private void ApplySelection(GameInstanceData? instance, bool refreshAvatar = false)
    {
        var profile = _profiles.FirstOrDefault(item => item.Id == _preferredProfileId)
            ?? _profiles.FirstOrDefault(item => item.Id == instance?.LastUsedPlayerId)
            ?? (_profiles.Count == 1 ? _profiles[0] : null);
        var profileChanged = _selectedProfile != profile;
        if (_selectedInstance?.Id != instance?.Id || profileChanged)
            LaunchError = null;

        _selectedInstance = instance;
        _selectedProfile = profile;
        Notify(nameof(InstanceName));
        Notify(nameof(InstanceVersion));
        Notify(nameof(HasSelectedInstance));
        Notify(nameof(PlayerName));
        Notify(nameof(AuthenticationMethod));
        NotifyLaunchState();

        if (profileChanged || refreshAvatar)
        {
            var revision = ++_avatarRevision;
            SetPlayerAvatar(null);
            if (profile is { AuthMethod: AuthMethod.Custom })
                _ = LoadPlayerAvatarAsync(profile.Id, revision);
        }
    }

    private async Task LoadPlayerAvatarAsync(Guid profileId, int revision)
    {
        var token = _appLifetime.ApplicationStopping;
        PlayerProfile? profile;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<PlayerProfileService>();
            profile = (await service.GetPlayerProfile(profileId, token)).Data;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (DbException) { return; }

        if (profile is null || revision != _avatarRevision || token.IsCancellationRequested)
            return;

        var avatar = await PlayerSkinAvatar.LoadAsync(profile, token);
        if (revision == _avatarRevision && !token.IsCancellationRequested)
            SetPlayerAvatar(avatar);
        else
            avatar?.Dispose();
    }

    private void SetPlayerAvatar(Bitmap? avatar)
    {
        var previous = _playerAvatar;
        _playerAvatar = avatar;
        Notify(nameof(PlayerAvatar));
        previous?.Dispose();
    }

    internal async Task LaunchGameAsync()
    {
        if (!CanLaunchGame || _selectedProfile is not { } profile || _selectedInstance is not { } instance)
            return;

        _isLaunching = true;
        LaunchError = null;
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
            if (result.Status != ServiceStatus.Success)
                LaunchError = result.Message;
        }
        finally
        {
            _isLaunching = false;
            _progress = null;
            RefreshRunningInstances();
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
        {
            var bytes = FormatBytes(progress.DownloadedBytes);
            text += progress.FileBytes is { } total
                ? $" · {progress.FileName} · {bytes} / {FormatBytes(total)}"
                : $" · {progress.FileName} · {bytes}";
        }

        return text;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.0} MiB",
        >= 1024 => $"{bytes / 1024d:0.0} KiB",
        _ => $"{bytes} B"
    };

    private void OnRunningInstancesChanged() => Dispatcher.UIThread.Post(RefreshRunningInstances);

    private void RefreshRunningInstances()
    {
        RunningInstances = _gameInstances.GetRunningInstances();
        Notify(nameof(RunningInstances));
        Notify(nameof(HasRunningInstances));
        NotifyLaunchState();
    }

    private void NotifyLaunchState()
    {
        Notify(nameof(CanLaunchGame));
        Notify(nameof(CanChangeLaunchSelection));
        Notify(nameof(LaunchLabel));
        Notify(nameof(LaunchError));
        NotifyLaunchStatus();
    }

    private void NotifyLaunchStatus()
    {
        Notify(nameof(LaunchStatus));
        Notify(nameof(HasLaunchStatus));
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _gameInstances.RunningInstancesChanged -= OnRunningInstancesChanged;
        _avatarRevision++;
        _playerAvatar?.Dispose();
    }

    private sealed record ProfileOption(Guid Id, string Name, string AuthenticationMethod, AuthMethod AuthMethod,
        Guid? AuthServerId);
}
