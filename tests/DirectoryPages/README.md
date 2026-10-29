# 目录页数据回归检查

独立的 .NET 9 控制台测试，无第三方测试包；直接链接生产模型及存储源码，不加载 WinForms、不读写本机用户配置。

仅在用户授权编译或运行测试后执行：

```powershell
dotnet run --project tests/DirectoryPages/DirectoryPages.csproj
```

覆盖旧分组配置迁移、旧数组、32 项不截断、页内/跨页数据隔离、全局脚本、选择恢复、迁移与上次保存备份、未来版本保护、坏页隔离和 UTF-8 无 BOM。

样例配置全部写入系统临时目录 `TortoiseGitLauncher-tests-{GUID}`，结束时输出位置并保留供排查。测试不修改真实目录文件，不触发 Git 操作。

当前提交前状态：只做语法及引用路径静态检查，尚未编译或运行这些检查项。
