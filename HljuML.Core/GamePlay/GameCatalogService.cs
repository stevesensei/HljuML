using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using ColorMC.Core.Helpers;
using ColorMC.Core.Net;
using ColorMC.Core.Objs;
using HljuML.Shared.Model;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

public sealed class GameCatalogService
{
    private readonly ILogger<GameCatalogService> _logger;
    private readonly Func<string, CancellationToken, Task<string>> _read;
    private readonly ConcurrentDictionary<SourceLocal, IReadOnlyList<GameVersionInfo>> _versions = new();

    private readonly
        ConcurrentDictionary<(SourceLocal Source, string Mc, Loaders Loader), IReadOnlyList<GameLoaderVersion>>
        _loaders = new();

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _requests = new();

    public GameCatalogService(ILogger<GameCatalogService> logger) : this(logger, ReadAsync)
    {
    }

    internal GameCatalogService(ILogger<GameCatalogService> logger, Func<string, CancellationToken, Task<string>> read)
    {
        _logger = logger;
        _read = read;
    }

    public async Task<ServiceResult<IReadOnlyList<GameVersionInfo>>> GetVersionsAsync(
        bool refresh = false, CancellationToken cancellationToken = default)
    {
        var source = CoreHttpClient.Source;
        var gate = _requests.GetOrAdd($"manifest:{source}", _ => new SemaphoreSlim(1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!refresh && _versions.TryGetValue(source, out var cached))
            {
                return ServiceResult<IReadOnlyList<GameVersionInfo>>.SuccessResult(cached);
            }

            using var document = JsonDocument.Parse(await _read(UrlHelper.GameVersion(source), cancellationToken));
            var root = document.RootElement;
            var latest = root.GetProperty("latest");
            var release = Text(latest, "release");
            var snapshot = Text(latest, "snapshot");
            var versions = root.GetProperty("versions").EnumerateArray().Select(item =>
            {
                var id = Text(item, "id");
                var time = Text(item, "releaseTime");
                return new GameVersionInfo(id, Text(item, "type"),
                    DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                        ? date
                        : null)
                {
                    MetadataUrl = Text(item, "url"), Sha1 = Text(item, "sha1"),
                    IsLatestRelease = id == release, IsLatestSnapshot = id == snapshot
                };
            }).ToArray();
            if (versions.Length == 0)
            {
                throw new InvalidDataException("版本清单为空。");
            }

            var result = Array.AsReadOnly(versions);
            _versions[source] = result;
            return ServiceResult<IReadOnlyList<GameVersionInfo>>.SuccessResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (IsRequestError(error))
        {
            _logger.LogWarning(error, "Could not retrieve Minecraft version catalog from {Source}", source);
            _versions.TryGetValue(source, out var previous);
            return ServiceResult<IReadOnlyList<GameVersionInfo>>.FailureResult(previous,
                "获取游戏版本失败，请重试。" + (previous is null ? "" : "已保留上次成功获取的版本。"));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ServiceResult<IReadOnlyList<GameLoaderVersion>>> GetLoaderVersionsAsync(
        string minecraftVersion, Loaders loader, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return ServiceResult<IReadOnlyList<GameLoaderVersion>>.FailureResult(message: "请选择游戏版本。");
        }

        if (loader == Loaders.Normal)
        {
            return ServiceResult<IReadOnlyList<GameLoaderVersion>>.SuccessResult([]);
        }

        if (loader is not (Loaders.Forge or Loaders.NeoForge or Loaders.Fabric or Loaders.OptiFine))
        {
            return ServiceResult<IReadOnlyList<GameLoaderVersion>>.FailureResult(message: "此加载器尚未提供下载支持。");
        }

        var source = CoreHttpClient.Source;
        var key = (source, minecraftVersion, loader);
        var gate = _requests.GetOrAdd($"loader:{source}:{minecraftVersion}:{loader}", _ => new SemaphoreSlim(1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (_loaders.TryGetValue(key, out var cached))
            {
                return ServiceResult<IReadOnlyList<GameLoaderVersion>>.SuccessResult(cached);
            }

            var mc = Uri.EscapeDataString(minecraftVersion);
            var url = loader switch
            {
                Loaders.Forge => UrlHelper.ForgeVersions(mc, source),
                Loaders.NeoForge => UrlHelper.NeoForgeVersions(mc, source),
                Loaders.Fabric => $"{UrlHelper.GetFabricMeta(source)}/loader/{mc}",
                _ => UrlHelper.GetOptifine(source)
            };
            var text = await _read(url, cancellationToken);
            var versions = ParseLoaders(text, minecraftVersion, loader, source);
            if (loader is Loaders.Forge or Loaders.NeoForge or Loaders.Fabric)
            {
                var sorted = versions.ToHashSet(StringComparer.Ordinal);
                StringHelper.VersionSort(sorted);
                versions = sorted.ToArray();
            }
            else if (loader == Loaders.OptiFine)
            {
                versions = versions.OrderBy(version => version.Contains("_pre", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(version => version, Comparer<string>.Create((left, right) =>
                        CultureInfo.InvariantCulture.CompareInfo.Compare(left, right,
                            CompareOptions.IgnoreCase | CompareOptions.NumericOrdering))).ToArray();
            }

            var result = Array.AsReadOnly(versions.Distinct(StringComparer.Ordinal)
                .Select(version => new GameLoaderVersion(loader, version)).ToArray());
            _loaders[key] = result;
            return ServiceResult<IReadOnlyList<GameLoaderVersion>>.SuccessResult(result,
                result.Count == 0 ? "此游戏版本暂无可用的加载器版本。" : "Success");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (IsRequestError(error))
        {
            _logger.LogWarning(error, "Could not retrieve {Loader} catalog for Minecraft {Version}", loader,
                minecraftVersion);
            return ServiceResult<IReadOnlyList<GameLoaderVersion>>.FailureResult(message: "获取加载器版本失败，请重试。");
        }
        finally
        {
            gate.Release();
        }
    }

    private static string[] ParseLoaders(string text, string mc, Loaders loader, SourceLocal source)
    {
        if (loader == Loaders.Forge && source != SourceLocal.BMCLAPI)
        {
            var elements = XDocument.Parse(text).Root?.Element("versioning")?.Element("versions")?.Elements("version")
                ?? throw new InvalidDataException("Forge 元数据缺少版本列表。");
            return elements.Select(item => item.Value)
                .Where(version => version.StartsWith(mc + "-", StringComparison.Ordinal))
                .Select(version => version[(mc.Length + 1)..].Split('-')[0]).Reverse().ToArray();
        }

        if (loader == Loaders.OptiFine && source != SourceLocal.BMCLAPI)
        {
            return ParseOptifineHtml(text, mc);
        }

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (loader == Loaders.NeoForge && source != SourceLocal.BMCLAPI)
        {
            return root.GetProperty("versions").EnumerateArray().Select(item => item.GetString()
                    ?? throw new InvalidDataException("NeoForge 版本无效。"))
                .Where(version => NeoMinecraftVersion(version) == mc).Reverse().ToArray();
        }

        return root.EnumerateArray().Where(item => loader != Loaders.OptiFine || Text(item, "mcversion") == mc)
            .Select(item => loader switch
            {
                Loaders.Fabric => Text(item.GetProperty("loader"), "version"),
                Loaders.OptiFine => $"{Text(item, "type")}_{Text(item, "patch")}",
                Loaders.NeoForge => Text(item, "version").Replace("neoforge-", "", StringComparison.Ordinal),
                _ => Text(item, "version")
            }).ToArray();
    }

    private static string[] ParseOptifineHtml(string text, string mc)
    {
        var html = new HtmlDocument();
        html.LoadHtml(text);
        var rows = html.DocumentNode.SelectNodes(
                "//tr[contains(concat(' ', normalize-space(@class), ' '), ' downloadLine ')]")
            ?? throw new InvalidDataException("OptiFine 元数据缺少下载列表。");
        var versions = new List<string>();
        foreach (var row in rows)
        {
            var file = row.SelectSingleNode(".//td[@class='colMirror']//a")?.GetAttributeValue("href", "");
            var label = row.SelectSingleNode(".//td[@class='colFile']")?.InnerText;
            if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(label))
            {
                throw new InvalidDataException("OptiFine 下载条目无效。");
            }

            var name = file.Replace("preview_OptiFine_", "").Replace("OptiFine_", "");
            if (name.Contains("f=", StringComparison.Ordinal))
            {
                name = name[(name.IndexOf("f=", StringComparison.Ordinal) + 2)..];
            }

            if (name.Split('_')[0] == mc)
            {
                versions.Add(HtmlEntity.DeEntitize(label).Trim().Replace("OptiFine ", "").Replace(" ", "_"));
            }
        }

        return versions.ToArray();
    }

    private static string? NeoMinecraftVersion(string version)
    {
        var parts = version.Split('+');
        var numbers = parts[0].Split('.');
        if (numbers.Length < 2 || !int.TryParse(numbers[0], out var major))
        {
            throw new InvalidDataException("NeoForge 版本格式无效。");
        }

        if (major == 0)
        {
            return null;
        }

        if (major < 26)
        {
            return $"1.{numbers[0]}.{numbers[1]}";
        }

        if (numbers.Length < 3)
        {
            throw new InvalidDataException("NeoForge 版本格式无效。");
        }

        return $"{numbers[0]}.{numbers[1]}" + (numbers[2] == "0" ? "" : $".{numbers[2]}")
            + (parts.Length > 1 ? $"-{parts[1]}" : "");
    }

    private static string Text(JsonElement item, string property)
    {
        return item.GetProperty(property).GetString() is { Length: > 0 } value
            ? value
            : throw new InvalidDataException($"元数据缺少 {property}。");
    }

    private static bool IsRequestError(Exception error)
    {
        return error is HttpRequestException or IOException
            or JsonException or XmlException or InvalidOperationException or KeyNotFoundException
            or OperationCanceledException;
    }

    private static async Task<string> ReadAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await CoreHttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}