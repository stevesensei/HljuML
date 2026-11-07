using ColorMC.Core.Objs;

namespace HljuML.Core.Model.Entity;

/// <summary>
/// 游戏实例数据库存储结构
/// </summary>
public class GameInstanceData
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    //Base64
    public string Avatar { get; set; } = string.Empty;
    
    public string Version { get; set; } = string.Empty;
    public Loaders Loader { get; set; }
    public DateTime LastLogin { get; set; }

    public Guid? GameFolderId { get; set; }
    public GameFolder? GameFolder { get; set; }
    
    public Guid? LastUsedPlayerId { get; set; }
    public PlayerProfile? LastUsedPlayer { get; set; }
}
