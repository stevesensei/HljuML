using System.IO.Compression;
using System.Security.Cryptography;
using ColorMC.Core.Downloader;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Objs;

namespace HljuML.Core.GamePlay;

public sealed partial class GameInstallationService
{
    private static async Task<IReadOnlyList<Artifact>> DownloadFilesAsync(IEnumerable<FileItemObj> files,
        IReadOnlyDictionary<string, long> sizes, CancellationToken token)
    {
        var inventory = new List<Artifact>();
        var needed = new List<FileItemObj>();
        var callbacks = new List<(FileItemObj File, Action<Stream> Run)>();
        foreach (var file in files.DistinctBy(item => Path.GetFullPath(item.Local), StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            EnsureCachePath(file.Local);
            var size = file.Sha1 is { } hash && sizes.TryGetValue(hash, out var length) ? (long?)length : null;
            var artifact = new Artifact(Path.GetFullPath(file.Local), file.Sha1, file.Sha256, size,
                string.Equals(Path.GetExtension(file.Local), ".jar", StringComparison.OrdinalIgnoreCase));
            inventory.Add(artifact);
            if (file.Later is not null)
            {
                callbacks.Add((file, file.Later));
                file.Later = null;
            }

            if (await IsValidAsync(artifact, token))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(file.Url))
            {
                throw new IOException($"必需的本地文件缺失或无效：{file.Name}");
            }

            if (!Uri.TryCreate(file.Url, UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
            {
                throw new IOException($"文件下载地址无效：{file.Name}");
            }

            file.Overwrite = true;
            needed.Add(file);
        }

        if (needed.Count > 0 && !await DownloadManager.StartAsync(needed, token: token))
        {
            token.ThrowIfCancellationRequested();
            throw new IOException("游戏下载失败，请检查网络或下载源后重试。");
        }

        foreach (var artifact in inventory)
            await RequireValidAsync(artifact, token);
        foreach (var (file, run) in callbacks)
        {
            token.ThrowIfCancellationRequested();
            using var stream = File.OpenRead(file.Local);
            run(stream);
        }

        return inventory;
    }

    private static void EnsureCachePath(string file)
    {
        var full = Path.GetFullPath(file);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var directory in new[] { VersionPath.BaseDir, LibrariesPath.BaseDir, AssetsPath.BaseDir })
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            if (full.StartsWith(root + Path.DirectorySeparatorChar, comparison))
            {
                for (var parent = Path.GetDirectoryName(full);
                     parent is not null;
                     parent = Path.GetDirectoryName(parent))
                {
                    if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new IOException("共享缓存路径包含重定向目录，已停止安装。");
                    }

                    if (string.Equals(parent, root, comparison))
                    {
                        break;
                    }
                }

                if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("共享缓存文件为重定向文件，已停止安装。");
                }

                return;
            }
        }

        throw new IOException("版本元数据包含超出共享缓存目录的文件路径。");
    }

    private static async Task RequireValidAsync(Artifact artifact, CancellationToken token)
    {
        if (!await IsValidAsync(artifact, token))
        {
            throw new IOException($"文件校验失败：{Path.GetFileName(artifact.Path)}");
        }
    }

    private static async Task<bool> IsValidAsync(Artifact artifact, CancellationToken token)
    {
        try
        {
            EnsureCachePath(artifact.Path);
            if (!File.Exists(artifact.Path))
            {
                return false;
            }

            await using var stream = new FileStream(artifact.Path, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (artifact.Size is { } size && stream.Length != size)
            {
                return false;
            }

            if (stream.Length == 0 && (artifact.Archive || (artifact.Size is null
                    && string.IsNullOrWhiteSpace(artifact.Sha1) && string.IsNullOrWhiteSpace(artifact.Sha256))))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(artifact.Sha1))
            {
                var hash = await SHA1.HashDataAsync(stream, token);
                if (!Convert.ToHexString(hash).Equals(artifact.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                stream.Position = 0;
            }

            if (!string.IsNullOrWhiteSpace(artifact.Sha256))
            {
                var hash = await SHA256.HashDataAsync(stream, token);
                if (!Convert.ToHexString(hash).Equals(artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                stream.Position = 0;
            }

            // Several upstream loaders publish no digest; require a fully readable JAR instead.
            if (artifact.Archive && string.IsNullOrWhiteSpace(artifact.Sha1)
                && string.IsNullOrWhiteSpace(artifact.Sha256))
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
                if (archive.Entries.Count == 0)
                {
                    return false;
                }

                foreach (var entry in archive.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    await using var content = entry.Open();
                    await content.CopyToAsync(Stream.Null, token);
                }
            }

            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record Artifact(string Path, string? Sha1, string? Sha256, long? Size, bool Archive);
}