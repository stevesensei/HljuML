using System.Text.Json;
using HljuML.Core.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

public sealed partial class GameInstallationService
{
    /// <summary>
    ///     导入失败只清理带本任务所有权标记的暂存目录，不触碰既有实例、已提交的最终目录或共享缓存。
    ///     发布后若标记清理失败，则保留最终目录给已有启动恢复流程处理。
    /// </summary>
    private async Task CleanupFailedImportAsync(Installation installation)
    {
        if (installation.Modpack is null)
        {
            return;
        }

        var stage = Path.Combine(LauncherPaths.DefaultInstanceDirectory,
            $".hljuml-install-{installation.InstanceId:N}");
        try
        {
            if (!Directory.Exists(stage) || !await IsOwnedAsync(stage, installation.InstanceId))
            {
                return;
            }

            EnsureInstancePath(LauncherPaths.DefaultInstanceDirectory, stage, true);
            var directories = new Stack<string>();
            directories.Push(stage);
            // 递归删除前检查整个目录树，拒绝跟随任何重定向路径。
            while (directories.TryPop(out var directory))
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new IOException("暂存目录包含重定向路径，已保留目录供人工检查。");
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        directories.Push(entry);
                    }
                }

            Directory.Delete(stage, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(error, "Could not clean failed modpack stage {Directory}", stage);
        }
    }
}