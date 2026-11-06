namespace HljuML.Core.Model.Entity;

public enum AuthMethod
{
    Offline = 0,
    Mojang = 1,
    Custom = 2,
}

/// <summary>
/// 玩家档案实体
/// </summary>
public class PlayerProfile
{
    public Guid Id { get; set; }
    public AuthMethod AuthMethod { get; set; }
    public Guid? AuthServerId { get; set; }
    public string PlayerName { get; set; } = null!;
    public string? PlayerEmail { get; set; }
    //从LoginObj拿的数据存一份
    public string? LoginCache { get;set; }
    public string? LoginCacheHash { get; set; }
    
    public CustomAuthServer? CustomAuthServer { get; set; }
}