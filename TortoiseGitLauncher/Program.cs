using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            try
            {
                var crashLogPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
                File.WriteAllText(crashLogPath, ex.ToString(), new System.Text.UTF8Encoding(false));
            }
            catch
            {
            }

            MessageBox.Show(
                ex.ToString(),
                "TortoiseGitLauncher 启动失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
