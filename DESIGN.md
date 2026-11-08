# HljuML 界面约定

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
- 顶部当前页用 `Secondary` 按钮区分，其余导航和常规窗口按钮用 `Ghost`。底部启动按钮内显示当前阶段和文件处理数量，失败原因通过 Toast 展示。
- 底部左侧用 `arc:Avatar` 展示所选档案的皮肤头像、名称与认证方式。实例区在选定后显示图标和名称。窄窗口里缩小头像并隐藏下拉箭头，为启动状态保留空间。
- 首页下载队列卡片仅显示摘要。有未完成或失败任务时，底部启动栏上方显示下载管理器入口；所有任务成功后隐藏入口，已打开窗口保留结果。下载管理器按任务展示进度及当前文件，字节总量未知时按文件数计算进度。
- 瀑布流中的运行列表展示由本次启动器会话管理的实例。每行在实例图标右下角叠加玩家头像，右侧展示实例、玩家和认证信息，以及针对该进程的结束按钮。

## 6. 动效与深度

窗口背景、浮条和内容卡片以渐变、描边和阴影区分层级。交互反馈由 Arc 控件处理。

## 7. 可访问性与范围

顶部浮条的品牌区和留白可拖动窗口。窗口按钮提供可读名称，边缘和角落提供缩放区域。设置页提供游戏、启动器及关于三个分区，所有设置项提供 Tooltip。用户负责构建后的桌面 UI 检验。

## 8. 账户管理

点击底部头像或玩家名打开居中的账户窗口。左列使用 Arc 的弱强调表面放置认证入口，右列展示玩家档案与刚添加的服务器。两列可独立纵向滚动，添加服务器按钮固定在右下方。窗口使用现有浅色蓝色主题、Arc 面板圆角和表面令牌，采用紧凑的双栏布局。

添加服务器在同一个对话框内完成地址输入与解析结果确认。外置登录用同一对话框显示账号、密码和按需出现的角色选择。表单只显示操作标签、必要状态和错误。玩家档案优先显示从皮肤裁出的像素头像，下载期间显示默认头像。键盘可打开入口、提交或关闭对话框。

认证入口用线性图标区分微软、离线和自定义服务器。左栏只显示服务器名称和域名，长文字从末尾省略，悬停可查看完整名称和 API 地址。账户窗口打开时，主窗口使用 Arc `Scrim` 令牌降低背景亮度。账户窗口内的弹框遮罩和模糊沿账户窗口的圆角裁切。

## 9. 实例选择

实例选择使用与账户管理一致的居中圆角窗口和主窗口遮罩。左栏按游戏文件夹筛选并显示数量，右栏为可滚动的实例列表，展示名称、版本和加载器。列表支持键盘选择，确认后才更新启动条。首次打开启动器时等待用户选定实例。

## 10. 组件与样式归属

- `Resources/LauncherResources.axaml` 统一注册品牌画刷、窗口尺寸资源、窗口图标和间距转换器，由 `App.axaml` 引入。页面专属资源留在页面中。
- `Styles/Typography.axaml` 提供显式文字类：`app-body`、`app-heading`、`app-section-heading`、`app-page-title`、`app-description` 和 `app-caption`。页面不通过无类名的 `TextBlock` 选择器覆盖子控件文字。
- `Styles/Surfaces.axaml` 提供 `app-panel` 和 `app-dialog-frame`；`Navigation.axaml` 提供 `app-nav` 及其选中态。`Shell.axaml` 仅维护窗口与启动栏外观。
- `Styles/ArcCompatibility.axaml` 集中维护依赖 TKCode.Arc 0.3.2-preview.1 内部模板部件的兼容样式；升级 Arc 时检查该文件。
- `Controls/PageHeader` 通过 `Title`、`Description`、`TitleFontSize` 和 `Actions` 插槽组合页面标题。`Controls/DialogHeader` 提供选择窗口标题栏，通过 `CloseRequested` 将关闭交回所属窗口。
- `Views/Shell` 维护顶栏和启动栏，通过事件向主窗口请求导航、窗口操作和游戏启动。主窗口保留窗口生命周期、下载管理、选择窗口及导入遮罩的协调逻辑。
- `Views/Home` 维护首页，`Views/Settings` 维护游戏、启动器、关于三个分区，`Views/Downloads` 维护版本目录和安装配置。子视图继承原有 ViewModel，不创建独立业务服务，也不将 `DataContext` 设置为自身。
- 页面继续决定滚动容器和列表虚拟化边界，公共标题和面板不自动包裹 `ScrollViewer`。设置字段保留标签、校验和 Tooltip；开发人员采用圆形头像、两行文字及右侧操作按钮。
