using System.Text;
using System.Collections.Concurrent;
using System.ComponentModel;
using ColorMC.Core;
using ColorMC.Core.Config;
using ColorMC.Core.Game;
using ColorMC.Core.Helpers;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Net;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Login;
using HljuML.Shared.Model;
using Microsoft.Extensions.Hosting;

namespace HljuML.Core.GamePlay;

public sealed class GameInstanceService: IHostedService
{
    private readonly ConcurrentDictionary<Guid, RunningProcess> _running = new();
    private readonly SemaphoreSlim _launchGate = new(1, 1);

    public string InstanceName { get; } = "Vanilla 26.2";

    public string PlayerName { get; } = "Tester";

    public string AuthenticationMethod => "离线认证";

    public event Action? RunningInstancesChanged;

    public IReadOnlyList<RunningGameInstance> GetRunningInstances() =>
        _running.Values.Select(process => process.Instance).OrderBy(instance => instance.ProcessId).ToArray();

    /// <summary>
    /// 启动游戏进程，新项目先使用临时的开发
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<int?>> LaunchGameInstance(CancellationToken stoppingToken)
    {
        if (!_launchGate.Wait(0))
        {
            return ServiceResult<int?>.FailureResult(null, "正在启动实例，请稍候。");
        }

        try
        {
            var game = InstancesPath.GetGameByName(InstanceName)??
                       await InstancesPath.CreateGameAsync(new()
                       {
                           UUID = Guid.NewGuid(),
                           Name = InstanceName,
                           Version = "26.2",
                           Loader = Loaders.Normal,
                           JvmLocal = "F:\\java\\25\\bin\\java.exe",
                       }, null);
            if (game == null)
            {
                return ServiceResult<int?>.FailureResult(null,"创建游戏实例失败");
            }

            if (_running.ContainsKey(game.UUID))
            {
                return ServiceResult<int?>.FailureResult(null, "该实例已经在运行。");
            }

            //Auth
            var auth = new LoginObj()
            {
                AuthType = AuthType.Offline,
                UserName = PlayerName,
                UUID = HashHelper.GenMd5(Encoding.UTF8.GetBytes(
                    PlayerName.ToLowerInvariant())),
                ClientToken = Guid.NewGuid().ToString("N")
            };
            var handle = await game.StartGameAsync(new(){Auth = auth}, stoppingToken)
                         ?? throw new InvalidOperationException("启动未完成");
            var instance = new RunningGameInstance(game.UUID, handle.Process.Id, game.Name, auth.UserName, AuthenticationMethod);
            var running = new RunningProcess(instance, handle);
            handle.Process.Exited += (_, _) => RemoveRunningProcess(running);
            _running[instance.Id] = running;

            if (handle.IsExit)
            {
                RemoveRunningProcess(running);
                return ServiceResult<int?>.FailureResult(null, "游戏进程在启动后立即退出。");
            }

            RunningInstancesChanged?.Invoke();
            return ServiceResult<int?>.SuccessResult(instance.ProcessId, "游戏实例启动成功");
        }
        catch (Exception e)
        {
            return ServiceResult<int?>.FailureResult(null, $"启动游戏实例时发生错误: {e.Message}");
        }
        finally
        {
            _launchGate.Release();
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
            Local = Path.Combine(AppContext.BaseDirectory, "HljuML"),
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
    int ProcessId,
    string InstanceName,
    string PlayerName,
    string AuthenticationMethod);
