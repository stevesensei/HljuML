# HljuML 的 TKCode.Arc 约定

- 修改 Avalonia 界面前，加载项目技能 `.agents/skills/tkcode-arc/SKILL.md`，通过同目录的 `components.md` 选择控件，按 `checklist.md` 检查改动。
- 从 `.agents/skills/tkcode-arc/references/llms.txt` 查找页面，再在同目录的 `llms-full.txt` 中定位所需组件、示例和 API 表。按需读取对应章节。
- 当前项目使用 Avalonia 12.1.4 和 TKCode.Arc 0.3.2-preview.1。以 `HljuML.App/HljuML.App.csproj` 的包版本及随包的 XML 文档核对属性、事件和枚举。官网页面随上游更新，升级依赖时同步更新本地资料。
- Arc 控件使用 `xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"`。启动时通过 `AppBuilder.UseArc(arc => arc.UseArcControls())` 注册，通过 `ArcTheme.Apply(...)` 更新运行时主题。
- 在控件中使用 `{arc:ArcDesignTokenResource Name}` 等 Arc 设计令牌。自定义动效按 `^:motion-full` 和 `^:motion-reduced` 区分，遵循 Arc 的动效偏好。保留现有应用的视图模型和服务接线方式。

技能与参考资料取自 NuGet 0.3.2-preview.1 对应的 [上游源码快照](https://github.com/AIDotNet/TKCode.Arc/tree/2f0d2b6b6a5fe4e1e7fc48e542acbd37cfbf18f7/gallery/TKCode.Arc.Gallery.Llms)。
