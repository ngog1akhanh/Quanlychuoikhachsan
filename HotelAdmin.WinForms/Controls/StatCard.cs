using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Thẻ KPI: tiêu đề, icon, giá trị lớn và dòng ghi chú (giống 4 thẻ đầu dashboard mẫu).</summary>
public class StatCard : RoundedPanel
{
    private string title = "";
    private string value = "—";
    private string note = "";
    private string? pill;
    private bool pillPositive = true;

    public StatCard(string title, string glyph, bool highlight = false)
    {
        this.title = title;
        Glyph = glyph;
        Highlight = highlight;
        Height = 128;
        if (highlight)
        {
            FillColor = AppTheme.Mint;
            BorderColor = Color.Transparent;
        }
    }

    public string Glyph { get; set; }
    public bool Highlight { get; }

    public string Title { get => title; set { title = value; Invalidate(); } }
    public string Value { get => value; set { this.value = value; Invalidate(); } }
    public string Note { get => note; set { note = value; Invalidate(); } }

    /// <summary>Đặt nhãn pill nhỏ (ví dụ "75%"), positive=false sẽ tô đỏ.</summary>
    public void SetPill(string? text, bool positive = true)
    {
        pill = text;
        pillPositive = positive;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        AppTheme.HighQuality(g);
        var left = Padding.Left;
        var top = Padding.Top;
        var right = Width - Padding.Right;

        // Tiêu đề
        TextRenderer.DrawText(g, title, AppTheme.Body, new Rectangle(left, top, right - left - 44, 22), AppTheme.TextSoft,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        // Icon bên phải
        var iconRect = new RectangleF(right - 34, top - 4, 34, 34);
        using (var path = AppTheme.RoundedRect(iconRect, 9))
        {
            using var brush = new SolidBrush(Highlight ? AppTheme.Surface : AppTheme.Mint);
            g.FillPath(brush, path);
        }
        TextRenderer.DrawText(g, Glyph, AppTheme.Icon(11f), Rectangle.Round(iconRect), AppTheme.Forest,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        // Giá trị
        TextRenderer.DrawText(g, value, AppTheme.Kpi, new Rectangle(left - 2, top + 26, right - left, 40), AppTheme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        // Pill + ghi chú
        var y = Height - Padding.Bottom - 22;
        var x = left;
        if (!string.IsNullOrEmpty(pill))
        {
            var w = TextRenderer.MeasureText(pill, AppTheme.SmallBold).Width + 14;
            var rect = new RectangleF(x, y, w, 22);
            using var path = AppTheme.RoundedRect(rect, 11);
            using var brush = new SolidBrush(pillPositive ? (Highlight ? AppTheme.Surface : AppTheme.Lime) : AppTheme.DangerBg);
            g.FillPath(brush, path);
            TextRenderer.DrawText(g, pill, AppTheme.SmallBold, Rectangle.Round(rect), pillPositive ? AppTheme.Text : AppTheme.DangerFg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            x += w + 8;
        }
        TextRenderer.DrawText(g, note, AppTheme.Small, new Rectangle(x, y, right - x, 22), AppTheme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}
