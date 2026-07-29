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
      ScriptRunnerPathHelper.cs
      ScriptRunnerSettings.cs
      ScriptRunnerStore.cs
  UI/
    MainForm.cs
    MainForm.RepositoryManagement.cs
    RepositoryListManagerForm.cs
    Controls/
      CardPanel.cs
    Icons/
      IconFactory.cs
  Models/
    CommandDefinitions.cs
    RepositoryEntry.cs
  Services/
    RepositoryHistoryStore.cs
    TortoiseGitLocator.cs
    GitPathHelper.cs
```

## 当前职责

| 模块 | 职责 |
| --- | --- |
| `Program.cs` | 进程入口、WinForms 初始化、顶层异常记录 |
| `Features/ScriptRunner` | 脚本执行页面、目录与脚本配置、持久化、运行实例和进程生命周期 |
| `UI/MainForm.cs` | 应用外壳、导航和页面装配 |
| `UI/MainForm.RepositoryManagement.cs` | 仓库管理页及 TortoiseGit 命令交互 |
| `UI/RepositoryListManagerForm.cs` | 仓库列表编辑对话框 |
| `UI/Controls` | 可复用自绘控件 |
| `UI/Icons` | 内置矢量图标枚举和绘制 |
| `Models` | 仓库与命令定义 |
| `Services` | 仓库历史、Git 路径和 TortoiseGit 定位 |

## 脚本执行功能实际边界

脚本执行按功能纵向组织在 `Features/ScriptRunner/`：

- `ScriptRunnerPage`：页面状态、目录与脚本入口、实例标签页和用户操作。
- `ExecutionDirectoryManagerForm`：最近执行目录的重命名、排序和删除。
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
