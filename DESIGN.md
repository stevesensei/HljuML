# HljuML 启动页

## 1. 目标

打开应用后直接进入启动器首页。顶部浮条切换首页、下载和设置，并提供窗口按钮。底部浮条显示当前账号、实例和启动操作。主页用错落卡片承载内容。

## 2. 色彩

| 资源 | 值 | 用途 |
| --- | --- | --- |
| `CanvasBrush` | `#E8F6FF` 至 `#88C5F0` | 窗口背景 |
| `HeroBrush` | `#D5EFFF` 至 `#B9DCF8` | 实例卡片封面底色 |
| `PanelBrush` | `#EAF7FCFF` | 两条浮层的半透明底色 |
| `GlassEdgeBrush` | `#E7FFFFFF` 至 `#B09FC9E8` | 面板受光边缘 |
| `Foreground`、`TextSecondary`、`Accent` | Arc 主题令牌 | 标题、说明与强调图标 |

标准控件使用 Arc 浅色外观和蓝色强调预设。卡片、正文和按钮从 Arc 读取表面、文字和交互状态。窗口背景与浮层材质使用 HljuML 的命名资源。

## 3. 字体

拉丁文字使用 Inter，中文使用系统字体回退。问候使用 Arc 的 `Type3XL`，产品名使用 `TypeLG`，正文使用 `TypeBase`，次要说明使用 `TypeSM`。

## 4. 布局

窗口按顶部浮条、滚动内容和底部浮条分三行。两条浮层在窗口内居中，导航按钮在顶部浮条的中心。垂直滚动只由中间的页面容器负责。主页问候与瀑布流左缘对齐。窗口在普通状态使用 `WindowRadius`，最大化时贴合屏幕边缘。

`MasonryPanel` 接受任意 Avalonia 子控件，按可用宽度和最小列宽决定列数。它将卡片放入当前最矮的可用列组。跨列卡片在列数减少时收缩到可用列数。

```xml
<layout:MasonryPanel MinimumColumnWidth="260" MaximumColumns="3" Spacing="16">
    <arc:Card layout:MasonryPanel.ColumnSpan="2" Title="跨两列的卡片" />
    <Border>
        <TextBlock Text="任意自定义内容" />
    </Border>
</layout:MasonryPanel>
```

AXAML 的 `layout` 命名空间是 `using:HljuML.App.Controls`。在 AXAML 中修改 `HomeFeed` 的直接子元素，或在代码中通过 `MainWindow.HomeCards` 增删控件。默认的 `ColumnSpan` 是 1。

## 5. 组件

- 上下浮条使用胶囊圆角、半透明底色、渐变边线和浮层阴影。
- 顶部当前页用 `Secondary` 按钮区分，其余导航和常规窗口按钮用 `Ghost`。底部启动按钮显示启动中、运行中或失败状态，失败时可重试。
- 底部左侧用 `arc:Avatar` 展示玩家头像，并显示名称与认证方式。右侧从左到右排列默认实例图标、实例名称与启动按钮。实例图标使用附件资源和 `InstanceIconRadius`。
- 瀑布流中的运行列表展示由本次启动器会话管理的实例。每行在实例图标右下角叠加玩家头像，右侧展示实例、玩家和认证信息，以及针对该进程的结束按钮。

## 6. 动效与深度

窗口背景、浮条和内容卡片以渐变、描边和阴影区分层级。交互反馈由 Arc 控件处理。

## 7. 可访问性与范围

顶部浮条的品牌区和留白可拖动窗口。窗口按钮提供可读名称，边缘和角落提供缩放区域。下载和设置暂用占位页。用户负责构建后的桌面 UI 检验。
