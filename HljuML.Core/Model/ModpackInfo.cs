using ColorMC.Core.Objs;

namespace HljuML.Core.Model;

/// <summary>本地整合包的来源格式；通过压缩包内清单识别，不依赖文件扩展名。</summary>
public enum ModpackFormat { Modrinth, CurseForge }

/// <summary>导入前可读取的基本信息。加载器版本由清单指定，不自动升级。</summary>
public sealed record ModpackInfo(string Name, string PackVersion, string MinecraftVersion,
    Loaders Loader, string? LoaderVersion, ModpackFormat Format, int FileCount);

/// <summary>队列拥有的压缩包快照；用户修改或移动原文件不会改变已提交的安装。</summary>
internal sealed record PreparedModpack(string ArchivePath, ModpackInfo Info, bool IncludeOptionalFiles);
