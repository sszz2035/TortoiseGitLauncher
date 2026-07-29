using System.Drawing;
using System.Drawing.Drawing2D;

namespace TortoiseGitLauncher;

internal enum UiIconKind
{
    Brand,
    SidebarRepo,
    ScriptRunner,
    RepoFolder,
    ChooseFolder,
    ManageList,
    Info,
    Stop,
    Commit,
    Pull,
    Push,
    Sync,
    Switch,
    Cleanup,
    RepoStatus,
    Revert,
    Resolve,
    Fetch,
    Stash,
    StashPop
}

internal static class IconFactory
{
    public static Bitmap Create(UiIconKind kind, Color color, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var pen = new Pen(color, Math.Max(1.6f, size / 10F))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var brush = new SolidBrush(color);

        switch (kind)
        {
            case UiIconKind.Brand:
                DrawBrand(graphics, pen, brush, size);
                break;
            case UiIconKind.ScriptRunner:
                DrawTerminal(graphics, pen, size);
                break;
            case UiIconKind.SidebarRepo:
            case UiIconKind.RepoFolder:
                DrawFolder(graphics, pen, brush, size);
                break;
            case UiIconKind.ChooseFolder:
                DrawFolder(graphics, pen, brush, size);
                DrawPlus(graphics, pen, size, size * 0.72F, size * 0.72F, size * 0.14F);
                break;
            case UiIconKind.ManageList:
                DrawList(graphics, pen, brush, size);
                break;
            case UiIconKind.Info:
                DrawInfo(graphics, pen, brush, size);
                break;
            case UiIconKind.Stop:
                DrawStop(graphics, brush, size);
                break;
            case UiIconKind.Commit:
                DrawCommit(graphics, pen, brush, size);
                break;
            case UiIconKind.Pull:
            case UiIconKind.Fetch:
                DrawArrowTray(graphics, pen, brush, size, upward: false);
                break;
            case UiIconKind.Push:
            case UiIconKind.StashPop:
                DrawArrowTray(graphics, pen, brush, size, upward: true);
                break;
            case UiIconKind.Sync:
                DrawSync(graphics, pen, brush, size);
                break;
            case UiIconKind.Switch:
                DrawSwitch(graphics, pen, brush, size);
                break;
            case UiIconKind.Cleanup:
                DrawBroom(graphics, pen, brush, size);
                break;
            case UiIconKind.RepoStatus:
                DrawStatus(graphics, pen, brush, size);
                break;
            case UiIconKind.Revert:
                DrawRevert(graphics, pen, brush, size);
                break;
            case UiIconKind.Resolve:
                DrawResolve(graphics, pen, brush, size);
                break;
            case UiIconKind.Stash:
                DrawStash(graphics, pen, brush, size);
                break;
        }

        return bitmap;
    }
    private static void DrawTerminal(Graphics g, Pen pen, int size)
    {
        g.DrawRectangle(pen, size * 0.14F, size * 0.20F, size * 0.72F, size * 0.60F);
        g.DrawLine(pen, size * 0.28F, size * 0.38F, size * 0.40F, size * 0.50F);
        g.DrawLine(pen, size * 0.40F, size * 0.50F, size * 0.28F, size * 0.62F);
        g.DrawLine(pen, size * 0.50F, size * 0.64F, size * 0.70F, size * 0.64F);
    }

    private static void DrawBrand(Graphics g, Pen pen, Brush brush, int size)
    {
        var p1 = new PointF(size * 0.28F, size * 0.32F);
        var p2 = new PointF(size * 0.66F, size * 0.32F);
        var p3 = new PointF(size * 0.48F, size * 0.68F);
        g.DrawLine(pen, p1, p2);
        g.DrawLine(pen, p1, p3);
        g.DrawLine(pen, p2, p3);
        var node = size * 0.16F;
        g.FillEllipse(brush, p1.X - node / 2, p1.Y - node / 2, node, node);
        g.FillEllipse(brush, p2.X - node / 2, p2.Y - node / 2, node, node);
        g.FillEllipse(brush, p3.X - node / 2, p3.Y - node / 2, node, node);
    }

    private static void DrawFolder(Graphics g, Pen pen, Brush brush, int size)
    {
        var top = new RectangleF(size * 0.16F, size * 0.26F, size * 0.30F, size * 0.16F);
        var body = new RectangleF(size * 0.12F, size * 0.36F, size * 0.76F, size * 0.42F);
        g.DrawRectangle(pen, top.X, top.Y, top.Width, top.Height);
        g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
    }

    private static void DrawList(Graphics g, Pen pen, Brush brush, int size)
    {
        for (var index = 0; index < 3; index++)
        {
            var y = size * (0.28F + index * 0.22F);
            g.FillEllipse(brush, size * 0.14F, y, size * 0.10F, size * 0.10F);
            g.DrawLine(pen, size * 0.32F, y + size * 0.05F, size * 0.82F, y + size * 0.05F);
        }
    }

    private static void DrawInfo(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawEllipse(pen, size * 0.18F, size * 0.18F, size * 0.64F, size * 0.64F);
        g.FillEllipse(brush, size * 0.46F, size * 0.30F, size * 0.08F, size * 0.08F);
        g.DrawLine(pen, size * 0.50F, size * 0.42F, size * 0.50F, size * 0.66F);
    }

    private static void DrawStop(Graphics g, Brush brush, int size)
    {
        g.FillRectangle(
            brush,
            size * 0.28F,
            size * 0.28F,
            size * 0.44F,
            size * 0.44F);
    }

