# 页面扩展指南

## 当前页面结构

左侧“功能导航”当前包含两个入口：

- `仓库管理`：现有仓库选择和 TortoiseGit 命令页面。
- `脚本执行`：自定义脚本功能页面，代码位于 `Features/ScriptRunner/`。

主窗体创建页面实例后统一放入页面宿主容器，通过显隐和层级切换页面。页面切换时不销毁实例，确保脚本执行页后续可以在离开页面后继续维护运行状态和输出。

## 页面职责

- `UI/MainForm.cs` 负责导航按钮、页面宿主和页面切换。
- `UI/MainForm.RepositoryManagement.cs` 负责仓库管理页面的布局与交互。
- `Features/ScriptRunner/ScriptRunnerPage.cs` 负责脚本执行页面的布局与交互。
- 页面内部的持久化和进程生命周期应继续下沉到 feature 内的独立类型，不能放回主窗体。

## 目录页与功能页的区别

目录页位于两个已有功能页内部，不新增左侧导航入口。共享的 `DirectoryPageSelectorButton` 提供标题菜单，`DirectoryPageManagerForm<TPage>` 管理页编辑副本；仓库逻辑在 `MainForm.DirectoryPages.cs`，脚本逻辑在 `ScriptRunnerPage.DirectoryPages.cs`。

每次切换先保存候选配置，成功后才刷新当前页目录列表与选择。仓库备注须先成功保存；脚本切页保留原页面与运行器，不销毁运行实例。当前页目录列表编辑也通过保存回调处理，失败时保留对话框供重试。

## 新增页面建议

新增页面时，应先定义页面职责，再确认：

- 导航入口和选中状态。
- 页面实例是常驻还是按需创建。
- 页面切换后的后台任务生命周期。
- 页面间需要共享的最小状态。
- 数据读写和错误呈现边界。
- 窗口最小尺寸、高 DPI 和滚动范围。
