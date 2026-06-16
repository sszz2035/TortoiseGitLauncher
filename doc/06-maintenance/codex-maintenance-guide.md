# Codex 维护说明

这份文档给后续接手本项目的 Codex 或维护者阅读。目标是减少重复摸索，保护已有行为，并让项目可以平稳扩展。

## 接手时先看什么

建议按下面顺序阅读：

1. `README.md`：文档库总入口。
2. `00-requirements/README.md`：确认需求文档放在哪里。
3. `01-architecture/overview.md`：了解当前架构状态。
4. `01-architecture/module-map.md`：了解未来模块拆分方向。
5. `02-extension/feature-extension-guide.md`：新增功能前先看扩展流程。
6. `03-ui/layout-guidelines.md`：改 WinForms 布局前必须看。
7. `06-maintenance/known-issues.md`：确认历史问题，避免重复踩坑。

## 当前项目状态

当前应用是一个 WinForms 小工具，主要代码集中在 `TortoiseGitLauncher/Program.cs`。

已经存在的核心能力包括：

- 仓库根目录选择
- 最近仓库列表管理
- TortoiseGit 常用命令快捷按钮
- 按命令分组展示按钮
- 调用本机 `TortoiseGitProc.exe`

随着项目增长，不能长期把所有新逻辑都堆进 `Program.cs`。新增功能时优先参考 `01-architecture/module-map.md` 中的拆分方向。

## 修改代码前的约束

- 所有修改过的文本文件必须保存为 UTF-8 无 BOM。
- 不要使用会默认写入 BOM 或系统本地编码的写法。
- 如果文件编码不确定，先检查再改。
- 不要在没有明确需求时做大规模重构。
- 不要为了一个小 UI 问题顺手改命令执行、数据存储或发布流程。
- 用户没有明确要求时，不要运行完整编译或发布；可以做轻量文本检查。
- 如果遇到影响实现方向、API 语义、数据含义或兼容性的疑问，先向用户确认。

## 修改文档前的约束

新增或调整功能时，应同步补充文档：

- 新需求：写到 `00-requirements/drafts/` 或 `00-requirements/accepted/`。
- 新命令：更新 `02-extension/command-extension.md`。
- 新页面：更新 `02-extension/page-extension.md`。
- UI 布局经验：更新 `03-ui/layout-guidelines.md`。
- 数据格式变化：更新 `04-data/repository-history.md`。
- 构建发布变化：更新 `05-build-release/build-and-release.md`。
- 重要修复：更新 `06-maintenance/known-issues.md` 或 `06-maintenance/change-log.md`。
- 架构级决策：在 `adr/` 下新增 ADR。

## UI 维护提醒

这个项目目前使用 WinForms，布局问题很容易来自容器高度、停靠方式和自动尺寸计算。

特别注意：

- `FlowLayoutPanel` 开启 `WrapContents` 后，要验证换行后的高度。
- `AutoSize` 和 `Dock` 混用时，要确认父容器是否真的会被撑开。
- 命令按钮目前使用固定尺寸，新增按钮后要检查换行效果。
- 改动后至少检查中文标题、按钮图标、边距、卡片高度和滚动区域。
- 不要只靠增大窗口尺寸掩盖布局问题。

## 命令系统维护提醒

新增 TortoiseGit 命令时，需要同时确认：

- `CommandButton` 的标题和 TortoiseGit 命令名。
- `CommandScope` 使用工作目录还是仓库根目录。
- 是否要新增图标枚举和绘制逻辑。
- 应放入现有分组还是新增分组。
- 对无仓库、路径不存在、TortoiseGit 未安装等情况是否已有合理提示。

## 数据维护提醒

仓库历史由 `RepositoryHistoryStore` 管理，并保存到用户本机目录。

如果要改数据结构：

- 保留旧格式读取能力。
- 明确默认值。
- 避免因为一个坏条目导致整个列表无法读取。
- 在 `04-data/repository-history.md` 记录格式和迁移策略。

## 构建和验证

当前项目提供 `build.ps1`，用于发布到 `dist/`。

用户没有明确要求时，不要主动完整构建。用户要求编译时，可以优先使用项目根目录的 `build.ps1`。

轻量检查建议：

- 搜索修改点附近代码是否落位。
- 检查目标文件是否仍是 UTF-8 无 BOM。
- 对 UI 修改，检查相关布局文档是否需要同步更新。
- 对命令修改，确认命令映射和作用目录没有误改。

## 已知历史问题

曾修复过命令按钮换行后被裁剪的问题。根因是 `FlowLayoutPanel` 换行后高度没有正确撑开，导致第二行按钮显示不完整。相关经验已记录在 `03-ui/layout-guidelines.md`。

## 扩展优先级建议

当项目继续变大时，建议优先做这些基础建设：

1. 把 `Program.cs` 中的模型、服务和窗体逐步拆分到独立文件。
2. 为新增页面建立明确的导航和生命周期约定。
3. 为命令执行建立单独服务，减少 UI 与外部进程调用耦合。
4. 为配置和仓库历史建立版本化数据格式。
5. 为常见 UI 布局问题建立验证清单。

## 给后续 Codex 的一句话

先理解需求和边界，再动代码。小步修改，及时补文档，保持 UTF-8 无 BOM，遇到兼容性疑问就问用户。
