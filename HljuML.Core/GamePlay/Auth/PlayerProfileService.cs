using System.Text.Json;
using System.Net.Mail;
using ColorMC.Core.Game;
using ColorMC.Core.GuiHandle;
using ColorMC.Core.Helpers;
using ColorMC.Core.Net.Apis;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Login;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Core.Model.Entity;
using HljuML.Shared.Model;
using Microsoft.EntityFrameworkCore;

namespace HljuML.Core.GamePlay.Auth;

public class PlayerProfileService(HljuMLDbContext db, AuthServerService authServers)
{
    /// <summary>
    /// 获取全部Profile
    /// </summary>
    private IQueryable<PlayerProfile> Profiles =>
        db.PlayerProfiles.AsNoTracking().Include(profile => profile.CustomAuthServer);

    /// <summary>
    /// Authlib登录
    /// </summary>
    /// <param name="playerProfileId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ServiceResult<PlayerProfile>> CustomServerLogin(
        Guid playerProfileId, CancellationToken cancellationToken = default) =>
        LoginCustomProfile(playerProfileId, cancellationToken);

    /// <summary>
    /// Authlib登录。原始方式
    /// </summary>
    /// <param name="playerProfileId"></param>
    /// <param name="account"></param>
    /// <param name="password"></param>
    /// <param name="profileSelector"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public Task<ServiceResult<PlayerProfile>> CustomServerLogin(
        Guid playerProfileId, string account, string password, ILoginGui profileSelector,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password))
            return Task.FromResult(ServiceResult<PlayerProfile>.FailureResult(message: "请输入认证账号和密码。"));

        ArgumentNullException.ThrowIfNull(profileSelector);
        return LoginCustomProfile(playerProfileId, cancellationToken, (account.Trim(), password, profileSelector));
    }

    public async Task<ServiceResult<PlayerProfile>> CreateCustomPlayerProfile(
        Guid serverId, string account, string password, ILoginGui profileSelector,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password))
            return ServiceResult<PlayerProfile>.FailureResult(message: "请输入认证账号和密码。");

        ArgumentNullException.ThrowIfNull(profileSelector);
        var serverResult = await authServers.GetAuthServer(serverId, cancellationToken);
        var server = serverResult.Data;
        var serverUrl = AuthServerService.NormalizeServerAddress(server?.Address);
        if (server is null || serverUrl is null)
            return ServiceResult<PlayerProfile>.FailureResult(message: "请为该档案配置有效的自定义认证服务器地址。");

        account = account.Trim();
        var loginResult = await AuthenticateCustom(serverUrl,
            () => GameAuth.LoginAuthlibInjectorAsync(serverUrl, account, password, profileSelector, cancellationToken),
            cancellationToken);
        if (loginResult.Data is not { } login)
            return ServiceResult<PlayerProfile>.FailureResult(message: loginResult.Message);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await authServers.GetAuthServer(serverId, cancellationToken);
        if (current.Data?.Address != server.Address)
            return ServiceResult<PlayerProfile>.FailureResult(message: "认证服务器已变更，请重新登录。");

        var cache = JsonSerializer.Serialize(login);
        var entry = db.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            AuthMethod = AuthMethod.Custom,
            AuthServerId = serverId,
            PlayerName = login.UserName,
            PlayerEmail = MailAddress.TryCreate(account, out var email) && email.Address == account ? account : null,
            LoginCache = cache,
            LoginCacheHash = HashHelper.GenSha256(cache)
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }

        entry.Entity.CustomAuthServer = current.Data;
        return ServiceResult<PlayerProfile>.SuccessResult(entry.Entity);
    }

    public static async Task<string?> DownloadProfileSkin(PlayerProfile profile)
    {
        var serverUrl = AuthServerService.NormalizeServerAddress(profile.CustomAuthServer?.Address);
        if (profile.AuthMethod != AuthMethod.Custom || serverUrl is null || ReadCachedLogin(profile, serverUrl) is not { } login)
            return null;

        return (await PlayerSkinAPI.DownloadSkinAsync(login)).Skin;
    }

    /// <summary>
    /// 获取全部Profile
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ServiceResult<List<PlayerProfile>>> GetAllPlayerProfiles(
        CancellationToken cancellationToken = default)
    {
        var profiles = await Profiles.OrderBy(profile => profile.PlayerName)
            .ThenBy(profile => profile.Id).ToListAsync(cancellationToken);
        return ServiceResult<List<PlayerProfile>>.SuccessResult(profiles);
    }

    /// <summary>
    /// 获取指定玩家Profile
    /// </summary>
    /// <param name="playerProfileId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ServiceResult<PlayerProfile>> GetPlayerProfile(
        Guid playerProfileId, CancellationToken cancellationToken = default)
    {
        var profile = await Profiles.SingleOrDefaultAsync(item => item.Id == playerProfileId, cancellationToken);
        return profile is null
            ? ServiceResult<PlayerProfile>.FailureResult(message: "玩家档案不存在。")
            : ServiceResult<PlayerProfile>.SuccessResult(profile);
    }

    /// <summary>
    /// 删除Prodfile
    /// </summary>
    /// <param name="playerProfileId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ServiceResult<PlayerProfile>> DeletePlayerProfile(
        Guid playerProfileId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await GetPlayerProfile(playerProfileId, cancellationToken);
        if (result.Data is null)
            return result;

        await db.GameInstances.Where(instance => instance.LastUsedPlayerId == playerProfileId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(instance => instance.LastUsedPlayerId, (Guid?)null),
                cancellationToken);
        await db.PlayerProfiles.Where(profile => profile.Id == playerProfileId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// 更新Profile
    /// </summary>
    /// <param name="profile"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ServiceResult<bool>> UpdatePlayerProfile(
        PlayerProfile profile, CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateProfile(profile, cancellationToken);
        if (validationError is not null)
            return ServiceResult<bool>.FailureResult(message: validationError);

        var playerName = profile.PlayerName.Trim();
        var updated = await db.PlayerProfiles.Where(item => item.Id == profile.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PlayerName, playerName)
                .SetProperty(item => item.PlayerEmail, profile.PlayerEmail)
                .SetProperty(item => item.AuthMethod, profile.AuthMethod)
                .SetProperty(item => item.AuthServerId, profile.AuthServerId)
                .SetProperty(item => item.LoginCache, item =>
                    item.AuthMethod == profile.AuthMethod && item.AuthServerId == profile.AuthServerId
                    && item.PlayerName == playerName ? item.LoginCache : null)
                .SetProperty(item => item.LoginCacheHash, item =>
                    item.AuthMethod == profile.AuthMethod && item.AuthServerId == profile.AuthServerId
                    && item.PlayerName == playerName ? item.LoginCacheHash : null), cancellationToken);

        return updated == 0
            ? ServiceResult<bool>.FailureResult(message: "玩家档案不存在。")
            : ServiceResult<bool>.SuccessResult(true);
    }

    /// <summary>
    /// 创建Profile
    /// </summary>
    /// <param name="profile"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ServiceResult<PlayerProfile>> CreatePlayerProfile(
        PlayerProfile profile, CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateProfile(profile, cancellationToken);
        if (validationError is not null)
            return ServiceResult<PlayerProfile>.FailureResult(message: validationError);

        if (profile.Id != Guid.Empty && await db.PlayerProfiles.AnyAsync(item => item.Id == profile.Id, cancellationToken))
            return ServiceResult<PlayerProfile>.FailureResult(message: "玩家档案已存在。");

        var entry = db.PlayerProfiles.Add(new PlayerProfile
        {
            Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id,
            PlayerName = profile.PlayerName.Trim(),
            PlayerEmail = profile.PlayerEmail,
            AuthMethod = profile.AuthMethod,
            AuthServerId = profile.AuthServerId
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }

        return await GetPlayerProfile(entry.Entity.Id, cancellationToken);
    }

    /// <summary>
    /// 验证Profile是否有效
    /// </summary>
    /// <param name="profile"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<string?> ValidateProfile(PlayerProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.PlayerName))
            return "请输入玩家名称。";
        if (!Enum.IsDefined(profile.AuthMethod))
            return "认证方式无效。";

        if (profile.AuthMethod != AuthMethod.Custom)
            return profile.AuthServerId is null ? null : "当前认证方式无需指定自定义认证服务器。";

        if (profile.AuthServerId is { } serverId)
        {
            var result = await authServers.GetAuthServer(serverId, cancellationToken);
            if (result.Data is not null)
                return null;
        }

        return "请选择有效的自定义认证服务器。";
    }

    /// <summary>
    /// Authlib登录
    /// </summary>
    /// <param name="playerProfileId"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="credentials"></param>
    /// <returns></returns>
    private async Task<ServiceResult<PlayerProfile>> LoginCustomProfile(
        Guid playerProfileId, CancellationToken cancellationToken,
        (string Account, string Password, ILoginGui Selector)? credentials = null)
    {
        var result = await GetPlayerProfile(playerProfileId, cancellationToken);
        if (result.Data is not { } profile)
            return result;

        var address = profile.CustomAuthServer?.Address;
        var serverUrl = AuthServerService.NormalizeServerAddress(address);
        if (profile.AuthMethod != AuthMethod.Custom || serverUrl is null)
            return ServiceResult<PlayerProfile>.FailureResult(message: "请为该档案配置有效的自定义认证服务器地址。");

        ServiceResult<LoginObj> loginResult;
        if (credentials is { } input)
        {
            loginResult = await AuthenticateCustom(serverUrl,
                () => GameAuth.LoginAuthlibInjectorAsync(serverUrl, input.Account, input.Password, input.Selector, cancellationToken),
                cancellationToken);
        }
        else
        {
            if (ReadCachedLogin(profile, serverUrl) is not { } cached)
                return ServiceResult<PlayerProfile>.FailureResult(message: "请使用账号和密码重新登录。");

            loginResult = await AuthenticateCustom(serverUrl, () => cached.RefreshTokenAsync(cancellationToken), cancellationToken);
        }

        if (loginResult.Data is not { } login)
            return ServiceResult<PlayerProfile>.FailureResult(message: loginResult.Message);

        var cache = JsonSerializer.Serialize(login);
        var hash = HashHelper.GenSha256(cache);
        var updated = await db.PlayerProfiles
            .Where(item => item.Id == profile.Id && item.AuthMethod == AuthMethod.Custom
                && item.AuthServerId == profile.AuthServerId && item.PlayerName == profile.PlayerName
                && item.CustomAuthServer!.Address == address && item.LoginCache == profile.LoginCache
                && item.LoginCacheHash == profile.LoginCacheHash)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PlayerName, login.UserName)
                .SetProperty(item => item.LoginCache, cache)
                .SetProperty(item => item.LoginCacheHash, hash), cancellationToken);

        if (updated == 0)
            return ServiceResult<PlayerProfile>.FailureResult(message: "玩家档案已变更，请重新登录。");

        profile.PlayerName = login.UserName;
        profile.LoginCache = cache;
        profile.LoginCacheHash = hash;
        return ServiceResult<PlayerProfile>.SuccessResult(profile);
    }

    private static async Task<ServiceResult<LoginObj>> AuthenticateCustom(
        string serverUrl, Func<Task<LoginObj>> authenticate, CancellationToken cancellationToken)
    {
        try
        {
            var login = await authenticate();
            return IsValidCustomLogin(login, serverUrl)
                ? ServiceResult<LoginObj>.SuccessResult(login)
                : ServiceResult<LoginObj>.FailureResult(message: "认证服务器返回了不完整的登录信息。");
        }
        catch (LoginException ex) when (ex.Fail == LoginFailState.LoginTokenTimeout)
        {
            return ServiceResult<LoginObj>.FailureResult(message: "登录已失效，请使用账号和密码重新登录。");
        }
        catch (LoginException)
        {
            return ServiceResult<LoginObj>.FailureResult(message: "认证服务器登录失败，请检查账号、密码或所选角色。");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException ||
            ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ServiceResult<LoginObj>.FailureResult(message: "无法获取有效的认证服务器响应，请稍后重试。");
        }
    }

    /// <summary>
    /// 拿LoginObj
    /// </summary>
    /// <param name="profile"></param>
    /// <param name="serverUrl"></param>
    /// <returns></returns>
    private static LoginObj? ReadCachedLogin(PlayerProfile profile, string serverUrl)
    {
        if (profile.LoginCache is not { Length: > 0 } cache ||
            !string.Equals(HashHelper.GenSha256(cache), profile.LoginCacheHash, StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var login = JsonSerializer.Deserialize<LoginObj>(cache);
            return login is not null && IsValidCustomLogin(login, serverUrl) ? login : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 有效性验证
    /// </summary>
    /// <param name="login"></param>
    /// <param name="serverUrl"></param>
    /// <returns></returns>
    private static bool IsValidCustomLogin(LoginObj login, string serverUrl) =>
        login.AuthType == AuthType.AuthlibInjector && login.Text1 == serverUrl
        && !string.IsNullOrWhiteSpace(login.AccessToken) && !string.IsNullOrWhiteSpace(login.ClientToken)
        && !string.IsNullOrWhiteSpace(login.UserName) && Guid.TryParse(login.UUID, out _);

}
