using System.Text.Json;
using ColorMC.Core;
using ColorMC.Core.Helpers;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Net;
using ColorMC.Core.Objs;
using HljuML.Shared.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

public sealed partial class GameInstallationService
{
    private async Task<IReadOnlyList<Artifact>> PrepareFilesAsync(Guid operationId, GameSettingObj instance,
        CancellationToken token)
    {
        downloads.SetOperationPhase(operationId, "正在获取并校验版本元数据");
        if (instance.Loader != Loaders.Normal)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var catalog = scope.ServiceProvider.GetRequiredService<GameCatalogService>();
            var versions = await catalog.GetLoaderVersionsAsync(instance.Version, instance.Loader, token);
            if (versions.Status != ServiceStatus.Success || versions.Data is null)
            {
                throw new IOException(versions.Message);
            }

            if (!versions.Data.Any(item => item.Version == instance.LoaderVersion))
            {
                throw new IOException("所选加载器版本不属于此 Minecraft 版本，请重新选择。");
            }
        }

        var manifest = await VersionPath.GetVersionsAsync()
            ?? throw new IOException("无法取得 Minecraft 版本清单，请检查网络后重试。");
        var version = manifest.Versions.FirstOrDefault(item => item.Id == instance.Version)
            ?? throw new IOException("Minecraft 版本已不在版本清单中，请刷新后重试。");
        var metadata = new FileItemObj
        {
            Name = $"{version.Id}.json", Sha1 = version.Sha1,
            Url = UrlHelper.DownloadSourceChange(version.Url, CoreHttpClient.Source),
            Local = Path.Combine(VersionPath.BaseDir, $"{version.Id}.json")
        };
        var artifacts = new List<Artifact>();
        artifacts.AddRange(await DownloadFilesAsync([metadata], new Dictionary<string, long>(), token));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(metadata.Local, token));
        var game = VersionPath.GetVersion(instance.Version)
            ?? throw new IOException("Minecraft 版本元数据无效。");
        if (game.Id != instance.Version)
        {
            throw new IOException("版本元数据与请求不一致。");
        }

        var sizes = ReadSizes(document.RootElement);
        downloads.SetOperationPhase(operationId, "正在准备客户端、依赖库与本机库");
        var files = (await game.BuildGameLibsAsync(LibrariesPath.GetNativeDir(instance.Version))).ToList();
        files.Add(GameDownloadHelper.BuildGameItem(instance.Version));
        if (game.Logging is { } logging)
        {
            files.Add(GameDownloadHelper.BuildLog4jItem(logging));
        }

        artifacts.AddRange(await DownloadFilesAsync(files, sizes, token));

        if (game.AssetIndex is { } assetIndex)
        {
            downloads.SetOperationPhase(operationId, "正在获取并校验资源索引");
            var rawIndex = document.RootElement.GetProperty("assetIndex");
            var index = new FileItemObj
            {
                Name = $"资源索引 {assetIndex.Id}",
                Local = Path.Combine(AssetsPath.IndexDir, $"{assetIndex.Id}.json"),
                Url = UrlHelper.DownloadSourceChange(assetIndex.Url, CoreHttpClient.Source),
                Sha1 = rawIndex.TryGetProperty("sha1", out var hash) ? hash.GetString() : null
            };
            artifacts.AddRange(await DownloadFilesAsync([index], sizes, token));
            using var indexDocument = JsonDocument.Parse(await File.ReadAllTextAsync(index.Local, token));
            var objects = new List<FileItemObj>();
            foreach (var item in indexDocument.RootElement.GetProperty("objects").EnumerateObject())
            {
                var objectHash = item.Value.GetProperty("hash").GetString();
                if (objectHash is null || objectHash.Length != 40 || !objectHash.All(Uri.IsHexDigit))
                {
                    throw new IOException("资源索引包含无效的文件摘要。");
                }

                var file = GameDownloadHelper.BuildAssetsItem(item.Name, objectHash);
                objects.Add(file);
            }

            downloads.SetOperationPhase(operationId, "正在准备并校验游戏资源");
            artifacts.AddRange(await DownloadFilesAsync(objects, ReadSizes(indexDocument.RootElement), token));
        }

        if (instance.Loader != Loaders.Normal)
        {
            downloads.SetOperationPhase(operationId, "正在准备加载器文件");
            var loaderFiles = await BuildLoaderFilesAsync(instance, token);
            artifacts.AddRange(await DownloadFilesAsync(loaderFiles, new Dictionary<string, long>(), token));
            if (instance.Loader == Loaders.OptiFine)
            {
                artifacts.Add(await WaitForOptifineMetadataAsync(instance.LoaderVersion!, token));
            }

            if (instance.Loader is Loaders.Forge or Loaders.NeoForge)
            {
                downloads.SetOperationPhase(operationId, "加载器文件已准备；处理器将在首次启动时由 wrapper 执行");
                logger.LogInformation(
                    "Prepared Forge input files for {InstanceId}; processors deferred to first launch", instance.UUID);
            }
        }

        return artifacts.DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static async Task<List<FileItemObj>> BuildLoaderFilesAsync(GameSettingObj instance, CancellationToken token)
    {
        List<FileItemObj> files;
        switch (instance.Loader)
        {
            case Loaders.Forge:
            case Loaders.NeoForge:
                var forge = instance.GetForgeLibs() ?? await instance.BuildForgeAsync(token)
                    ?? throw new IOException("无法取得 Forge / NeoForge 安装文件。");
                files = [.. forge.Loaders ?? [], .. forge.Installs ?? []];
                if (instance.IsGameVersionV2())
                {
                    GameHelper.ReadyForgeWrapper();
                    files.Add(GameHelper.ForgeWrapper);
                }

                // Blank URLs describe processor outputs produced by ForgeWrapper on first launch.
                files = files.Where(item => !string.IsNullOrWhiteSpace(item.Url) || File.Exists(item.Local)).ToList();
                break;
            case Loaders.Fabric:
                files = instance.GetFabricLibs() ?? await instance.BuildFabricAsync(token)
                    ?? throw new IOException("无法取得 Fabric 安装文件。");
                break;
            case Loaders.OptiFine:
                files = instance.GetOptifineLibs() ?? await instance.BuildOptifineAsync(token)
                    ?? throw new IOException("无法取得 OptiFine 安装文件。");
                GameHelper.ReadyOptifineWrapper();
                files.Add(GameHelper.OptifineWrapper);
                break;
            default:
                throw new IOException("不支持此加载器。");
        }

        token.ThrowIfCancellationRequested();
        return files;
    }

    private static async Task<Artifact> WaitForOptifineMetadataAsync(string loaderVersion, CancellationToken token)
    {
        var file = Path.Combine(VersionPath.BaseDir, Names.NameOptifineFile);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                    4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty(loaderVersion, out _))
                {
                    return new Artifact(Path.GetFullPath(file), null, null, null, false);
                }
            }
            catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
            {
                // ColorMC persists this metadata through its background config queue.
            }

            await Task.Delay(100, token);
        }

        throw new IOException("OptiFine 元数据未能持久化，安装未发布。请重试。");
    }

    private static Dictionary<string, long> ReadSizes(JsonElement root)
    {
        var sizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        Visit(root);
        return sizes;

        void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray()) Visit(item);
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("size", out var size) && size.TryGetInt64(out var length))
                {
                    if (element.TryGetProperty("sha1", out var sha1) && sha1.GetString() is { } sha)
                    {
                        sizes[sha] = length;
                    }

                    if (element.TryGetProperty("hash", out var hash) && hash.GetString() is { } value)
                    {
                        sizes[value] = length;
                    }
                }

                foreach (var property in element.EnumerateObject()) Visit(property.Value);
            }
        }
    }
}