# 架构总览

## 当前状态

当前应用是一个 WinForms 单项目应用，主界面、命令定义、仓库历史、TortoiseGit 调用逻辑集中在 `TortoiseGitLauncher/Program.cs`。

## 后续目标

随着功能增加，逐步把界面、模型、服务和命令执行逻辑拆分到独立文件，避免 `Program.cs` 持续膨胀。
