using System.Drawing;

namespace TortoiseGitLauncher;

internal sealed record CommandSection(string Title, Color AccentColor, string[] CommandTitles);

internal enum CommandScope
{
    WorkingDirectory,
    RepositoryRoot
}

internal sealed record CommandButton(string Title, string Command, CommandScope Scope);
