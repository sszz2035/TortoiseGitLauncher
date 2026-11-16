using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            ApplicationConfiguration.Initialize();
            using var mutex = new Mutex(initiallyOwned: true, SingleInstanceBroker.MutexName, out var isPrimary);
            if (!isPrimary)
            {
                if (!SingleInstanceBroker.TryForward(args))
                {
                    MessageBox.Show("无法将请求转交给已运行的软件，请稍后重试。", "无法处理请求",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            using var requestBroker = new SingleInstanceBroker();
            var isExplorerLaunch = ExplorerAddRequest.TryParse(args, out _);
            using var form = new MainForm(suppressInitialDirectoryFallback: isExplorerLaunch);
            requestBroker.RequestReceived += forwardedArgs =>
            {
                if (form.IsDisposed || !form.IsHandleCreated) return;
                try
                {
                    form.BeginInvoke((Action)(() =>
                    {
                        if (!form.IsDisposed && !form.Disposing) form.HandleExplorerRequest(forwardedArgs);
                    }));
                }
                catch (InvalidOperationException) when (form.IsDisposed || form.Disposing || !form.IsHandleCreated)
                {
                }
            };
            form.Shown += (_, _) =>
            {
                // Start accepting requests only once the window handle and message loop are ready.
                requestBroker.Start();
                form.HandleExplorerRequest(args);
            };
            try { Application.Run(form); }
            finally { mutex.ReleaseMutex(); }
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
