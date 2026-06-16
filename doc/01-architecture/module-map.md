# 模块职责图

## 建议演进方向

```text
TortoiseGitLauncher/
  UI/
    MainForm.cs
    RepositoryListManagerForm.cs
    Controls/
  Models/
    CommandButton.cs
    CommandSection.cs
    RepositoryEntry.cs
  Services/
    RepositoryHistoryStore.cs
    TortoiseGitLocator.cs
    GitPathHelper.cs
    TortoiseGitCommandExecutor.cs
```

## 拆分原则

先在功能稳定后拆分，避免为了拆分而拆分。新增功能优先放到清晰职责边界内。
