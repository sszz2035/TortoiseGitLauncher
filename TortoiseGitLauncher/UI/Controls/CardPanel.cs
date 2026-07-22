using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class CardPanel : Panel
{
    public Color FillColor = Color.White;

    public Color BorderColor = Color.FromArgb(229, 235, 243);

    public int CornerRadius = 16;

    public CardPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Margin = new Padding(0);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;

        using var path = CreateRoundedPath(rect, CornerRadius);
        using var brush = new SolidBrush(FillColor);
        using var pen = new Pen(BorderColor);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
        base.OnPaint(e);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
