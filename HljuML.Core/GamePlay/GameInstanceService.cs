using System.Text;
using ColorMC.Core;
using ColorMC.Core.Game;
using ColorMC.Core.Helpers;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Login;
using HljuML.Shared.Model;
using Microsoft.Extensions.Hosting;

namespace HljuML.Core.GamePlay;

public sealed class GameInstanceService: IHostedService
{
    /// <summary>
    /// 启动游戏进程，新项目先使用临时的开发
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<int?>> LaunchGameInstance(CancellationToken stoppingToken)
    {
        //测试实例
        try
        {
            var game = InstancesPath.GetGameByName("Vanilla 26.2")??
                       await InstancesPath.CreateGameAsync(new()
                       {
                           UUID = Guid.NewGuid(),
                           Name = "Vanilla 26.2",
                           Version = "26.2",
                           Loader = Loaders.Normal,
                           JvmLocal = "F:\\java\\25\\bin\\java.exe",
                       }, null);
            if (game == null)
            {
                return ServiceResult<int?>.FailureResult(null,"创建游戏实例失败");
            }
            //Auth
            var playerName = "Tester";
            var auth = new LoginObj()
            {
                AuthType = AuthType.Offline,
                UserName = playerName,
                UUID = HashHelper.GenMd5(Encoding.UTF8.GetBytes(
                    playerName.ToLowerInvariant())),
                ClientToken = Guid.NewGuid().ToString("N")
            };
            var handle = await game.StartGameAsync(new(){Auth = auth}, stoppingToken)
                         ?? throw new InvalidOperationException("启动未完成");
            return ServiceResult<int?>.SuccessResult(handle.Process.Id,$"游戏实例启动成功");
        }
        catch (Exception e)
        {
            return ServiceResult<int?>.FailureResult(null, $"启动游戏实例时发生错误: {e.Message}");
        }
    }
    
    /// <summary>
    /// 停止游戏进程
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<object>> StopGameInstance()
    {
        
        return ServiceResult<object>.SuccessResult();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        //核心初始化
        ColorMCCore.Init(new()
        {
            Local = Path.Combine(AppContext.BaseDirectory, "HljuML"),
        });
        ColorMCCore.Init1();
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        ColorMCCore.Close();
        return Task.CompletedTask;
    }
}