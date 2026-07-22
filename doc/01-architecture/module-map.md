# 模块职责图

## 当前目录

```text
TortoiseGitLauncher/
  Program.cs
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
| `UI/MainForm.cs` | 应用外壳、导航和页面装配 |
| `UI/MainForm.RepositoryManagement.cs` | 仓库管理页及 TortoiseGit 命令交互 |
| `UI/RepositoryListManagerForm.cs` | 仓库列表编辑对话框 |
| `UI/Controls` | 可复用自绘控件 |
| `UI/Icons` | 内置矢量图标枚举和绘制 |
| `Models` | 仓库与命令定义 |
| `Services` | 仓库历史、Git 路径和 TortoiseGit 定位 |

## 脚本执行功能建议落点

脚本执行属于包含 UI、持久化和进程生命周期的独立功能，建议按功能纵向组织：

```text
TortoiseGitLauncher/
  Features/
    ScriptRunner/
      ScriptRunnerPage.cs
      ScriptConfigurationForm.cs
      ScriptConfiguration.cs
      ScriptRunnerSettings.cs
      ScriptRunnerStore.cs
      ScriptProcessRunner.cs
      ScriptRunInstance.cs
```

文件名可以随实现细化，但必须保持以下边界：

- `ScriptRunnerPage` 只负责页面状态和用户交互。
- `ScriptRunnerStore` 只负责 JSON 读取、兼容和保存。
- `ScriptProcessRunner` 负责启动、输出重定向、停止进程树和退出状态。
- `ScriptRunInstance` 保存单次运行状态，不与持久化脚本配置混用。
- 主窗体只负责创建页面和切换导航，不接管脚本运行细节。

## 拆分原则

- 稳定的现有行为优先做机械拆分，不因移动文件顺手重写。
- 新功能从一开始建立清晰职责边界。
- 只有出现真实复用时才提升为公共抽象。
- 保持单项目和单命名空间，除非规模或测试需求证明需要拆分程序集。
