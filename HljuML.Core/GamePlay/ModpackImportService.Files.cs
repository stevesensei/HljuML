using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ColorMC.Core;
using ColorMC.Core.Helpers;
using ColorMC.Core.Net.Apis;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.CurseForge;
using HljuML.Core.Infrastructure.Downloads;
using HljuML.Core.Model;

namespace HljuML.Core.GamePlay;

public sealed partial class ModpackImportService
{
    /// <summary>准备实例专属内容；写入 UUID 暂存目录，下载校验完成后才叠加配置覆盖层。</summary>
    internal static async Task PrepareContentAsync(PreparedModpack pack, string stage, GameSettingObj game,
        DownloadManagementService downloads, Guid operationId, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(pack.ArchivePath);
        ModpackArchive.Inspect(archive);
        var manifestName = pack.Info.Format == ModpackFormat.Modrinth ? "modrinth.index.json" : "manifest.json";
        using var manifest = ModpackArchive.ReadManifest(archive.GetEntry(manifestName)!);
        var root = manifest.RootElement;
        var gameDirectory = Path.Combine(stage, Names.NameGameDir);
        Directory.CreateDirectory(gameDirectory);
        downloads.SetOperationPhase(operationId, "正在解析整合包文件清单");
        var files = pack.Info.Format == ModpackFormat.Modrinth
            ? ReadModrinthFiles(root, pack.IncludeOptionalFiles)
            : await ReadCurseForgeFilesAsync(root, game, pack.IncludeOptionalFiles, token);
        if (files.Sum(file => file.Size) > 16L * 1024 * 1024 * 1024)
            throw new InvalidDataException("整合包声明的下载总量过大。");
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
            if (!destinations.Add(ModpackArchive.NormalizePath(file.Path)))
                throw new InvalidDataException($"整合包包含重复下载目标：{file.Path}");

        downloads.SetOperationPhase(operationId, "正在下载并校验整合包文件");
        // 直接使用下载管理服务并逐个验证摘要，不采用会忽略下载返回值的 ColorMC InstallZip 入口。
        await Parallel.ForEachAsync(files, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = 4 },
            async (file, cancellation) =>
            {
                var destination = ModpackArchive.ResolvePath(gameDirectory, file.Path);
                await downloads.DownloadOperationFileAsync(operationId, file.Path, destination, file.Sources, cancellation);
                await VerifyFileAsync(destination, file, cancellation);
            });

        downloads.SetOperationPhase(operationId, "正在解压整合包配置");
        if (pack.Info.Format == ModpackFormat.Modrinth)
        {
            // 客户端覆盖层必须最后应用；server-overrides 不属于桌面客户端实例。
            await ExtractOverridesAsync(archive, "overrides", gameDirectory, token);
            await ExtractOverridesAsync(archive, "client-overrides", gameDirectory, token);
        }
        else
            await ExtractOverridesAsync(archive, ModpackArchive.RequiredString(root, "overrides"), gameDirectory, token);

