using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Logo đơn giản: 3 khối nhà cao tầng (xanh chanh / mint) + tên khách sạn.</summary>
public class LogoMark : Control
{
    public LogoMark()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = AppTheme.Logo;
        ForeColor = AppTheme.Text;
        Size = new Size(200, 40);
    }

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        var cy = Height / 2f;
        Bar(g, 0, cy - 2, 7, 14, AppTheme.MintDark);
        Bar(g, 9, cy - 12, 7, 24, AppTheme.LimeDark);
        Bar(g, 18, cy - 7, 7, 19, AppTheme.Mint);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(34, 0, Width - 34, Height), ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private static void Bar(Graphics g, float x, float y, float w, float h, Color color)
    {
        using var path = AppTheme.RoundedRect(new RectangleF(x, y, w, h), 2);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }
}
