using System.IO.Compression;
using System.Text.Json;
using ColorMC.Core.Objs;
using HljuML.Core.Model;

namespace HljuML.Core.GamePlay;

/// <summary>清单解析和路径边界。所有压缩包条目都必须先通过检查，再允许写入实例。</summary>
internal static class ModpackArchive
{
    internal const long MaximumArchiveSize = 2L * 1024 * 1024 * 1024;
    private const long MaximumExpandedSize = 16L * 1024 * 1024 * 1024;

    /// <summary>限制解压规模并拒绝重复路径、符号链接和路径穿越，兼顾跨平台文件名冲突。</summary>
    internal static ModpackInfo Inspect(ZipArchive archive)
    {
        if (archive.Entries.Count > 50_000)
        {
            throw new InvalidDataException("整合包条目过多（最多 50000 项）。");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long length = 0;
        foreach (var entry in archive.Entries)
        {
            var path = NormalizePath(entry.FullName.TrimEnd('/'));
            if (!names.Add(path))
            {
                throw new InvalidDataException($"压缩包存在重复路径：{path}");
            }

            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000
                || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("整合包不能包含符号链接或重定向条目。");
            }

            length = checked(length + entry.Length);
            if (entry.Length > MaximumArchiveSize || length > MaximumExpandedSize)
            {
                throw new InvalidDataException("整合包解压后的文件过大。");
            }
        }

        var modrinth = archive.GetEntry("modrinth.index.json");
        var curseforge = archive.GetEntry("manifest.json");
        if (modrinth is null == curseforge is null)
        {
            throw new InvalidDataException("请选择包含唯一 Modrinth 或 CurseForge 根目录清单的整合包。");
        }

        using var document = ReadManifest(modrinth ?? curseforge!);
        var root = document.RootElement;
        var name = RequiredString(root, "name");
        var format = modrinth is not null ? ModpackFormat.Modrinth : ModpackFormat.CurseForge;
        string version, minecraft;
        var loader = Loaders.Normal;
        string? loaderVersion = null;
        if (format == ModpackFormat.Modrinth)
        {
            if (root.GetProperty("formatVersion").GetInt32() != 1 || RequiredString(root, "game") != "minecraft")
            {
                throw new InvalidDataException("不支持此 Modrinth 清单格式或游戏类型。");
            }

            version = RequiredString(root, "versionId");
            var dependencies = root.GetProperty("dependencies");
            minecraft = RequiredString(dependencies, "minecraft");
            foreach (var dependency in dependencies.EnumerateObject())
            {
                if (dependency.Name == "minecraft")
                {
                    continue;
                }

                var selected = dependency.Name switch
                {
                    "forge" => Loaders.Forge, "neoforge" => Loaders.NeoForge,
                    "fabric-loader" => Loaders.Fabric,
                    _ => throw new InvalidDataException($"暂不支持整合包依赖：{dependency.Name}")
                };
                if (loader != Loaders.Normal)
                {
                    throw new InvalidDataException("整合包指定了多个不兼容的加载器。");
                }

                loader = selected;
                loaderVersion = dependency.Value.GetString();
            }
        }
        else
        {
            if (RequiredString(root, "manifestType") != "minecraftModpack"
                || root.GetProperty("manifestVersion").GetInt32() != 1)
            {
                throw new InvalidDataException("不支持此 CurseForge 清单格式。");
            }

            version = RequiredString(root, "version");
            var game = root.GetProperty("minecraft");
            minecraft = RequiredString(game, "version");
            var loaders = game.GetProperty("modLoaders").EnumerateArray().ToArray();
            var primary = loaders.Where(item => item.TryGetProperty("primary", out var flag) && flag.GetBoolean())
                .ToArray();
            if (primary.Length > 1 || (primary.Length == 0 && loaders.Length > 1))
            {
                throw new InvalidDataException("整合包没有指定唯一的主加载器。");
            }

            if (loaders.Length > 0)
            {
                var id = RequiredString(primary.Length == 1 ? primary[0] : loaders[0], "id");
                var separator = id.IndexOf('-');
                if (separator < 1)
                {
                    throw new InvalidDataException("整合包的加载器标识无效。");
                }

                loader = id[..separator] switch
                {
                    "forge" => Loaders.Forge, "neoforge" => Loaders.NeoForge, "fabric" => Loaders.Fabric,
                    _ => throw new InvalidDataException($"暂不支持加载器：{id}")
                };
                loaderVersion = id[(separator + 1)..];
                if (loaderVersion.StartsWith(minecraft + "-", StringComparison.Ordinal))
                {
                    loaderVersion = loaderVersion[(minecraft.Length + 1)..];
                }
            }

            NormalizePath(RequiredString(root, "overrides"));
        }

        if (loader != Loaders.Normal && string.IsNullOrWhiteSpace(loaderVersion))
        {
            throw new InvalidDataException("整合包缺少加载器版本。");
        }

        var fileCount = root.GetProperty("files").GetArrayLength();
        if (fileCount > 50_000)
        {
            throw new InvalidDataException("整合包下载文件过多（最多 50000 项）。");
        }

        return new ModpackInfo(name, version, minecraft, loader, loaderVersion, format, fileCount);
    }

