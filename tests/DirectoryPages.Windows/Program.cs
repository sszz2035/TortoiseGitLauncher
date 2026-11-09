using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using TortoiseGitLauncher;

// Isolated component tests, not desktop automation. No MainForm/ScriptRunnerPage construction:
// their default constructors load the user's real settings. All stores use explicit fixture paths.
internal static class Program
{
    private static int _passed;
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "TortoiseGitLauncher-Windows-tests-" + Guid.NewGuid().ToString("N"));

    [STAThread]
    private static void Main(string[] args)
    {
        if (args is ["--reload-probe", var fixtureRoot])
        {
            var repos = RepositoryHistoryStore.LoadSettingsFrom(Path.Combine(fixtureRoot, "restart-repos.json"), out var repoError);
            var scripts = ScriptRunnerStore.LoadFrom(Path.Combine(fixtureRoot, "restart-scripts.json"), out var scriptError);
            if (repoError is not null || scriptError is not null || repos.SaveBlockedReason is not null || scripts.SaveBlockedReason is not null ||
                JsonSerializer.Serialize(repos) != File.ReadAllText(Path.Combine(fixtureRoot, "restart-repos.expected.json")) ||
                JsonSerializer.Serialize(scripts) != File.ReadAllText(Path.Combine(fixtureRoot, "restart-scripts.expected.json")) ||
                repos.AllowInitialDirectoryFallback || scripts.AllowInitialDirectoryFallback)
                throw new InvalidOperationException("fresh-process reload differs from persisted state");
            Console.WriteLine("Fresh-process reload passed.");
            return;
        }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Directory.CreateDirectory(Root);
        Console.WriteLine("Fixtures: " + Root);
        TestTargetChooser();
        TestPageManager();
        TestListManagers();
        TestFailedWrites();
        TestCrossModuleMoveFailureRecovery();
        TestFreshProcessReload();
        TestRunningIsolation();
        Console.WriteLine($"{_passed} Windows component checks passed. Fixtures: {Root}");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        _passed++;
        Console.WriteLine("PASS " + name);
    }

    private static T Field<T>(object instance, string name) =>
        (T)(instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance) ??
            throw new MissingFieldException(instance.GetType().Name, name)).GetValue(instance)!;

    private static object? Invoke(object instance, string name, params object[] args) =>
        (instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance) ??
            throw new MissingMethodException(instance.GetType().Name, name)).Invoke(instance, args);

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void Prepare(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-20000, -20000);
        form.ShowInTaskbar = false;
        form.Show();
        Application.DoEvents();
    }

    private static void Snapshot(Form form, string name)
    {
        form.PerformLayout();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(Root, name + ".png"));
    }

    private static void CheckButtonsVisible(Form form, string label)
    {
        foreach (var button in Descendants(form).OfType<Button>().Where(button => button.Visible))
        {
            var bounds = new Rectangle(button.PointToScreen(Point.Empty), button.Size);
            for (Control? parent = button.Parent; parent is not null; parent = parent.Parent)
            {
                var client = new Rectangle(parent.PointToScreen(Point.Empty), parent.ClientSize);
                if (!client.Contains(bounds))
                    throw new InvalidOperationException($"{label}: button '{button.Text}' clipped by {parent.GetType().Name}; {bounds} / {client}");
            }
        }
        Check(true, label + " buttons fully contained");
    }

    private static void TestTargetChooser()
    {
        var repo = new RepositoryDirectoryPage { Name = "仓库目标" };
        var execution = new ExecutionDirectoryPage { Name = new string('长', 70) };
        using var chooser = new DirectoryTransferTargetForm("仓库管理", [repo], true, [execution]);
        Prepare(chooser);
        var modules = Field<ComboBox>(chooser, "_modules");
        Check(modules.Items.Count == 2 && !chooser.IsScriptTarget && chooser.SelectedPageId == repo.Id,
            "repository chooser provides both modules and repository target");
        modules.SelectedIndex = 1;
        Check(chooser.IsScriptTarget && chooser.SelectedPageId == execution.Id,
            "module change updates target list and stable page identity");
        chooser.Size = chooser.MinimumSize;
        CheckButtonsVisible(chooser, "minimum chooser");
        Snapshot(chooser, "chooser-minimum");
        chooser.Scale(new SizeF(1.5F, 1.5F));
        CheckButtonsVisible(chooser, "chooser at simulated 150% scale");
        Snapshot(chooser, "chooser-scale150");
        chooser.Scale(new SizeF(4F / 3F, 4F / 3F));
        CheckButtonsVisible(chooser, "chooser at simulated 200% scale");
        Snapshot(chooser, "chooser-scale200");
        using var direct = new DirectoryTransferTargetForm("仓库管理", [], false, [execution]);
        Check(direct.IsScriptTarget && direct.SelectedPageId == execution.Id,
            "single repository page can transfer directly to script module");
        using var scriptOnly = new DirectoryTransferTargetForm("脚本执行", [execution], false);
        Check(Field<ComboBox>(scriptOnly, "_modules").Items.Count == 1 && scriptOnly.SelectedPageId == execution.Id,
            "script chooser exposes no repository reverse transfer");
        using var empty = new DirectoryTransferTargetForm("仓库管理", [], false);
        Check(empty.SelectedPageId is null && empty.AcceptButton is Button { Enabled: false },
            "empty target cannot confirm transfer");
    }

    private static void TestPageManager()
    {
        var original = new RepositoryDirectoryPage { Name = "原始页" };
        var other = new RepositoryDirectoryPage { Name = "第二页", DisplayOrder = 1 };
        var saveCalls = 0;
        List<RepositoryDirectoryPage>? saved = null;
        using var manager = new DirectoryPageManagerForm<RepositoryDirectoryPage>("仓库管理", [original, other], original.Id,
            page => page.Clone(), (pages, id) =>
            {
                saveCalls++;
                if (saveCalls == 1) return "模拟保存失败：" + new string('长', 90);
                saved = pages.Select(page => page.Clone()).ToList();
                return null;
            });
        Prepare(manager);
        Field<TextBox>(manager, "_name").Text = "新的页名";
        Check(original.Name == "原始页" && Field<ListBox>(manager, "_list").SelectedItem?.ToString() == "新的页名",
            "page rename previews only in editing copy");
        Invoke(manager, "MoveSelectedPage", 1);
        var drafts = Field<List<RepositoryDirectoryPage>>(manager, "_pages");
        Check(drafts[1].Id == original.Id && Field<ListBox>(manager, "_list").SelectedIndex == 1,
            "page reorder retains stable identity and selection");
        manager.Size = manager.MinimumSize;
        Snapshot(manager, "pages-minimum");
        CheckButtonsVisible(manager, "minimum page manager");
        Invoke(manager, "SaveAndClose");
        Check(manager.Visible && Field<Label>(manager, "_error").Text.Contains("模拟保存失败") && saveCalls == 1 && original.Name == "原始页",
            "failed page save retains open editor, draft name and original data");
        CheckButtonsVisible(manager, "minimum page manager with long save error");
        Snapshot(manager, "pages-save-error");
        Invoke(manager, "SaveAndClose");
        Check(saved?.Count == 2 && saved[1].Name == "新的页名" && saved[1].DisplayOrder == 1 && saveCalls == 2,
            "retry applies edited page name and order");
        using var last = new DirectoryPageManagerForm<ExecutionDirectoryPage>("脚本执行", [new()], "",
            page => page.Clone(), (_, _) => throw new InvalidOperationException("cancel cannot save"));
        Check(!Field<Button>(last, "_delete").Enabled, "last page delete disabled");
        Field<TextBox>(last, "_name").Text = "取消编辑";
        last.Close();
    }

    private static void TestListManagers()
    {
        var group = new RepositoryGroup { Name = "分组", DisplayOrder = 2 };
        var repo = new RepositoryDirectoryPage
        {
            Groups = [group],
            Entries = [new() { RepoRootPath = Path.Combine(Root, "r1"), DisplayName = "第一项", DisplayOrder = 0 },
                new() { RepoRootPath = Path.Combine(Root, "r2"), DisplayName = "第二项", DisplayOrder = 1 },
                new() { RepoRootPath = Path.Combine(Root, "r3"), DisplayName = "组内", GroupId = group.Id }]
        };
        var target = new ExecutionDirectoryPage();
        var saved = false;
        using var manager = new RepositoryListManagerForm(repo.Entries, repo.Groups, repo.Entries[0].RepoRootPath,
            editor => { saved = editor.GetEntries()[0].DisplayName == "实时名称"; return null; }, [repo], repo.Id, [target]);
        Prepare(manager);
        Field<TextBox>(manager, "_displayNameTextBox").Text = "实时名称";
        Check(manager.GetEntries()[0].DisplayName == "实时名称" && repo.Entries[0].DisplayName == "第一项",
            "repository name editing updates draft immediately, not original");
        var list = Field<ListBox>(manager, "_entriesListBox");
        list.SetSelected(0, true);
        list.SetSelected(1, true);
        Check(list.SelectedItems.Count == 2 && Field<TextBox>(manager, "_displayNameTextBox").ReadOnly &&
              Field<TextBox>(manager, "_displayNameTextBox").BackColor == Color.White,
            "multiple repository selection retains white read-only editor");
        Check(list.AllowDrop, "repository list permits local drag and drop");
        manager.Size = manager.MinimumSize;
        Snapshot(manager, "repositories-minimum");
        CheckButtonsVisible(manager, "minimum repository manager");
        manager.Scale(new SizeF(1.5F, 1.5F));
        CheckButtonsVisible(manager, "repository manager at simulated 150% scale");
        Snapshot(manager, "repositories-scale150");
        Invoke(manager, "SaveAndClose");
        Check(saved, "repository manager save callback receives latest draft name");

        var execution = new ExecutionDirectoryPage
        {
            RecentDirectories = [new() { DirectoryPath = Path.Combine(Root, "e1"), DisplayName = "执行一" },
                new() { DirectoryPath = Path.Combine(Root, "e2"), DisplayName = "执行二", DisplayOrder = 1 }]
        };
        using var executionManager = new ExecutionDirectoryManagerForm(execution.RecentDirectories, execution.DirectoryGroups,
            execution.RecentDirectories[0].DirectoryPath, null, [execution], execution.Id);
        Prepare(executionManager);
        var executionList = Field<ListBox>(executionManager, "_entriesListBox");
        Field<TextBox>(executionManager, "_displayNameTextBox").Text = "新的执行名";
        Check(executionManager.GetEntries()[0].DisplayName == "新的执行名" && execution.RecentDirectories[0].DisplayName == "执行一",
            "execution name preview isolated from original");
        executionList.SetSelected(0, true);
        executionList.SetSelected(1, true);
        Check(executionList.AllowDrop && executionList.SelectedItems.Count == 2, "execution list supports same multi-selection and drag policy");
        executionManager.Size = executionManager.MinimumSize;
        Snapshot(executionManager, "execution-minimum");
        CheckButtonsVisible(executionManager, "minimum execution manager");
        executionManager.Scale(new SizeF(1.5F, 1.5F));
        CheckButtonsVisible(executionManager, "execution manager at simulated 150% scale");
        executionManager.Close();
        Check(execution.RecentDirectories[0].DisplayName == "执行一", "execution cancel retains original names");

        using var selector = new DirectoryPageSelectorButton("仓库根目录");
        var longPage = new RepositoryDirectoryPage { Name = new string('长', 80) };
        selector.SetPages([repo, longPage], longPage.Id);
        var menu = Field<ContextMenuStrip>(selector, "_menu");
        Check(selector.Text.Contains("…") && selector.Text.Length < longPage.Name.Length &&
              menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Checked).ToolTipText == longPage.Name,
            "long page title truncates while menu preserves full name");
        var selectedId = "";
        selector.PageSelected += id => selectedId = id;
        ((ToolStripMenuItem)menu.Items[0]).PerformClick();
        Check(selectedId == repo.Id, "page menu selection emits stable page identity");
        selector.SetPages([longPage], longPage.Id);
        Check(menu.Items.OfType<ToolStripMenuItem>().Count() == 2 && menu.Items[0].Text == longPage.Name,
            "page menu rebuild removes stale pages and retains management entry");
    }

    private static void TestFailedWrites()
    {
        var path = Path.Combine(Root, "locked-repositories.json");
        var original = new RepositoryHistorySettings();
        Check(RepositoryHistoryStore.TrySaveTo(path, original, out _), "locked-write fixture initialized");
        var bytes = File.ReadAllBytes(path);
        var draft = original.Clone();
        draft.Pages[0].Name = "保存后名字";
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Check(!RepositoryHistoryStore.TrySaveTo(path, draft, out var error) && !string.IsNullOrWhiteSpace(error),
                "real locked file reports failure");
            Check(File.ReadAllBytes(path).SequenceEqual(bytes), "failed replacement retains original file bytes");
        }
        Check(RepositoryHistoryStore.TrySaveTo(path, draft, out _) &&
              RepositoryHistoryStore.LoadSettingsFrom(path, out _).Pages[0].Name == "保存后名字",
            "unlock and retry persist same draft");
        Check(!Directory.GetFiles(Root, "*.tmp").Any(), "failed write cleans temporary file");

        var targetPath = Path.Combine(Root, "locked-script-target.json");
        var scripts = new ScriptRunnerSettings();
        Check(ScriptRunnerStore.TrySaveTo(targetPath, scripts, out _), "cross-file lock fixture initialized");
        var scriptDraft = scripts.Clone();
        scriptDraft.Pages[0].Name = "已接收页";
        var nextSource = draft.Clone();
        nextSource.Pages[0].Name = "源已完成";
        var transaction = new DirectoryTransferSave();
        var sourceCalls = 0;
        var targetCalls = 0;
        string? SaveTarget()
        {
            targetCalls++;
            return ScriptRunnerStore.TrySaveTo(targetPath, scriptDraft, out var error) ? null : error;
        }
        string? SaveSource()
        {
            sourceCalls++;
            return RepositoryHistoryStore.TrySaveTo(path, nextSource, out var error) ? null : error;
        }
        using (var locked = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Check(transaction.Save(SaveTarget, SaveSource) is not null && sourceCalls == 0 && !transaction.TargetSaved &&
                  RepositoryHistoryStore.LoadSettingsFrom(path, out _).Pages[0].Name == "保存后名字",
                "real target lock never attempts source save");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Check(transaction.Save(SaveTarget, SaveSource) is not null && transaction.TargetSaved && !transaction.Completed &&
                  ScriptRunnerStore.LoadFrom(targetPath, out _).Pages[0].Name == "已接收页" &&
                  RepositoryHistoryStore.LoadSettingsFrom(path, out _).Pages[0].Name == "保存后名字",
                "real source lock retains source bytes after target commit");
        var callsBeforeRetry = targetCalls;
        Check(transaction.Save(SaveTarget, SaveSource) is null && transaction.Completed && targetCalls == callsBeforeRetry &&
              RepositoryHistoryStore.LoadSettingsFrom(path, out _).Pages[0].Name == "源已完成",
            "real file unlock retry skips committed target and finishes source");
    }

    private static void TestFreshProcessReload()
    {
        var available = Path.Combine(Root, "remembered-repo");
        Directory.CreateDirectory(available);
        var group = new RepositoryGroup { Name = "重载组", IsExpanded = false };
        var first = new RepositoryDirectoryPage
        {
            Name = "第一页", Groups = [group], LastSelectedDirectoryPath = available,
            Entries = [new() { RepoRootPath = available, DisplayName = "页一名称", Notes = "页一备注\r\n换行", GroupId = group.Id }]
        };
        var second = new RepositoryDirectoryPage
        {
            Name = "第二页", DisplayOrder = 1,
            Entries = [new() { RepoRootPath = available, DisplayName = "页二名称", Notes = "独立备注" }]
        };
        var repos = new RepositoryHistorySettings { Pages = [first, second], LastSelectedPageId = second.Id };
        var scripts = new ScriptRunnerSettings
        {
            Pages = [new() { Name = "接收页" }, new() { Name = "保留空页", DisplayOrder = 1 }],
            Scripts = [new() { Name = "全局", ScriptPath = Path.Combine(Root, "restart.py"), Arguments = "--example" }]
        };
        scripts.LastSelectedPageId = scripts.Pages[1].Id;
        RepositoryToScriptTransfer.Transfer(first.Entries, first.Groups, scripts.Pages[0].RecentDirectories,
            scripts.Pages[0].DirectoryGroups, [], first.Groups.ToList(), false);
        var repoPath = Path.Combine(Root, "restart-repos.json");
        var scriptPath = Path.Combine(Root, "restart-scripts.json");
        Check(RepositoryHistoryStore.TrySaveTo(repoPath, repos, out _) && ScriptRunnerStore.TrySaveTo(scriptPath, scripts, out _),
            "restart fixtures preserve page-local duplicates and cross-module target");
        File.WriteAllText(Path.Combine(Root, "restart-repos.expected.json"),
            JsonSerializer.Serialize(RepositoryHistoryStore.LoadSettingsFrom(repoPath, out _)), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(Root, "restart-scripts.expected.json"),
            JsonSerializer.Serialize(ScriptRunnerStore.LoadFrom(scriptPath, out _)), new UTF8Encoding(false));
        var startInfo = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        startInfo.ArgumentList.Add("--reload-probe");
        startInfo.ArgumentList.Add(Root);
        using var probe = Process.Start(startInfo)!;
        if (!probe.WaitForExit(10000))
        {
            probe.Kill(entireProcessTree: true);
            throw new TimeoutException("fresh-process reload timed out");
        }
        var output = probe.StandardOutput.ReadToEnd();
        var error = probe.StandardError.ReadToEnd();
        Check(probe.ExitCode == 0 && output.Contains("Fresh-process reload passed."),
            "fresh process restores both complete configurations, active pages, notes, groups, order and global scripts: " + error);
    }

    private static void TestCrossModuleMoveFailureRecovery()
    {
        var directory = Path.Combine(Root, "cross-file-move-directory");
        var group = new RepositoryGroup { Name = "保留源组" };
        var entry = new RepositoryEntry
        {
            RepoRootPath = directory, DisplayName = "待移动项", Notes = "保留到源提交成功",
            GroupId = group.Id, DisplayOrder = 0
        };
        var source = new RepositoryHistorySettings
        {
            Pages = [new RepositoryDirectoryPage { Groups = [group], Entries = [entry] }]
        };
        var target = new ScriptRunnerSettings { Pages = [new() { Name = "跨模块目标" }] };
        var sourcePath = Path.Combine(Root, "move-source.json");
        var targetPath = Path.Combine(Root, "move-target.json");
        Check(RepositoryHistoryStore.TrySaveTo(sourcePath, source, out _) && ScriptRunnerStore.TrySaveTo(targetPath, target, out _),
            "cross-module move source and target fixtures persisted");
        var sourceDraft = RepositoryHistoryStore.LoadSettingsFrom(sourcePath, out _);
        var targetDraft = ScriptRunnerStore.LoadFrom(targetPath, out _);
        var result = RepositoryToScriptTransfer.Transfer(sourceDraft.Entries, sourceDraft.Groups,
            targetDraft.ActivePage.RecentDirectories, targetDraft.ActivePage.DirectoryGroups,
            [], sourceDraft.Groups.ToList(), move: true);
        Check(result.AddedDirectories == 1 && sourceDraft.Entries.Count == 0 && sourceDraft.Groups.Count == 0,
            "cross-module move draft removes item only in memory");
        var transaction = new DirectoryTransferSave();
        string? SaveTarget() => ScriptRunnerStore.TrySaveTo(targetPath, targetDraft, out var error) ? null : error;
        string? SaveSource() => RepositoryHistoryStore.TrySaveTo(sourcePath, sourceDraft, out var error) ? null : error;
        using (var lockedTarget = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Check(transaction.Save(SaveTarget, SaveSource) is not null &&
                  RepositoryHistoryStore.LoadSettingsFrom(sourcePath, out _).Entries.Count == 1,
                "failed target write leaves moved directory persisted in source");
        using (var lockedSource = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Check(transaction.Save(SaveTarget, SaveSource) is not null && transaction.TargetSaved &&
                  ScriptRunnerStore.LoadFrom(targetPath, out _).ActivePage.RecentDirectories.Count == 1 &&
                  RepositoryHistoryStore.LoadSettingsFrom(sourcePath, out _).Entries.Count == 1,
                "source write failure leaves both persisted copy and recoverable original");
        Check(transaction.Save(SaveTarget, SaveSource) is null && transaction.Completed &&
              RepositoryHistoryStore.LoadSettingsFrom(sourcePath, out _).Entries.Count == 0 &&
              RepositoryHistoryStore.LoadSettingsFrom(sourcePath, out _).Groups.Count == 0 &&
              ScriptRunnerStore.LoadFrom(targetPath, out _).ActivePage.RecentDirectories.Single().Notes == entry.Notes,
            "retry removes source only after target is durable");
    }

    private static void TestRunningIsolation()
    {
        var pathA = Path.Combine(Root, "work-a");
        var pathB = Path.Combine(Root, "work-b");
        Directory.CreateDirectory(pathA);
        Directory.CreateDirectory(pathB);
        var scriptPath = Path.Combine(Root, "isolation.cmd");
        File.WriteAllText(scriptPath,
            "@echo off\r\necho START:%~1\r\necho CWD:%CD%\r\nping -n 3 127.0.0.1 >nul\r\necho END:%~1\r\nexit /b 0\r\n", new UTF8Encoding(false));
        var configuration = new ScriptConfiguration { Name = "并行测试", ScriptPath = scriptPath, Arguments = "first" };
        using var runner = new ScriptProcessRunner();
        var first = runner.Start(configuration, pathA);
        configuration.Arguments = "second";
        var second = runner.Start(configuration, pathB);
        try
        {
            Check(first.IsRunning && second.IsRunning && first.Id != second.Id, "two independent script instances run concurrently");
            var source = new RepositoryDirectoryPage { Entries = [new() { RepoRootPath = pathA }] };
            var settings = new ScriptRunnerSettings
            {
                Pages = [new() { Name = "运行中", RecentDirectories = [new() { DirectoryPath = pathA }] },
                    new() { Name = "接收页" }], Scripts = [configuration.Clone()]
            };
            settings.LastSelectedPageId = settings.Pages[0].Id;
            RepositoryToScriptTransfer.Transfer(source.Entries, source.Groups, settings.Pages[1].RecentDirectories,
                settings.Pages[1].DirectoryGroups, source.Entries.ToList(), [], true);
            settings.LastSelectedPageId = settings.Pages[1].Id;
            settings.Pages.RemoveAt(0);
            Check(ScriptRunnerStore.TrySaveTo(Path.Combine(Root, "running-scripts.json"), settings, out _),
                "transfer, switch and remove launch page configuration while scripts run");
            Check(first.IsRunning && second.IsRunning && first.WorkingDirectory == pathA && second.WorkingDirectory == pathB &&
                  first.ScriptSnapshot.Arguments == "first" && second.ScriptSnapshot.Arguments == "second",
                "directory config edits preserve running instance launch snapshots");
            var timer = Stopwatch.StartNew();
            while ((first.IsRunning || second.IsRunning) && timer.Elapsed < TimeSpan.FromSeconds(15)) Thread.Sleep(30);
            Check(first.State == ScriptRunState.Succeeded && second.State == ScriptRunState.Succeeded &&
                  first.ExitCode == 0 && second.ExitCode == 0, "both scripts automatically complete after directory edits");
            var firstLines = first.GetOutputSnapshot().Lines.Select(line => line.Text).ToList();
            var secondLines = second.GetOutputSnapshot().Lines.Select(line => line.Text).ToList();
            Check(firstLines.Contains("START:first") && firstLines.Contains("END:first") && firstLines.Contains("CWD:" + pathA) &&
                  !firstLines.Contains("END:second") && secondLines.Contains("END:second") && secondLines.Contains("CWD:" + pathB),
                "concurrent output and working directories remain isolated");
            var stopPath = Path.Combine(Root, "stop.cmd");
            File.WriteAllText(stopPath, "@echo off\r\necho STOP-READY\r\nping -n 31 127.0.0.1 >nul\r\n", new UTF8Encoding(false));
            var stoppable = runner.Start(new ScriptConfiguration { Name = "终止测试", ScriptPath = stopPath }, pathA);
            Check(stoppable.CanStop && runner.TryStop(stoppable.Id, out _), "script instance can still be manually stopped");
            timer.Restart();
            while (stoppable.IsRunning && timer.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(30);
            Check(stoppable.State == ScriptRunState.Stopped && !stoppable.CanStop,
                "manual stop reaches final stopped state");
        }
        finally { runner.StopAll(); }
    }
}
