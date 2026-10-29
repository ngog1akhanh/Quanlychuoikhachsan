using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Một dòng chỉ số: nhãn, giá trị, thanh tiến độ tùy chọn (0..1).</summary>
public sealed record MetricItem(string Label, string Value, double? Fraction = null);

/// <summary>Danh sách chỉ số có thanh tiến độ (giống khối "Overall Rating").</summary>
public class MetricList : ChartBase
{
    private List<MetricItem> items = [];

    public void SetData(IEnumerable<MetricItem> data)
    {
        items = data.ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        if (items.Count == 0) { PaintEmpty(g); return; }

        const int rowH = 40;
        var y = 0;
        foreach (var item in items)
        {
            TextRenderer.DrawText(g, item.Label, AppTheme.Body, new Rectangle(0, y, Width - 110, 22), AppTheme.TextSoft,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, item.Value, AppTheme.BodyBold, new Rectangle(Width - 140, y, 140, 22), AppTheme.Text,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            if (item.Fraction is { } f)
            {
                var track = new RectangleF(0, y + 26, Width - 1, 6);
                using (var tp = AppTheme.RoundedRect(track, 3))
                using (var tb = new SolidBrush(AppTheme.MintSoft))
                    g.FillPath(tb, tp);
                var fw = (float)Math.Clamp(f, 0, 1) * track.Width;
                if (fw > 2)
                {
                    using var fp = AppTheme.RoundedRect(new RectangleF(0, y + 26, fw, 6), 3);
                    using var fb = new SolidBrush(AppTheme.MintDark);
                    g.FillPath(fb, fp);
                }
            }
            y += rowH;
            if (y > Height) break;
        }
    }
}

/// <summary>Một thẻ trong danh sách công việc/hoạt động.</summary>
public sealed record TimelineItem(string Caption, string Title, Color Fill);

/// <summary>Danh sách thẻ xếp dọc (giống khối "Tasks" của mẫu); tự hiện "+n nữa" khi tràn.</summary>
public class TimelineList : ChartBase
{
    private List<TimelineItem> items = [];

    public void SetData(IEnumerable<TimelineItem> data)
    {
        items = data.ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        if (items.Count == 0) { PaintEmpty(g); return; }

        const int itemH = 58;
        const int gap = 8;
        var capacity = Math.Max(1, (Height + gap) / (itemH + gap));
        var showMore = items.Count > capacity;
        var visible = showMore ? capacity - 1 : items.Count;
        var y = 0;
        for (var i = 0; i < visible; i++)
        {
            var item = items[i];
            var rect = new RectangleF(0, y, Width - 1, itemH);
            using (var path = AppTheme.RoundedRect(rect, 10))
            using (var brush = new SolidBrush(item.Fill))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, item.Caption, AppTheme.Small, new Rectangle(12, y + 8, Width - 24, 18), AppTheme.TextSoft,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, item.Title, AppTheme.BodyBold, new Rectangle(12, y + 27, Width - 24, 22), AppTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            y += itemH + gap;
        }
        if (showMore)
        {
            TextRenderer.DrawText(g, $"+{items.Count - visible} mục khác", AppTheme.SmallBold,
                new Rectangle(0, y, Width, itemH), AppTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
