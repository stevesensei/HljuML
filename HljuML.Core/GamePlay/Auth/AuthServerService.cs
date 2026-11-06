using System.Net.Http.Json;
using System.Text.Json;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Core.Model.Entity;
using HljuML.Shared.Model;
using Microsoft.EntityFrameworkCore;

namespace HljuML.Core.GamePlay.Auth;

public sealed class AuthServerService(HljuMLDbContext db)
{
    private const string ApiLocationHeader = "X-Authlib-Injector-API-Location";
    private static readonly HttpClient MetadataClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        MaxAutomaticRedirections = 5,
        UseCookies = false
    })
    {
        Timeout = TimeSpan.FromSeconds(15),
        MaxResponseContentBufferSize = 1024 * 1024
    };

    public async Task<ServiceResult<List<CustomAuthServer>>> GetAllAuthServers(
        CancellationToken cancellationToken = default)
    {
        var servers = await db.CustomAuthServers.AsNoTracking()
            .OrderBy(server => server.Name).ThenBy(server => server.Id).ToListAsync(cancellationToken);
        return ServiceResult<List<CustomAuthServer>>.SuccessResult(servers);
    }

    public async Task<ServiceResult<CustomAuthServer>> GetAuthServer(
        Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await db.CustomAuthServers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == serverId, cancellationToken);
        return server is null
            ? ServiceResult<CustomAuthServer>.FailureResult(message: "认证服务器不存在。")
            : ServiceResult<CustomAuthServer>.SuccessResult(server);
    }

    public async Task<ServiceResult<CustomAuthServer>> CreateAuthServer(
        string address, CancellationToken cancellationToken = default)
    {
        var result = await GetYggdrasilServerMetadata(address, cancellationToken);
        return result.Data is { } resolved
            ? await CreateAuthServer(resolved, cancellationToken)
            : ServiceResult<CustomAuthServer>.FailureResult(message: result.Message);
    }

    public async Task<ServiceResult<CustomAuthServer>> CreateAuthServer(
        ResolvedYggdrasilServer resolved, CancellationToken cancellationToken = default)
    {
        if (!resolved.ApiUri.IsAbsoluteUri || ParseServerUri(resolved.ApiUri.AbsoluteUri) is null)
            return ServiceResult<CustomAuthServer>.FailureResult(message: "请输入有效的 Yggdrasil 服务器地址。");

        var server = ToEntity(resolved);
        server.Id = Guid.NewGuid();
        var entry = db.CustomAuthServers.Add(server);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }

        return ServiceResult<CustomAuthServer>.SuccessResult(server);
    }

    public async Task<ServiceResult<CustomAuthServer>> UpdateAuthServer(
        Guid serverId, string address, CancellationToken cancellationToken = default)
    {
        var result = await GetYggdrasilServerMetadata(address, cancellationToken);
        if (result.Data is not { } resolved)
            return ServiceResult<CustomAuthServer>.FailureResult(message: result.Message);

        var server = ToEntity(resolved);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var existing = await GetAuthServer(serverId, cancellationToken);
        if (existing.Data is not { } previous)
            return existing;

        await db.CustomAuthServers.Where(item => item.Id == serverId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Address, server.Address)
                .SetProperty(item => item.Name, server.Name)
                .SetProperty(item => item.RegisterAddress, server.RegisterAddress), cancellationToken);

        if (!string.Equals(NormalizeServerAddress(previous.Address), server.Address, StringComparison.Ordinal))
        {
            await db.PlayerProfiles.Where(profile => profile.AuthServerId == serverId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(profile => profile.LoginCache, (string?)null)
                    .SetProperty(profile => profile.LoginCacheHash, (string?)null), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        server.Id = serverId;
        return ServiceResult<CustomAuthServer>.SuccessResult(server);
    }

    public async Task<ServiceResult<CustomAuthServer>> DeleteAuthServer(
        Guid serverId, CancellationToken cancellationToken = default)
    {
        var result = await GetAuthServer(serverId, cancellationToken);
        if (result.Data is null)
            return result;

        var deleted = await db.CustomAuthServers
            .Where(server => server.Id == serverId && !db.PlayerProfiles.Any(profile => profile.AuthServerId == server.Id))
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0
            ? ServiceResult<CustomAuthServer>.FailureResult(message: "认证服务器已删除或仍有玩家档案在使用。")
            : result;
    }

    public async Task<ServiceResult<ResolvedYggdrasilServer>> GetYggdrasilServerMetadata(
        string address, CancellationToken cancellationToken = default)
    {
        var apiUri = ParseServerUri(address);
        if (apiUri is null)
            return ServiceResult<ResolvedYggdrasilServer>.FailureResult(message: "请输入有效的 Yggdrasil 服务器地址。");

        var visited = new HashSet<Uri>();
        try
        {
            while (visited.Count < 5 && visited.Add(apiUri))
            {
                using var response = await MetadataClient.GetAsync(apiUri, cancellationToken);
                var responseUri = response.RequestMessage?.RequestUri ?? apiUri;
                if (ParseServerUri(responseUri.AbsoluteUri) is null)
                    return ServiceResult<ResolvedYggdrasilServer>.FailureResult(message: "认证服务器重定向地址无效。");

                if (response.Headers.TryGetValues(ApiLocationHeader, out var values))
                {
                    if (!Uri.TryCreate(responseUri, values.FirstOrDefault(), out var location)
                        || ParseServerUri(location.AbsoluteUri) is null)
                        return ServiceResult<ResolvedYggdrasilServer>.FailureResult(message: "认证服务器声明的 API 地址无效。");

                    if (location != responseUri)
                    {
                        apiUri = location;
                        continue;
                    }
                }

                response.EnsureSuccessStatusCode();
                var metadata = await response.Content.ReadFromJsonAsync<YggdrasilServerMetadata>(cancellationToken);
                if (metadata is not null && (metadata.Meta is not null || metadata.SkinDomains is not null
                    || !string.IsNullOrWhiteSpace(metadata.SignaturePublicKey)))
                {
                    return ServiceResult<ResolvedYggdrasilServer>.SuccessResult(new(responseUri, metadata));
                }

                return ServiceResult<ResolvedYggdrasilServer>.FailureResult(message: "响应中没有 Yggdrasil 元数据。");
            }

            return ServiceResult<ResolvedYggdrasilServer>.FailureResult(message: "认证服务器的 API 地址指向循环或跳转次数过多。");
        }
        catch (JsonException)
        {
            return ServiceResult<ResolvedYggdrasilServer>.FailureResult(message: "Yggdrasil 元数据的 JSON 格式无效。");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ServiceResult<ResolvedYggdrasilServer>.FailureResult(message: "无法读取认证服务器信息，请检查地址或网络。");
        }
    }

    internal static string? NormalizeServerAddress(string? address) =>
        ParseServerUri(address)?.AbsoluteUri.TrimEnd('/');

    private static CustomAuthServer ToEntity(ResolvedYggdrasilServer resolved)
    {
        var register = resolved.Metadata.Meta?.Links?.Register;
        return new CustomAuthServer
        {
            Name = resolved.DisplayName,
            Address = resolved.ApiUri.AbsoluteUri.TrimEnd('/'),
            RegisterAddress = !string.IsNullOrWhiteSpace(register)
                && Uri.TryCreate(resolved.ApiUri, register.Trim(), out var registerUri)
                && IsWebUri(registerUri) ? registerUri.AbsoluteUri : string.Empty
        };
    }

    private static Uri? ParseServerUri(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && IsWebUri(uri)
            && uri.Query.Length == 0 && uri.Fragment.Length == 0 ? uri : null;

    private static bool IsWebUri(Uri uri) =>
        (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        && uri.Host.Length > 0 && uri.UserInfo.Length == 0;
}

public sealed record ResolvedYggdrasilServer(Uri ApiUri, YggdrasilServerMetadata Metadata)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Metadata.Meta?.ServerName)
        ? ApiUri.Host : Metadata.Meta.ServerName.Trim();
}