    /// <summary>清单单独限制为 8 MiB，避免仅预览信息时加载巨大的 JSON。</summary>
    internal static JsonDocument ReadManifest(ZipArchiveEntry entry)
    {
        if (entry.Length > 8 * 1024 * 1024)
        {
            throw new InvalidDataException("整合包清单过大。");
        }

        using var stream = entry.Open();
        using var content = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            if (content.Length + count > entry.Length)
            {
                throw new InvalidDataException("整合包清单实际长度超出声明。");
            }

            content.Write(buffer, 0, count);
        }

        return JsonDocument.Parse(content.ToArray());
    }

    /// <summary>限制实际解压字节数，不能仅信任 ZIP 中声明的长度，防止伪造长度导致解压膨胀。</summary>
    internal static async Task CopyEntryAsync(ZipArchiveEntry entry, Stream output, CancellationToken token)
    {
        await using var input = entry.Open();
        var buffer = new byte[81920];
        long length = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            length += count;
            if (length > entry.Length)
            {
                throw new InvalidDataException($"解压内容超出声明长度：{entry.FullName}");
            }

            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }

        if (length != entry.Length)
        {
            throw new InvalidDataException($"压缩包文件不完整：{entry.FullName}");
        }
    }

    /// <summary>必填清单字段缺失、为空或类型错误时拒绝导入。</summary>
    internal static string RequiredString(JsonElement element, string name)
    {
        return element.GetProperty(name).GetString() is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"整合包缺少有效字段：{name}");
    }

    /// <summary>同时按 Windows 和 Unix 的分隔符检查，禁止盘符、绝对路径及父目录跳转。</summary>
    internal static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/') || normalized.Contains(':')
            || normalized.Any(char.IsControl)
            || normalized.Split('/').Any(part => part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ')))
        {
            throw new InvalidDataException($"整合包包含不安全的相对路径：{path}");
        }

        return normalized;
    }

    /// <summary>返回实例内部路径，并检查已有父目录及目标文件，防止通过重定向写出实例。</summary>
    internal static string ResolvePath(string root, string relative)
    {
        var basePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var result = Path.GetFullPath(Path.Combine(basePath, NormalizePath(relative)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!result.StartsWith(basePath + Path.DirectorySeparatorChar, comparison))
        {
            throw new InvalidDataException("整合包文件超出了实例目录。");
        }

        for (var current = result; current is not null; current = Path.GetDirectoryName(current))
        {
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("整合包路径包含重定向文件或目录。");
            }

            if (string.Equals(current, basePath, comparison))
            {
                break;
            }
        }

        return result;
    }
}