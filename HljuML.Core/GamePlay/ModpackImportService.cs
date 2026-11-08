using System.IO.Compression;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Core.Model;
using HljuML.Shared.Model;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

/// <summary>本地整合包入口：检查清单、保存输入快照，并交给统一安装队列发布实例。</summary>
public sealed partial class ModpackImportService(GameInstallationService installations,
    ILogger<ModpackImportService> logger)
{
    private static string ImportDirectory => Path.Combine(LauncherPaths.ColorMcDirectory, "imports");

    /// <summary>仅读取包信息，不创建实例或访问平台 API；失败时返回可展示给用户的原因。</summary>
    public async Task<ServiceResult<ModpackInfo>> InspectAsync(string file, CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await Task.Run(() => InspectFile(file), cancellationToken);
            return ServiceResult<ModpackInfo>.SuccessResult(info);
        }
        catch (Exception error) when (IsImportError(error))
        {
            logger.LogWarning(error, "Could not inspect modpack {File}", file);
            return ServiceResult<ModpackInfo>.FailureResult(message: $"无法读取整合包：{error.Message}");
        }
    }

    /// <summary>
    /// 导入本地 .mrpack 或 CurseForge ZIP。返回下载管理器任务 ID；可自定义实例名和是否包含可选文件。
    /// 取消令牌覆盖排队及安装生命周期，成功发布前不会向实例列表暴露半成品。
    /// </summary>
    public async Task<ServiceResult<Guid>> EnqueueAsync(string file, string? instanceName = null,
        bool includeOptionalFiles = true, CancellationToken cancellationToken = default)
    {
        string? snapshot = null;
        var queued = false;
        try
        {
            ValidateSource(file);
            Directory.CreateDirectory(ImportDirectory);
            snapshot = ModpackArchive.ResolvePath(ImportDirectory, $"{Guid.NewGuid():N}.zip");
            // 队列只读取自己的快照，避免用户选完文件后原包被替换或删除。
            await using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
                await input.CopyToAsync(output, cancellationToken);
            var info = await Task.Run(() => InspectFile(snapshot), cancellationToken);
            var prepared = new PreparedModpack(snapshot, info, includeOptionalFiles);
            var request = new GameInstallationRequest(instanceName ?? $"{info.Name}-{info.PackVersion}",
                info.MinecraftVersion, info.Loader, info.LoaderVersion);
            var result = await installations.EnqueueModpackAsync(request, prepared, cancellationToken);
            queued = result.Status == ServiceStatus.Success;
            return result;
        }
        catch (Exception error) when (IsImportError(error))
        {
            logger.LogWarning(error, "Could not queue modpack {File}", file);
            return ServiceResult<Guid>.FailureResult(message: $"无法导入整合包：{error.Message}");
        }
        finally
        {
            if (!queued && snapshot is not null) ReleaseArchive(snapshot, logger);
        }
    }

    /// <summary>通过 ZIP 内容识别格式，扩展名仅用于文件选择器提示。</summary>
    private static ModpackInfo InspectFile(string file)
    {
        ValidateSource(file);
        using var archive = ZipFile.OpenRead(file);
        return ModpackArchive.Inspect(archive);
    }

    /// <summary>只接收可读取的绝对本地路径，并限制复制到快照目录的压缩包大小。</summary>
    private static void ValidateSource(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || !Path.IsPathFullyQualified(file) || !File.Exists(file))
            throw new ArgumentException("请选择存在的本地整合包文件。");
        if (new FileInfo(file).Length > ModpackArchive.MaximumArchiveSize)
            throw new InvalidDataException("整合包文件过大（最多 2 GiB）。");
    }

    /// <summary>仅删除 imports 目录内由本服务生成的快照，绝不删除用户选择的原文件。</summary>
    internal static void ReleaseArchive(string archive, ILogger log)
    {
        try
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(archive), "N", out _))
                throw new IOException("不是受管理的导入快照。");
            var expected = ModpackArchive.ResolvePath(ImportDirectory, Path.GetFileName(archive));
            if (!string.Equals(expected, Path.GetFullPath(archive), OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new IOException("导入快照不在受管理目录内。");
            File.Delete(expected);
        }
        catch (Exception error) when (IsImportError(error))
        { log.LogWarning(error, "Could not remove modpack snapshot {File}", archive); }
    }

    /// <summary>将可预期的输入、压缩格式和持久化错误转换为服务结果；取消异常继续向上传播。</summary>
    private static bool IsImportError(Exception error) => error is IOException or InvalidDataException
        or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException
        or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException
        or NotSupportedException or System.Data.Common.DbException;
}
