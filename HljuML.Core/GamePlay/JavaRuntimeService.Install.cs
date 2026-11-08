using System.Security.Cryptography;
using ColorMC.Core.Downloader;
using ColorMC.Core.GuiHandle;
using ColorMC.Core.Helpers;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Net.Apis;
using ColorMC.Core.Objs;
using ColorMC.Core.Utils;
using HljuML.Core.Infrastructure.Downloads;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

public sealed partial class JavaRuntimeService
{
    /// <summary>在统一安装任务内下载对应主版本的最新 JDK，校验后通过 ColorMC 解压并注册。</summary>
    internal async Task<JavaInfoObj> EnsureAsync(int major, Guid operationId,
        DownloadManagementService downloads, CancellationToken token)
    {
        if (await FindExactAsync(major, token) is { } existing) return existing;
        downloads.SetOperationPhase(operationId, $"正在获取 Java {major} 下载信息");
        var os = OperatingSystem.IsWindows() ? 1
            : OperatingSystem.IsLinux() ? 2
            : OperatingSystem.IsMacOS() ? 4
            : throw new PlatformNotSupportedException("当前系统不支持自动下载 Java。");
        var arch = SystemInfo.SystemArch switch
        {
            ArchEnum.x86_64 => "x64", ArchEnum.x86 => "x86", ArchEnum.aarch64 => "aarch64", ArchEnum.arm => "arm",
            _ => throw new PlatformNotSupportedException("当前处理器架构不支持自动下载 Java。")
        };
        // ColorMC 查询接口没有取消参数；只等待元数据，不允许取消后的结果继续安装。
        var releases = await AdoptiumApi
            .GetJavaListAsync(major.ToString(System.Globalization.CultureInfo.InvariantCulture), os)
            .WaitAsync(TimeSpan.FromSeconds(60), token);
        var release = releases?.FirstOrDefault(item => item.Binary.Os == AdoptiumApi.GetOs(os) && item.Binary.Architecture == arch && item.Binary.ImageType == "jdk")
                      ?? throw new IOException($"没有找到适用于当前系统和架构的 Java {major}，请手动安装后重试。");
        var package = release.Binary.Package;
        if (package.Checksum is not { Length: 64 } 
            || !package.Checksum.All(Uri.IsHexDigit)
            || !Uri.TryCreate(package.Link, UriKind.Absolute, out var source) ||
            source.Scheme != "https"
            || package.Size <= 0 || package.Size > 2L * 1024 * 1024 * 1024)
            throw new InvalidDataException("Java 下载源返回了无效的地址、大小或 SHA-256。");
        var extension = package.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ? ".tar.gz"
            : package.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip"
            : throw new InvalidDataException("Java 下载源返回了不支持的压缩格式。");
        var name = $"hljuml-java{major}-{Guid.NewGuid():N}";
        var archive = Path.Combine(DownloadManager.DownloadDir, name + extension);
        var installed = false;
        try
        {
            downloads.SetOperationPhase(operationId, $"正在下载 Java {major} · {arch}");
            // 使用启动器已有的下载条目，避免 ColorMC 内部重试影响整合包任务的取消和完成状态。
            await downloads.DownloadOperationFileAsync(operationId, $"Java {major}", archive, [source], token);
            await using (var input = File.OpenRead(archive))
            {
                if (input.Length != package.Size || !string.Equals(
                        Convert.ToHexString(await SHA256.HashDataAsync(input, token)), package.Checksum,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Java 压缩包大小或 SHA-256 校验失败。");
            }
            token.ThrowIfCancellationRequested();
            downloads.SetOperationPhase(operationId, $"正在解压并检查 Java {major}");
            // 不调用 InstallAsync：当前 ColorMC 实现将 UnzipJavaAsync 的 name/file 参数传反。
            var result = await JvmPath.UnzipJavaAsync(name, archive,
                new JavaExtractionProgress(downloads, operationId, token));
            token.ThrowIfCancellationRequested();
            var info = result.State ? JvmPath.GetInfo(name) : null;
            var executable = info is null ? null : JavaExecutablePreference.PreferWindowed(info.GetPath());
            var java = executable is null ? null : await Task.Run(() => JavaHelper.GetJavaInfo(executable), token);
            if (java?.MajorVersion != major || java.Arch != SystemInfo.SystemArch)
                throw new IOException($"Java {major} 安装检查失败（{result.Error}），请查看日志或手动安装。");
            installed = true;
            await environments.ScanAsync(token);
            return java;
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Could not prepare Java {Major} from {Source}", major,
                source.GetLeftPart(UriPartial.Path));
            throw;
        }
        finally
        {
            TryDeleteArchive(archive);
            if (!installed) CleanupJava(name);
        }
    }

    /// <summary>只清理本次生成的缓存包，失败时保留日志，不覆盖原始安装异常。</summary>
    private void TryDeleteArchive(string archive)
    {
        try
        {
            File.Delete(archive);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(error, "Could not delete Java download {Archive}", archive);
        }
    }

    /// <summary>失败回滚限于随机生成的直接子目录，遇到重定向目录则保留现场。</summary>
    private void CleanupJava(string name)
    {
        try
        {
            JvmPath.Remove(name);
            var root = Path.GetFullPath(JvmPath.JavaDir);
            var path = Path.GetFullPath(Path.Combine(root, name));
            if (Path.GetDirectoryName(path) != Path.TrimEndingDirectorySeparator(root))
                throw new IOException("Java 清理路径不在运行环境目录内。");
            if (!Directory.Exists(path)) return;
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0
                || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Java 清理目录存在重定向。");
            // JDK 可能包含合法的符号链接；Directory.Delete 删除链接本身，不递归进入其目标。
            Directory.Delete(path, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogWarning(error, "Could not clean incomplete Java environment {Name}", name);
        }
    }

    /// <summary>将 ColorMC 解压进度传回下载管理器，并在每个解压条目之间响应取消。</summary>
    private sealed class JavaExtractionProgress(
        DownloadManagementService downloads,
        Guid operationId,
        CancellationToken token) : IZipGui
    {
        public void ZipUpdate(string text, int size, int all)
        {
            token.ThrowIfCancellationRequested();
            downloads.SetOperationPhase(operationId, all > 0 ? $"正在解压 Java · {size}/{all}" : "正在解压 Java");
        }

        public Task<bool> FileRename(string? text) => Task.FromResult(false);
        public void Unzip() => token.ThrowIfCancellationRequested();
        public void Done() => token.ThrowIfCancellationRequested();
    }
}
