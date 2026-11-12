# Explorer 目录入口维护记录

## 范围与架构

- Services/ExplorerContextMenu.cs：启动参数解析、当前用户菜单注册/移除、单实例命名管道转发。服务不依赖页面控件。
- Program.cs：装配互斥锁和转发接收器；窗口就绪后开始接收，二次启动只转发不创建主窗体。
- UI/ExplorerDirectoryTargetForm.cs：只读待添加路径和模块内目标页选择，不自行保存数据。
- MainForm：管理菜单入口、UI 线程请求队列、仓库根路径解析与仓库导入；脚本导入由 ScriptRunnerPage 负责。
- 目录写入使用现有页副本和先保存后应用边界。模态编辑期间等待，避免列表编辑副本覆盖导入；重复项保留名称、备注、分组与顺序。Explorer 启动时抑制初始目录自动导入，取消选择不新增目录。

## 注册与兼容

注册位于 HKCU 的 Software\Classes\Directory\shell 和 Directory\Background\shell 下，仅操作 TortoiseGitLauncher.DirectoryActions。禁用偏好位于 Software\TortoiseGitLauncher 的 ExplorerContextMenuDisabled；移除后重启不恢复，手动启用会清除禁用标记。目录 JSON 格式及版本不变。首次启动新程序才会注册，移动程序后需要在新位置启动更新。Windows 11 下可能出现在“显示更多选项”。

## 验证状态

新增 Windows 组件测试源码覆盖参数解析、中文空格路径、根目录、错误参数、目标页默认值与排序、取消隔离、最小尺寸及模拟缩放。测试不注册菜单，不连接真实应用管道。本次仅做差异、C# 语法及编码静态检查；未编译、未运行组件测试或主应用、未覆盖 dist、未写真实注册表/配置、未提交。Explorer 端到端及进程转发待编译后验收。

需求见 ../../doc/00-requirements/accepted/2026-11-11-explorer-directory-actions.md。
