using ColorMC.Core.GuiHandle;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Login;

namespace HljuML.Core.GamePlay;

public sealed record JavaVersionWarning(int SelectedMajor, int RecommendedMajor);

public sealed record GameLaunchProgress(
    string Operation,
    int? ProcessedFiles = null,
    int? TotalFiles = null,
    string? FileName = null,
    long DownloadedBytes = 0,
    long? FileBytes = null,
    JavaVersionWarning? JavaWarning = null);

internal sealed class GameLaunchReporter(IProgress<GameLaunchProgress> progress) : ILaunchGui
{
    public void StateUpdate(GameSettingObj obj, LaunchState state)
    {
        progress.Report(new GameLaunchProgress(state switch
        {
            LaunchState.Loging => "正在验证玩家档案",
            LaunchState.Checking => "正在检查游戏文件和运行环境",
            LaunchState.Downloading => "正在下载游戏文件",
            LaunchState.JvmPrepare => "正在准备 Java 启动参数",
            LaunchState.LaunchPre => "正在执行启动前命令",
            LaunchState.LaunchPost => "正在执行启动后命令",
            LaunchState.LoadServerPack => "正在读取服务器包",
            LaunchState.CheckServerPack => "正在检查服务器包",
            LaunchState.DownloadServerPack => "正在下载服务器包文件",
            LaunchState.DownloadServerPackDone => "服务器包文件已处理",
            _ => "正在启动游戏进程"
        }));
    }

    public Task<bool> RequestDownload()
    {
        return Task.FromResult(true);
    }

    public Task<bool> LaunchProcess(bool pre)
    {
        return Task.FromResult(true);
    }

    public Task<bool> ServerPackUpgrade(string text)
    {
        return Task.FromResult(true);
    }

    public Task<bool> LoginFail(LoginObj obj)
    {
        return Task.FromResult(false);
    }

    public void NoJava(int version)
    {
        progress.Report(new GameLaunchProgress($"正在查找 Java {version}"));
    }
}