    private static void DrawCommit(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawRectangle(pen, size * 0.20F, size * 0.18F, size * 0.46F, size * 0.60F);
        g.DrawLine(pen, size * 0.54F, size * 0.18F, size * 0.74F, size * 0.38F);
        g.DrawLine(pen, size * 0.66F, size * 0.30F, size * 0.74F, size * 0.38F);
        g.DrawLine(pen, size * 0.74F, size * 0.38F, size * 0.66F, size * 0.46F);
        g.DrawLine(pen, size * 0.30F, size * 0.56F, size * 0.42F, size * 0.66F);
        g.DrawLine(pen, size * 0.42F, size * 0.66F, size * 0.68F, size * 0.36F);
    }

    private static void DrawArrowTray(Graphics g, Pen pen, Brush brush, int size, bool upward)
    {
        var trayY = size * 0.70F;
        g.DrawLine(pen, size * 0.22F, trayY, size * 0.78F, trayY);
        g.DrawLine(pen, size * 0.22F, trayY, size * 0.22F, size * 0.54F);
        g.DrawLine(pen, size * 0.78F, trayY, size * 0.78F, size * 0.54F);

        if (upward)
        {
            g.DrawLine(pen, size * 0.50F, size * 0.72F, size * 0.50F, size * 0.24F);
            g.DrawLine(pen, size * 0.50F, size * 0.24F, size * 0.36F, size * 0.38F);
            g.DrawLine(pen, size * 0.50F, size * 0.24F, size * 0.64F, size * 0.38F);
        }
        else
        {
            g.DrawLine(pen, size * 0.50F, size * 0.20F, size * 0.50F, size * 0.64F);
            g.DrawLine(pen, size * 0.50F, size * 0.64F, size * 0.36F, size * 0.50F);
            g.DrawLine(pen, size * 0.50F, size * 0.64F, size * 0.64F, size * 0.50F);
        }
    }

    private static void DrawSync(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawArc(pen, size * 0.18F, size * 0.22F, size * 0.42F, size * 0.42F, 30, 240);
        g.DrawLine(pen, size * 0.52F, size * 0.20F, size * 0.66F, size * 0.24F);
        g.DrawLine(pen, size * 0.52F, size * 0.20F, size * 0.58F, size * 0.34F);
        g.DrawArc(pen, size * 0.40F, size * 0.36F, size * 0.42F, size * 0.42F, 210, 240);
        g.DrawLine(pen, size * 0.48F, size * 0.80F, size * 0.34F, size * 0.76F);
        g.DrawLine(pen, size * 0.48F, size * 0.80F, size * 0.42F, size * 0.66F);
    }

    private static void DrawSwitch(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawLine(pen, size * 0.30F, size * 0.22F, size * 0.30F, size * 0.72F);
        g.DrawLine(pen, size * 0.30F, size * 0.34F, size * 0.60F, size * 0.34F);
        g.DrawLine(pen, size * 0.30F, size * 0.60F, size * 0.60F, size * 0.72F);
        var node = size * 0.12F;
        g.FillEllipse(brush, size * 0.24F, size * 0.16F, node, node);
        g.FillEllipse(brush, size * 0.54F, size * 0.28F, node, node);
        g.FillEllipse(brush, size * 0.54F, size * 0.66F, node, node);
    }

    private static void DrawBroom(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawLine(pen, size * 0.66F, size * 0.18F, size * 0.38F, size * 0.52F);
        g.DrawLine(pen, size * 0.34F, size * 0.56F, size * 0.56F, size * 0.78F);
        g.DrawLine(pen, size * 0.28F, size * 0.62F, size * 0.50F, size * 0.84F);
        g.DrawLine(pen, size * 0.40F, size * 0.50F, size * 0.62F, size * 0.72F);
    }

    private static void DrawStatus(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawLine(pen, size * 0.22F, size * 0.28F, size * 0.78F, size * 0.28F);
        g.DrawLine(pen, size * 0.22F, size * 0.48F, size * 0.66F, size * 0.48F);
        g.DrawLine(pen, size * 0.22F, size * 0.68F, size * 0.58F, size * 0.68F);
    }

    private static void DrawRevert(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawArc(pen, size * 0.22F, size * 0.24F, size * 0.52F, size * 0.46F, 165, 210);
        g.DrawLine(pen, size * 0.28F, size * 0.30F, size * 0.18F, size * 0.44F);
        g.DrawLine(pen, size * 0.28F, size * 0.30F, size * 0.40F, size * 0.30F);
    }

    private static void DrawResolve(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawLine(pen, size * 0.24F, size * 0.24F, size * 0.76F, size * 0.76F);
        g.DrawLine(pen, size * 0.76F, size * 0.24F, size * 0.24F, size * 0.76F);
        g.FillEllipse(brush, size * 0.18F, size * 0.18F, size * 0.14F, size * 0.14F);
        g.FillEllipse(brush, size * 0.68F, size * 0.68F, size * 0.14F, size * 0.14F);
    }

    private static void DrawStash(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawRectangle(pen, size * 0.20F, size * 0.34F, size * 0.60F, size * 0.36F);
        g.DrawLine(pen, size * 0.28F, size * 0.34F, size * 0.34F, size * 0.20F);
        g.DrawLine(pen, size * 0.72F, size * 0.34F, size * 0.66F, size * 0.20F);
        g.DrawLine(pen, size * 0.34F, size * 0.20F, size * 0.66F, size * 0.20F);
    }

    private static void DrawPlus(Graphics g, Pen pen, float size, float centerX, float centerY, float halfLength)
    {
        g.DrawLine(pen, centerX - halfLength, centerY, centerX + halfLength, centerY);
        g.DrawLine(pen, centerX, centerY - halfLength, centerX, centerY + halfLength);
    }
}
