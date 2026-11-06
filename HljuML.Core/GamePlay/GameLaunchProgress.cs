using System.Diagnostics;
using ColorMC.Core.GuiHandle;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Login;

namespace HljuML.Core.GamePlay;

public sealed record GameLaunchProgress(string Operation, int? ProcessedFiles = null, int? TotalFiles = null,
    string? FileName = null, long DownloadedBytes = 0, long? FileBytes = null);

internal sealed class GameLaunchReporter(IProgress<GameLaunchProgress> progress) : ILaunchGui
{
    public void StateUpdate(GameSettingObj obj, LaunchState state) => progress.Report(new(state switch
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

    public Task<bool> RequestDownload() => Task.FromResult(true);
    public Task<bool> LaunchProcess(bool pre) => Task.FromResult(true);
    public Task<bool> ServerPackUpgrade(string text) => Task.FromResult(true);
    public Task<bool> LoginFail(LoginObj obj) => Task.FromResult(false);
    public void NoJava(int version) => progress.Report(new($"正在查找 Java {version}"));

    internal void AttachDownload(DownloadEventArgs args) => args.GuiHandle ??= new DownloadReporter(progress);

    private sealed class DownloadReporter(IProgress<GameLaunchProgress> progress) : IDownloadGui
    {
        private readonly object _sync = new();
        private int _total;
        private int _processed;
        private long _lastReport;

        public void Update(int thread, bool state, int count) { }

        public void UpdateTask(UpdateType type, int data)
        {
            lock (_sync)
            {
                if (type == UpdateType.AddItems)
                {
                    _total = data;
                    Report();
                }
                else if (type == UpdateType.ItemDone)
                {
                    _processed += data;
                    if (_processed == _total || CanReport())
                        Report();
                }
            }
        }

        public void UpdateItem(int thread, FileItemObj item)
        {
            lock (_sync)
            {
                if (item.State == DownloadItemState.Download && CanReport())
                    Report(item);
            }
        }

        private bool CanReport() => Stopwatch.GetElapsedTime(_lastReport) >= TimeSpan.FromMilliseconds(150);

        private void Report(FileItemObj? item = null)
        {
            _lastReport = Stopwatch.GetTimestamp();
            progress.Report(new("正在下载", _processed, _total, item?.Name, item?.NowSize ?? 0,
                item?.AllSize is > 0 ? item.AllSize : null));
        }
    }
}
