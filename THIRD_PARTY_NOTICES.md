# Third-party notices / 第三方声明

Reviewed on 2026-11-08 against the project's package references, resolved NuGet package metadata, bundled notices, and upstream license documents. This covers HljuML's application projects and the referenced ColorMC.Core project, rather than every project in the ColorMC repository.

本文件依据项目引用、已解析的 NuGet 包元数据、随包声明及上游许可证整理，范围包括 HljuML 应用工程和实际引用的 ColorMC.Core，不代表引用了 ColorMC 仓库中的全部软件库。

## Project license choice / 本项目的协议选择

Apache-2.0 is used for original HljuML source code. It matches ColorMC's license, includes an express patent grant, and can be used alongside the permissive MIT, BSD, and ISC licenses identified below. The reviewed library licenses do not require HljuML to adopt a copyleft license. MIT would also be a possible choice for original code, but would not replace the Apache-2.0 obligations attached to ColorMC.

HljuML 的原创代码采用 Apache-2.0，与 ColorMC 保持一致，包含明确的专利授权，也便于与下列宽松许可证依赖一同使用。此次核对的软件库协议没有要求本项目采用强制开源衍生作品的协议。原创代码也可选择 MIT，但这不会免除 ColorMC 自身的 Apache-2.0 义务。

See the [Apache-2.0 license text](LICENSE) and [upstream terms](https://www.apache.org/licenses/LICENSE-2.0).

## Libraries / 软件库

| Component | Referenced version | License | Source |
| --- | --- | --- | --- |
| ColorMC.Core | `LibColorMC` submodule | Apache-2.0 | [Local license](LibColorMC/LICENSE), [upstream](https://github.com/Coloryr/ColorMC/blob/main/LICENSE) |
| ForgeWrapper, OptifineWrapper, ColorMCASM embedded Java helpers | prism-2025-12-07 / 1.1 / 1.1-all | Upstream projects: MIT; bundled third-party classes retain their own terms | [ForgeWrapper](https://github.com/PrismLauncher/ForgeWrapper), [OptifineWrapper](https://github.com/Coloryr/OptifineWrapper), [ColorMCASM](https://github.com/Coloryr/ColorMCASM) |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter | 12.1.4 | MIT for framework/package code; Inter font has a separate OFL license | [Avalonia](https://github.com/AvaloniaUI/Avalonia/blob/master/licence.md) |
| TKCode.Arc, TKCode.Arc.Core, TKCode.Arc.Foundation | 0.3.2-preview.1 | MIT; embedded third-party content has separate notices | [License](https://github.com/AIDotNet/TKCode.Arc/blob/2f0d2b6b6a5fe4e1e7fc48e542acbd37cfbf18f7/LICENSE), [notices](https://github.com/AIDotNet/TKCode.Arc/blob/2f0d2b6b6a5fe4e1e7fc48e542acbd37cfbf18f7/THIRD-PARTY-NOTICES.md) |
| Microsoft.EntityFrameworkCore.*, Microsoft.Data.Sqlite.* | 10.0.14 | MIT | [EF Core](https://github.com/dotnet/efcore/blob/main/LICENSE.txt) |
| Microsoft.Extensions.* | Mainly 11.0.0-rc.3.26522.148 | MIT | [.NET](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| Microsoft.AspNetCore.OpenApi | 10.0.14 | MIT | [ASP.NET Core](https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt) |
| HtmlAgilityPack | 1.12.4 | MIT | [Project](https://github.com/zzzprojects/html-agility-pack) |
| Jint | 4.15.3 | BSD-2-Clause | [Project](https://github.com/sebastienros/jint) |
| Tomlyn | 2.10.1 | BSD-2-Clause | [Project](https://github.com/xoofx/Tomlyn) |
| K4os.Compression.LZ4, K4os.Compression.LZ4.Streams | 1.3.8 | MIT | [License](https://github.com/MiloszKrajewski/K4os.Compression.LZ4/blob/master/LICENSE) |
| K4os.Hash.xxHash | 1.0.8 | MIT | [License](https://github.com/MiloszKrajewski/K4os.Hash.xxHash/blob/master/LICENSE) |
| Ae.Dns.Client, Ae.Dns.Protocol | 3.1.0 | MIT | [Project](https://github.com/alanedwardes/Ae.Dns) |
| SharpCompress | 0.50.3 | MIT | [Project](https://github.com/adamhathcock/sharpcompress) |
| SkiaSharp and HarfBuzzSharp managed/native packages | 3.119.4 / 8.3.1.3 | MIT for bindings; native engines retain their upstream licenses and notices | [SkiaSharp](https://github.com/mono/SkiaSharp), [Skia](https://skia.googlesource.com/skia/+/main/LICENSE), [HarfBuzz](https://github.com/harfbuzz/harfbuzz/blob/main/COPYING) |
| SQLitePCLRaw.* | 2.1.13 | Apache-2.0 | [Project](https://github.com/ericsink/SQLitePCL.raw) |
| Acornima | 1.6.2 | BSD-3-Clause | [Project](https://github.com/adams85/acornima) |
| Markdig | 1.4.0 | BSD-2-Clause | [Project](https://github.com/xoofx/markdig) |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | BSD-style, three-clause license in the package's `LICENSE` file | [ANGLE](https://github.com/AvaloniaUI/angle) |
| TKCode.StreamMark.*, TextMateSharp.*, Onigwrap, and remaining Microsoft/System support packages | As resolved by NuGet | MIT at package level; preserve bundled notices for embedded content | [StreamMark](https://www.nuget.org/packages/TKCode.StreamMark.Avalonia/0.1.1), [TextMateSharp](https://github.com/danipen/TextMateSharp) |

The table summarizes license metadata; it does not replace upstream license texts or provide new rights to bundled content. Retain applicable copyright notices, license texts, third-party notices, and notices of modifications when redistributing dependencies. Native libraries and embedded grammars can carry additional notices beyond their .NET wrapper's package license.

上表是协议摘要，不替代上游许可证，也不为随附内容提供新的授权。再分发依赖时，应保留适用的版权声明、许可证、第三方声明及修改说明。原生库和内嵌语法资源可能包含独立于 .NET 包装库的声明。

## Fonts and icons / 字体与图标

- **Inter**, included through Avalonia.Fonts.Inter: [SIL Open Font License 1.1](https://github.com/rsms/inter/blob/master/LICENSE.txt).
- **Lucide icon geometry**, included through TKCode.Arc.Core: ISC, with MIT-covered Feather portions, as recorded in [TKCode.Arc's notices](https://github.com/AIDotNet/TKCode.Arc/blob/2f0d2b6b6a5fe4e1e7fc48e542acbd37cfbf18f7/THIRD-PARTY-NOTICES.md).
- **Minecraft, Forge, NeoForge, Fabric, and OptiFine images** in `HljuML.App/Assets/loaders/` identify their respective products. Their sources are the [Minecraft resource host](https://resources.download.minecraft.net/80/8030dd9dc315c0381d52c4782ea36c6baf6e8135), [Forge](https://files.minecraftforge.net/static/images/apple-touch-icon.png), [NeoForge](https://neoforged.net/img/authors/neoforged.png), [Fabric](https://fabricmc.net/assets/logo.png), and [OptiFine](https://optifine.net/images/of16r.png). No separate permissive redistribution license for these images was established by this review; they are not covered by HljuML's Apache-2.0 license.
- Other third-party artwork and trademarks retain their respective rights. This review of software-package licenses does not establish redistribution rights for all artwork in the repository.

Inter 字体采用 OFL-1.1；Arc 内嵌的 Lucide 图标保留 ISC/MIT 声明。各加载器及 Minecraft 的产品标志不随本项目改用 Apache-2.0，本次软件包协议核对也不代表已取得所有图片的再分发授权。

## Developer tools / 开发工具

`AvaloniaUI.DiagnosticsSupport` 2.2.3 does not declare an SPDX license expression in its NuGet metadata. Avalonia's [official FAQ](https://docs.avaloniaui.net/tools/faq) states that this integration bridge may be referenced in public projects and does not itself require a tool license to build. Opening the separate Avalonia Developer Tools requires the vendor's applicable license and account. Do not describe the tools or bridge as MIT-licensed solely because the Avalonia framework is MIT-licensed.

该调试桥接包的 NuGet 元数据未声明 SPDX 开源协议。官方说明允许公开项目引用，构建本身不要求工具许可证；打开独立的 Developer Tools 则需要遵守厂商的授权及账号要求。Avalonia 框架的 MIT 协议不能直接套用于这些开发工具。
