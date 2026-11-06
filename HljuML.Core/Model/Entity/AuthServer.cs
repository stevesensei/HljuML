namespace HljuML.Core.Model.Entity;

/// <summary>
/// 自定义Ygg服务器实体
/// </summary>
public class CustomAuthServer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string RegisterAddress { get; set; } = string.Empty;
}