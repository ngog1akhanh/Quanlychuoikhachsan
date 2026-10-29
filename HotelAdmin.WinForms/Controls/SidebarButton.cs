using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Mục menu bên trái: icon + chữ, nền xanh chanh khi đang chọn, có badge số.</summary>
public class SidebarButton : Control
{
    private bool hover;
    private bool selected;
    private int badge;

    public SidebarButton(string glyph, string text)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Glyph = glyph;
        Text = text;
        Font = AppTheme.Body;
        Height = 42;
        Cursor = Cursors.Hand;
        BackColor = AppTheme.Surface;
    }

    public string Glyph { get; set; }

    /// <summary>Khóa định danh trang (dùng cho điều hướng).</summary>
    public string PageKey { get; init; } = "";

    public bool Selected
    {
        get => selected;
        set { selected = value; Invalidate(); }
    }

    public int Badge
    {
        get => badge;
        set { badge = value; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        AppTheme.HighQuality(g);
        g.Clear(BackColor);

        var rect = new RectangleF(0.5f, 2.5f, Width - 1.5f, Height - 5f);
        if (selected || hover)
        {
            using var path = AppTheme.RoundedRect(rect, 10);
            using var brush = new SolidBrush(selected ? AppTheme.Lime : AppTheme.Hover);
            g.FillPath(brush, path);
        }

        var fore = selected ? AppTheme.Text : AppTheme.TextSoft;
        TextRenderer.DrawText(g, Glyph, AppTheme.Icon(12f), new Rectangle(12, 0, 26, Height), fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, Text, selected ? AppTheme.BodyBold : AppTheme.Body,
            new Rectangle(46, 0, Width - 46 - 34, Height), fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if (badge > 0)
        {
            var text = badge > 99 ? "99+" : badge.ToString();
            var size = TextRenderer.MeasureText(text, AppTheme.SmallBold);
            var w = Math.Max(20, size.Width + 6);
            var badgeRect = new RectangleF(Width - w - 10, (Height - 20) / 2f, w, 20);
            using var path = AppTheme.RoundedRect(badgeRect, 10);
            using var brush = new SolidBrush(AppTheme.Badge);
            g.FillPath(brush, path);
            TextRenderer.DrawText(g, text, AppTheme.SmallBold, Rectangle.Round(badgeRect), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }
    }
}
