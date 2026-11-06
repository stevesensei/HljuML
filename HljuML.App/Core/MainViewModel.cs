using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using HljuML.Core.GamePlay;
using HljuML.Shared.Model;
using Microsoft.Extensions.Hosting;

namespace HljuML.App.Core;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly GameInstanceService _gameInstances;
    private readonly IHostApplicationLifetime _appLifetime;
    private bool _isLaunching;

    public MainViewModel(GameInstanceService gameInstances, IHostApplicationLifetime appLifetime)
    {
        _gameInstances = gameInstances;
        _appLifetime = appLifetime;
        RunningInstances = _gameInstances.GetRunningInstances();
        _gameInstances.RunningInstancesChanged += OnRunningInstancesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PlayerName => _gameInstances.PlayerName;
    public string InstanceName => _gameInstances.InstanceName;
    public string AuthenticationMethod => _gameInstances.AuthenticationMethod;
    public IReadOnlyList<RunningGameInstance> RunningInstances { get; private set; }
    public bool HasRunningInstances => RunningInstances.Count > 0;
    public bool CanLaunchGame => !_isLaunching && !IsSelectedInstanceRunning;
    public string? LaunchError { get; private set; }

    public string LaunchLabel => _isLaunching
        ? "正在启动…"
        : IsSelectedInstanceRunning
            ? "运行中"
            : LaunchError is null ? "启动游戏" : "启动失败 · 重试";

    private bool IsSelectedInstanceRunning => RunningInstances.Any(instance => instance.InstanceName == InstanceName);

    internal async Task LaunchGameAsync()
    {
        if (!CanLaunchGame)
        {
            return;
        }

        _isLaunching = true;
        LaunchError = null;
        NotifyLaunchState();

        try
        {
            var result = await _gameInstances.LaunchGameInstance(_appLifetime.ApplicationStopping);
            if (result.Status != ServiceStatus.Success)
            {
                LaunchError = result.Message;
            }
        }
        finally
        {
            _isLaunching = false;
            RefreshRunningInstances();
        }
    }

    internal ServiceResult<object> StopGameInstance(RunningGameInstance instance) =>
        _gameInstances.StopGameInstance(instance);

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
        Notify(nameof(LaunchLabel));
        Notify(nameof(LaunchError));
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose() => _gameInstances.RunningInstancesChanged -= OnRunningInstancesChanged;
}