        // 保留平台清单和来源标记，方便之后识别实例来源和实现整合包更新。
        await using var metadata = new FileStream(Path.Combine(stage, manifestName), FileMode.CreateNew, FileAccess.Write);
        await ModpackArchive.CopyEntryAsync(archive.GetEntry(manifestName)!, metadata, token);
        game.Modpack = true;
        game.ModPackType = pack.Info.Format == ModpackFormat.Modrinth ? SourceType.Modrinth : SourceType.CurseForge;
    }

    /// <summary>按客户端 env 过滤文件，保留多个下载地址和 SHA-1/SHA-512 双重校验。</summary>
    private static IReadOnlyList<PackFile> ReadModrinthFiles(JsonElement root, bool optional)
    {
        var files = new List<PackFile>();
        foreach (var entry in root.GetProperty("files").EnumerateArray())
        {
            if (entry.TryGetProperty("env", out var environment) && environment.TryGetProperty("client", out var client))
            {
                var requirement = client.GetString();
                if (requirement == "unsupported" || requirement == "optional" && !optional) continue;
                if (requirement is not ("required" or "optional"))
                    throw new InvalidDataException("整合包包含无效的客户端 env 设置。");
            }
            var path = ModpackArchive.NormalizePath(ModpackArchive.RequiredString(entry, "path"));
            var hashes = entry.GetProperty("hashes");
            var sha1 = ReadHash(hashes, "sha1", 40);
            var sha512 = ReadHash(hashes, "sha512", 128);
            var sources = entry.GetProperty("downloads").EnumerateArray().Select(url => ReadSource(url.GetString())).ToArray();
            if (sources.Length == 0) throw new InvalidDataException($"文件没有下载地址：{path}");
            var size = entry.GetProperty("fileSize").GetInt64();
            ValidateFileSize(size);
            files.Add(new(path, sources, size, sha1, sha512));
        }
        return files;
    }

    /// <summary>通过 ColorMC 的 CurseForge API 将 projectID/fileID 转为下载元数据；不猜测受限文件地址。</summary>
    private static async Task<IReadOnlyList<PackFile>> ReadCurseForgeFilesAsync(JsonElement root, GameSettingObj game,
        bool optional, CancellationToken token)
    {
        var references = root.GetProperty("files").EnumerateArray()
            .Where(entry => optional || !entry.TryGetProperty("required", out var required) || required.GetBoolean())
            .Select(entry => new CurseForgePackObj.FilesObj
            { ProjectID = entry.GetProperty("projectID").GetInt32(), FileID = entry.GetProperty("fileID").GetInt32() })
            .DistinctBy(entry => (entry.ProjectID, entry.FileID)).ToList();
        if (references.Count == 0) return [];
        if (references.Any(entry => entry.ProjectID <= 0 || entry.FileID <= 0))
            throw new InvalidDataException("CurseForge 项目 ID 或文件 ID 无效。");
        var resolved = await CurseForgeAPI.GetFilesAsync(references, token);
        token.ThrowIfCancellationRequested();
        var files = new List<PackFile>();
        foreach (var reference in references)
        {
            token.ThrowIfCancellationRequested();
            // 批量接口可能只返回部分文件，必须逐一补查，禁止漏装后仍然报告成功。
            var data = resolved?.FirstOrDefault(item => item.ModId == reference.ProjectID && item.Id == reference.FileID)
                ?? (await CurseForgeAPI.GetModAsync(reference, token))?.Data;
            if (data is null || data.ModId != reference.ProjectID || data.Id != reference.FileID)
                throw new IOException($"无法取得 CurseForge 文件 {reference.ProjectID}/{reference.FileID}。请检查 ColorMC 初始化中的 CurseForgeKey 和网络，具体原因见日志。");
            if (string.IsNullOrWhiteSpace(data.DownloadUrl))
                throw new IOException($"CurseForge 文件未提供 API 下载地址：{data.DisplayName}。请联系作者或从网站手动获取。");
            var filename = ModpackArchive.NormalizePath(data.FileName);
            if (filename.Contains('/')) throw new InvalidDataException("CurseForge 返回了无效的文件名。");
            var location = await CurseForgeHelper.GetItemPathAsync(game, data);
            if (location.FileType == FileType.Save)
                throw new IOException("此整合包引用了需要单独解压的世界文件，暂不支持自动导入该文件。");
            var hash = data.Hashes?.FirstOrDefault(item => item.Algo == 1)?.Value;
            if (hash is null || hash.Length != 40 || !hash.All(Uri.IsHexDigit))
                throw new InvalidDataException($"CurseForge 文件缺少有效的 SHA-1：{filename}");
            ValidateFileSize(data.FileLength);
            files.Add(new(ModpackArchive.NormalizePath(location.Path + "/" + filename),
                [ReadSource(data.DownloadUrl)], data.FileLength, hash, null));
        }
        return files;
    }

    /// <summary>仅展开指定覆盖层；使用边界校验后的路径，不把包内配置写入启动器目录。</summary>
    private static async Task ExtractOverridesAsync(ZipArchive archive, string prefix, string destination, CancellationToken token)
    {
        prefix = ModpackArchive.NormalizePath(prefix) + "/";
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = entry.FullName.Replace('\\', '/');
            if (entry.Name.Length == 0 || !name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var file = ModpackArchive.ResolvePath(destination, name[prefix.Length..]);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await using var output = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
            await ModpackArchive.CopyEntryAsync(entry, output, token);
        }
    }

    /// <summary>下载成功仍要验证长度和摘要，HTTP 200 或上游成功标志都不能代替内容校验。</summary>
    private static async Task VerifyFileAsync(string path, PackFile file, CancellationToken token)
    {
        await using var input = File.OpenRead(path);
        if (input.Length != file.Size) throw new IOException($"整合包文件大小不匹配：{file.Path}");
        var sha1 = Convert.ToHexString(await SHA1.HashDataAsync(input, token));
        if (!sha1.Equals(file.Sha1, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"整合包文件 SHA-1 校验失败：{file.Path}");
        if (file.Sha512 is not null)
        {
            input.Position = 0;
            var sha512 = Convert.ToHexString(await SHA512.HashDataAsync(input, token));
            if (!sha512.Equals(file.Sha512, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"整合包文件 SHA-512 校验失败：{file.Path}");
        }
    }

    /// <summary>校验摘要格式，避免空摘要使文件校验被意外跳过。</summary>
    private static string ReadHash(JsonElement hashes, string key, int length)
    {
        var hash = ModpackArchive.RequiredString(hashes, key);
        if (hash.Length != length || !hash.All(Uri.IsHexDigit)) throw new InvalidDataException($"无效的 {key} 校验值。");
        return hash;
    }

    /// <summary>只接受清单规范中的绝对 HTTPS 下载地址。</summary>
    private static Uri ReadSource(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps ? uri : throw new InvalidDataException("整合包下载地址必须为有效的 HTTPS 地址。");

    /// <summary>约束远程单文件体积，整体下载大小在开始传输前另外累计检查。</summary>
    private static void ValidateFileSize(long size)
    {
        if (size < 0 || size > ModpackArchive.MaximumArchiveSize) throw new InvalidDataException("整合包声明了无效的文件大小。");
    }

    private sealed record PackFile(string Path, IReadOnlyList<Uri> Sources, long Size, string Sha1, string? Sha512);
}
