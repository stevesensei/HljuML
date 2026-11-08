using System.Security.Cryptography;
using System.Text.Json;
using ColorMC.Core.Helpers;
using ColorMC.Core.Net;
using ColorMC.Core.Objs;
using HljuML.Shared.Model;

namespace HljuML.Core.GamePlay;

/// <summary>读取 Minecraft 声明的 Java 要求，并为导入实例准备匹配的运行环境。</summary>
public sealed partial class JavaRuntimeService(GameCatalogService catalog, JavaEnvService environments,
    Microsoft.Extensions.Logging.ILogger<JavaRuntimeService> logger)
{
    /// <summary>校验版本清单中的 SHA-1 后读取 majorVersion；旧版本没有声明时返回 null，不猜测版本。</summary>
    public async Task<int?> GetRequiredMajorAsync(string minecraftVersion, CancellationToken token = default)
    {
        var result = await catalog.GetVersionsAsync(cancellationToken: token);
        if (result.Status != ServiceStatus.Success || result.Data is null)
            throw new IOException(result.Message);
        var version = result.Data.FirstOrDefault(item => item.Id == minecraftVersion)
            ?? throw new InvalidDataException("找不到整合包指定的 Minecraft 版本。");
        if (string.IsNullOrWhiteSpace(version.MetadataUrl) || version.Sha1 is not { Length: 40 })
            throw new InvalidDataException("Minecraft 版本清单缺少下载地址或摘要。");
        
        using var response = await CoreHttpClient.GetAsync(
            UrlHelper.DownloadSourceChange(version.MetadataUrl, CoreHttpClient.Source), token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var bytes = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(bytes, token)) > 0)
        {
            if (buffer.Length + count > 8 * 1024 * 1024)
                throw new InvalidDataException("Minecraft 版本文件超过大小限制。");
            buffer.Write(bytes, 0, count);
        }
        var content = buffer.ToArray();
        if (!string.Equals(Convert.ToHexString(SHA1.HashData(content)),
            version.Sha1, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Minecraft 版本文件校验失败，请重新解析。");
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.GetProperty("id").GetString() != minecraftVersion)
            throw new InvalidDataException("Minecraft 版本文件与整合包要求不一致。");
        if (!root.TryGetProperty("javaVersion", out var java)) return null;
        if (!java.TryGetProperty("majorVersion", out var major) || !major.TryGetInt32(out var value) || value <= 0)
            throw new InvalidDataException("版本文件包含无效的 Java 主版本。");
        return value;
    }

    /// <summary>只复用主版本及架构均匹配的 Java，避免把较高主版本误认为所要求的环境。</summary>
    public async Task<JavaInfoObj?> FindExactAsync(int major, CancellationToken token = default)
    {
        var java = await environments.FindJavaAsync(major, token);
        var candidates = environments.Environments;
        if (java?.MajorVersion != major || java.Arch != ColorMC.Core.Utils.SystemInfo.SystemArch)
            candidates = await environments.ScanAsync(token);
        foreach (var candidate in candidates.Concat(new[] { java }.OfType<JavaInfoObj>())
            .Where(item => item.MajorVersion == major && item.Arch == ColorMC.Core.Utils.SystemInfo.SystemArch)
            .DistinctBy(item => item.Path))
        {
            var verified = await Task.Run(() => JavaHelper.GetJavaInfo(
                JavaExecutablePreference.PreferWindowed(candidate.Path)), token);
            if (verified?.MajorVersion == major && verified.Arch == candidate.Arch) return verified;
        }
        return null;
    }
}
