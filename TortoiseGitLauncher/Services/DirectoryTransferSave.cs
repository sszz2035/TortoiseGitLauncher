namespace TortoiseGitLauncher;

// A failed source save leaves the source intact on disk. Retry the same frozen drafts,
// without importing groups again or replacing the already committed target.
internal sealed class DirectoryTransferSave
{
    public bool TargetSaved { get; private set; }
    public bool Completed { get; private set; }

    public string? Save(Func<string?> saveTarget, Func<string?> saveSource)
    {
        if (Completed) return null;
        if (!TargetSaved)
        {
            if (saveTarget() is { } targetError)
                return $"脚本执行目录保存失败，仓库配置未保存，源目录不会被删除。\n{targetError}";
            TargetSaved = true;
        }
        if (saveSource() is { } sourceError)
            return $"脚本执行目录已保存，但仓库配置保存失败，磁盘中的源仓库配置仍保留。\n" +
                   $"当前编辑已锁定，请点击“保存修改”重试；重试不会重复添加目标目录或分组。\n{sourceError}";
        Completed = true;
        return null;
    }
}
