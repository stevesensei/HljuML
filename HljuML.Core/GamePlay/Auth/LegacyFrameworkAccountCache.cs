using System.Text.Json;
using System.Text.Json.Nodes;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Login;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay.Auth;

/// <summary>仅清理旧版本写到 ColorMC 共享账户文件中的同一会话，不接管或清空其他启动器的账户库。</summary>
public sealed class LegacyFrameworkAccountCache(ILogger<LegacyFrameworkAccountCache> logger)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly string? _file;

    /// <summary>允许离线回归验证使用隔离的缓存文件，不读取用户真实认证数据。</summary>
    internal LegacyFrameworkAccountCache(ILogger<LegacyFrameworkAccountCache> logger, string file) : this(logger)
    {
        _file = file;
    }

    /// <summary>只有明确匹配档案会话的条目才删除；无法安全读写时保留原文件并记录不含令牌的日志。</summary>
    public async Task<bool> RemoveAsync(LoginObj? login, CancellationToken token = default)
    {
        if (login is null || login.AuthType == AuthType.Offline)
        {
            return true;
        }

        var root = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ColorMC")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorMC");
        try
        {
            await RemoveMatchingAsync(_file ?? Path.Combine(root, "auth.json"), login, token);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning("旧 ColorMC 认证缓存清理失败，已保留原文件；错误类型 {ErrorType}", error.GetType().Name);
            return false;
        }
    }

    /// <summary>锁定最新文件后保留未匹配的原始 JSON 节点，并使用同目录临时文件原子替换。</summary>
    internal static async Task RemoveMatchingAsync(string file, LoginObj login, CancellationToken token)
    {
        await Gate.WaitAsync(token);
        string? temporary = null;
        try
        {
            if (!File.Exists(file))
            {
                return;
            }

            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0
                || (File.GetAttributes(Path.GetDirectoryName(file)!) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("认证缓存路径存在重定向，未修改文件。");
            }

            // 读取期间拒绝其他写入；允许读取和替换，兼容 Windows 的 ReplaceFile 原子提交。
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                81920, FileOptions.Asynchronous);
            if (input.Length > 8 * 1024 * 1024)
            {
                throw new IOException("旧认证缓存超过大小限制。");
            }

            var entries = await JsonNode.ParseAsync(input, cancellationToken: token) as JsonArray
                ?? throw new JsonException("旧认证缓存不是数组。");
            var changed = false;
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                if (entries[index]?.Deserialize<LoginObj>() is { } candidate && SameSession(candidate, login))
                {
                    entries.RemoveAt(index);
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            temporary = Path.Combine(Path.GetDirectoryName(file)!, $".hljuml-auth-{Guid.NewGuid():N}.tmp");
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             81920, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(output, entries, cancellationToken: token);
                await output.FlushAsync(token);
                output.Flush(true);
            }

            token.ThrowIfCancellationRequested();
            File.Replace(temporary, file, null);
        }
        finally
        {
            try
            {
                if (temporary is not null)
                {
                    File.Delete(temporary);
                }
            }
            finally
            {
                Gate.Release();
            }
        }
    }

    /// <summary>UUID、认证类型、服务器与客户端会话必须一致；仅 UUID 相同不能证明属于本档案。</summary>
    internal static bool SameSession(LoginObj left, LoginObj right)
    {
        if (left.AuthType != right.AuthType ||
            !string.Equals(left.UUID, right.UUID, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (left.AuthType == AuthType.AuthlibInjector)
        {
            return AuthServerService.NormalizeServerAddress(left.Text1) is { } server
                && server == AuthServerService.NormalizeServerAddress(right.Text1)
                && !string.IsNullOrWhiteSpace(left.ClientToken) && left.ClientToken == right.ClientToken;
        }

        return left.AuthType == AuthType.OAuth && !string.IsNullOrWhiteSpace(left.Text1)
            && left.Text1 == right.Text1 && left.AccessToken == right.AccessToken;
    }
}