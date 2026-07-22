# 架构总览

## 当前状态

应用保持为一个 .NET 9 WinForms 项目和一个可执行程序，但源代码已经按职责拆分，不再集中在 `Program.cs`。

主要边界如下：

- `Program.cs`：只负责应用初始化、启动主窗体和记录未处理的启动异常。
- `UI/`：窗体、页面布局、自绘控件和图标。
- `Models/`：不负责 IO 或界面的数据模型与命令定义。
- `Services/`：配置持久化、路径解析和外部程序定位。
- `Features/`：后续较大的独立功能按功能纵向组织，例如“脚本执行”。

## 当前 UI 组织

`MainForm` 使用 partial class 按页面职责组织：

- `UI/MainForm.cs`：应用外壳、初始化、侧边栏和页面装配。
- `UI/MainForm.RepositoryManagement.cs`：现有仓库管理页的布局和交互。

partial class 只用于整理现有窗体代码，不代表新的功能都应继续加入 `MainForm`。新页面应优先实现为独立控件或 feature 内部页面，再由主窗体负责导航和生命周期装配。

## 依赖方向

建议依赖方向为：

```text
Program -> UI / Features
UI / Features -> Models / Services
Services -> Models
Models -> .NET 基础类型
```

约束：

- 模型不得依赖 WinForms 控件。
- 服务不得直接操作页面控件或弹出消息框；错误通过返回值、结果对象或异常交给 UI 呈现。
- 页面不直接实现复杂进程生命周期和配置序列化。
- 公共 UI 控件放在 `UI/Controls`，仅由单一功能使用的控件留在对应 feature 中。

## 后续目标

“脚本执行”包含持久化、进程并发、输出缓冲和页面状态，应该建立独立 feature 边界。功能稳定后，再按实际复用情况决定是否把其中的通用进程或配置能力提升到 `Services/`，避免提前抽象。
