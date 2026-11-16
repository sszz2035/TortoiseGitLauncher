# 模块职责图

## 当前目录

```text
TortoiseGitLauncher/
  Program.cs
  Features/
    ScriptRunner/
      ExecutionDirectoryManagerForm.cs
      ScriptConfiguration.cs
      ScriptConfigurationForm.cs
      ScriptConfigurationManagerForm.cs
      ScriptProcessRunner.cs
      ScriptRunInstance.cs
      ScriptRunnerPage.cs
      ScriptRunnerPage.DirectoryPages.cs
      ScriptRunnerPathHelper.cs
      ScriptRunnerSettings.cs
      ScriptRunnerStore.cs
  UI/
    MainForm.cs
    MainForm.RepositoryManagement.cs
    MainForm.DirectoryPages.cs
    DirectoryPageManagerForm.cs
    DirectoryTransferTargetForm.cs
    DirectoryTransferFeedback.cs
    RepositoryListManagerForm.cs
    Controls/
      CardPanel.cs
      RepositoryDetailsControl.cs
      DirectoryPageSelectorButton.cs
    Icons/
      IconFactory.cs
  Models/
    CommandDefinitions.cs
    DirectoryPage.cs
    DirectoryTransferContracts.cs
    RepositoryEntry.cs
  Services/
    RepositoryHistoryStore.cs
    SafeConfigurationFile.cs
    DirectoryPageSelection.cs
    DirectoryPageTransfer.cs
    RepositoryToScriptTransfer.cs
    DirectoryTransferSave.cs
    TortoiseGitLocator.cs
    GitPathHelper.cs
    RepositoryBranchReader.cs
    RepositoryCommitReader.cs
```

## 当前职责

| 模块 | 职责 |
| --- | --- |
| `Program.cs` | 进程入口、WinForms 初始化、顶层异常记录 |
| `Features/ScriptRunner` | 脚本执行页面、目录与脚本配置、持久化、运行实例和进程生命周期 |
| `UI/MainForm.cs` | 应用外壳、导航和页面装配 |
| `UI/MainForm.RepositoryManagement.cs` | 仓库管理页及 TortoiseGit 命令交互 |
| `UI/RepositoryListManagerForm.cs` | 仓库列表及跨页移动/复制编辑事务，持有全部仓库页与执行目录页的深克隆；跨模块部分保存时锁定编辑供重试 |
| `UI/MainForm.DirectoryPages.cs` | 仓库目录页切换、页管理与保存成功后替换状态 |
| `UI/DirectoryPageManagerForm.cs` | 两模块共享的页管理编辑副本、名称校验、排序与删除确认 |
| `UI/Controls/DirectoryPageSelectorButton.cs` | 标题切页菜单、当前页标记与管理入口 |
| `Services/DirectoryPageSelection.cs` | 仅从本页现有可用条目恢复选择，避免删除项复活 |
| `Models/DirectoryTransferContracts.cs` | 两模块转移的最小条目/分组协议；路径显式实现，不改变 JSON 格式 |
| `Services/DirectoryPageTransfer.cs` | 共享转移规则、页内校验、路径去重、组标识与名称生成；只修改编辑副本，不读写磁盘 |
| `UI/DirectoryTransferTargetForm.cs`、`UI/DirectoryTransferFeedback.cs` | 目标模块/页选择（脚本源仅本模块）、结果统计与可滚动的跳过路径明细 |
| Services/RepositoryToScriptTransfer.cs | 仓库到执行目录的单向类型映射，复用转移规则，不触碰磁盘 |
| Services/DirectoryTransferSave.cs | 目标优先保存与源失败重试状态，不重复保存已完成的目标 |
| UI/Controls | 可复用自绘控件 |
| `UI/Controls/RepositoryDetailsControl.cs` | 仓库目录、分支、最新提交时间和备注展示；可见时后台轮询、过期结果隔离与生命周期清理 |
| `UI/Icons` | 内置矢量图标枚举和绘制 |
| `Models` | 仓库与命令定义，DirectoryPage 提供页标识、页名、排序和页级选择的共同元数据 |
| `Services` | 仓库历史、Git 路径和 TortoiseGit 定位 |
| `Services/SafeConfigurationFile.cs` | 配置版本保护、迁移备份、落盘刷新及原子替换 |
| `Services/RepositoryBranchReader.cs` | 只读解析工作目录的 HEAD，兼容 `.git` 目录及 gitdir 文件，不依赖 Git CLI |
| `Services/RepositoryCommitReader.cs` | 定位本机 Git，通过有超时的只读命令查询 HEAD 提交者时间并转换为本机时区 |

## 脚本执行功能实际边界

脚本执行按功能纵向组织在 `Features/ScriptRunner/`：

- `ScriptRunnerPage`：页面状态、目录与脚本入口、实例标签页和用户操作。
- `ScriptRunnerPage.DirectoryPages.cs`：执行目录页切换、页管理和保存副本；复用页面及进程运行器，保持运行实例独立。
- `ExecutionDirectoryManagerForm`：执行目录的重命名、排序、删除和同模块跨页移动/复制；编辑期间持有全部执行目录页的深克隆。
- `ScriptConfigurationForm`、`ScriptConfigurationManagerForm`：脚本配置创建、编辑、排序和删除。
- `ScriptRunnerSettings`、`ScriptRunnerStore`：版本化配置模型、兼容读取和安全保存。
- `ScriptRunnerPathHelper`：脚本与执行目录的路径规范化和类型识别。
- `ScriptProcessRunner`：脚本启动、输出重定向、并发进程跟踪和进程树终止。
- `ScriptRunInstance`：单次运行的配置快照、线程安全状态和 10,000 行输出缓冲。
- `MainForm`：只负责页面装配、导航切换和关闭应用时的运行实例确认。

## 拆分原则

- 稳定的现有行为优先做机械拆分，不因移动文件顺手重写。
- 新功能从一开始建立清晰职责边界。
- 只有出现真实复用时才提升为公共抽象。
- 保持单项目和单命名空间，除非规模或测试需求证明需要拆分程序集。
