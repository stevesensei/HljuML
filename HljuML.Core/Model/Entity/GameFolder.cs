namespace HljuML.Core.Model.Entity;

public sealed class GameFolder
{
    public Guid Id { get; set; }
    public string AbsolutePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
