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

当前应用是一个 .NET 9 WinForms 单项目工具。`Program.cs` 只保留入口，应用外壳、仓库管理、模型、服务和独立功能已经按 `UI/`、`Models/`、`Services/`、`Features/` 拆分。

已经存在的核心能力包括：

- 仓库根目录选择、最近仓库列表管理和 TortoiseGit 常用命令。
- 通过左侧导航切换常驻的“仓库管理”和“脚本执行”页面。
- 管理全局脚本配置与最近执行目录。
- 在指定目录并发运行 `.py`、`.bat`、`.cmd` 和 `.ps1` 脚本。
- 按实例查看实时输出、终止进程树、移除已结束实例和清理会话记录。
- 关闭应用时对仍在运行的脚本进行确认和清理。

新增独立功能优先放入 `Features/`，主窗体只承担应用外壳、导航和必要的窗口生命周期协调。当前目录与职责以 `01-architecture/module-map.md` 为准。

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

仓库历史由 `RepositoryHistoryStore` 管理，脚本执行配置由 `ScriptRunnerStore` 管理；两者使用相互独立的本机 JSON 文件。

如果要改数据结构：

- 保留旧格式读取能力并维护明确的版本字段。
- 为新增字段提供默认值。
- 避免因为一个坏条目导致整个列表无法读取。
- 仓库历史变化更新 `04-data/repository-history.md`。
- 脚本执行配置变化更新 `04-data/script-runner-settings.md`。

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

当项目继续变大时，建议优先关注这些事项：

1. 仓库管理页继续增长时，将 `MainForm.RepositoryManagement.cs` 提取为独立 `UserControl`。
2. 为 `ScriptRunnerStore`、路径规范化和运行实例状态转换补充针对性测试。
3. 验证高输出量、多实例并发、窗口关闭和进程树终止的边界行为。
4. 只有出现真实复用时，才把 feature 内的进程或配置能力提升为公共服务。
5. 持续维护高 DPI、窗口缩放和滚动区域的 UI 验证清单。

## 给后续 Codex 的一句话

先理解需求和边界，再动代码。小步修改，及时补文档，保持 UTF-8 无 BOM，遇到兼容性疑问就问用户。
