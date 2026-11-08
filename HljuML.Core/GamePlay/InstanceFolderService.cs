using System.Text.Json;
using ColorMC.Core;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Objs;
using ColorMC.Core.Utils;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Core.Model.Entity;
using HljuML.Shared.Model;

namespace HljuML.Core.GamePlay;

internal static class InstanceFolderService
{
    internal static string? GetResourceDirectory(string instanceDirectory)
    {
        if (LauncherPaths.IsDefaultInstanceDirectory(instanceDirectory))
        {
            return null;
        }

        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(instanceDirectory));
        var parent = Path.GetDirectoryName(directory);
        if (parent is null)
        {
            return null;
        }

        // ColorMC keeps shared resources alongside its instances directory.
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var hasColorMcLayout = string.Equals(Path.GetFileName(directory), Names.NameInstanceDir, comparison)
            && string.Equals(Path.GetFileName(parent), Names.NameMinecraftKey, comparison);
        var hasSharedFiles = Directory.Exists(Path.Combine(parent, Names.NameVersionDir))
            && Directory.Exists(Path.Combine(parent, Names.NameLibDir));
        return hasColorMcLayout || hasSharedFiles ? parent : null;
    }

    internal static IEnumerable<GameSettingObj> ReadInstances(string path, CancellationToken token)
    {
        foreach (var directory in Directory.EnumerateDirectories(path, "*", new EnumerationOptions
                 {
                     AttributesToSkip = FileAttributes.ReparsePoint,
                     IgnoreInaccessible = false
                 }))
        {
            token.ThrowIfCancellationRequested();
            if (File.Exists(Path.Combine(directory, GameInstallationService.PendingMarker))
                || Path.GetFileName(directory).StartsWith(".hljuml-install-", StringComparison.Ordinal))
            {
                continue;
            }

            var file = Path.Combine(directory, Names.NameGameFile);
            if (!File.Exists(file))
            {
                continue;
            }

            GameSettingObj? game;
            try
            {
                using var stream = File.OpenRead(file);
                game = JsonSerializer.Deserialize(stream, JsonType.GameSettingObj);
            }
            catch (JsonException)
            {
                continue;
            }

            if (game is null || game.UUID == Guid.Empty || string.IsNullOrWhiteSpace(game.Version)
                || !Enum.IsDefined(game.Loader))
            {
                continue;
            }

            // ColorMC combines DirName with its default base path, allowing an absolute instance directory.
            game.DirName = directory;
            yield return game;
        }
    }

    internal static async Task<ServiceResult<GameSettingObj>> ResolveAsync(GameInstanceData instance, string path,
        IProgress<GameLaunchProgress>? progress, CancellationToken token)
    {
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path))
        {
            return ServiceResult<GameSettingObj>.FailureResult(message: "实例目录不存在或无法访问。");
        }

        try
        {
            var matches = ReadInstances(path, token).Where(game => game.UUID == instance.Id).Take(2).ToArray();
            if (matches.Length > 1)
            {
                return ServiceResult<GameSettingObj>.FailureResult(message: "目录中有多个相同 ID 的实例。");
            }

            if (matches.Length == 1)
            {
                var game = matches[0];
                if (game.Version != instance.Version || game.Loader != instance.Loader)
                {
                    return ServiceResult<GameSettingObj>.FailureResult(message: "实例记录与 ColorMC 的版本或加载器配置不一致。");
                }

                game.ReadModInfo(false);
                game.ReadLaunchCountData();
                game.ReadCustomJson();
                return ServiceResult<GameSettingObj>.SuccessResult(game);
            }

            if (instance.Loader != Loaders.Normal || string.IsNullOrWhiteSpace(instance.Version))
            {
                return ServiceResult<GameSettingObj>.FailureResult(message: "请在该目录中准备同 ID 的 ColorMC 实例配置。");
            }

            progress?.Report(new GameLaunchProgress("正在初始化原版实例"));
            var directory = Path.Combine(path, $"hljuml-{instance.Id:N}");
            var newGame = new GameSettingObj
            {
                UUID = instance.Id,
                Name = instance.Name,
                DirName = directory,
                Version = instance.Version,
                Loader = instance.Loader,
                Encoding = OperatingSystem.IsWindows() ? LogEncoding.GBK : default,
                Mods = [],
                CustomJson = [],
                LaunchData = new LaunchDataObj { AddTime = DateTime.Now }
            };

            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                return ServiceResult<GameSettingObj>.FailureResult(message: "无法在重定向的实例目录中初始化实例。");
            }

            var temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                {
                    await JsonSerializer.SerializeAsync(stream, newGame with { DirName = Path.GetFileName(directory) },
                        JsonType.GameSettingObj, token);
                }

                token.ThrowIfCancellationRequested();
                File.Move(temporary, Path.Combine(directory, Names.NameGameFile));
            }
            finally
            {
                File.Delete(temporary);
            }

            return ServiceResult<GameSettingObj>.SuccessResult(newGame);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return ServiceResult<GameSettingObj>.FailureResult(message: "无法读取或初始化该目录中的实例，请检查目录权限与配置。");
        }
    }
}