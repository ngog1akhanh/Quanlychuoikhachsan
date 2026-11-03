using System.Drawing.Drawing2D;
using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Lớp nền cho biểu đồ tự vẽ bằng GDI+ (không cần thư viện ngoài).</summary>
public abstract class ChartBase : Control
{
    protected ChartBase()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = AppTheme.Small;
    }

    /// <summary>Định dạng giá trị trên trục và tooltip.</summary>
    public Func<double, string> Format { get; set; } = v => v.ToString("#,0");

    /// <summary>Thông báo hiển thị khi chưa có dữ liệu.</summary>
    public string EmptyText { get; set; } = "Chưa có dữ liệu";

    protected static double NiceMax(double max)
    {
        if (max <= 0) return 1;
        var exp = Math.Pow(10, Math.Floor(Math.Log10(max)));
        foreach (var step in new[] { 1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10 })
            if (step * exp >= max) return step * exp;
        return 10 * exp;
    }

    protected void PaintEmpty(Graphics g)
    {
        TextRenderer.DrawText(g, EmptyText, AppTheme.Body, ClientRectangle, AppTheme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    protected static void DrawTooltip(Graphics g, PointF anchor, string title, string value, Rectangle bounds)
    {
        var titleSize = TextRenderer.MeasureText(title, AppTheme.Small);
        var valueSize = TextRenderer.MeasureText(value, AppTheme.BodyBold);
        var w = Math.Max(titleSize.Width, valueSize.Width) + 18;
        const int h = 46;
        var x = Math.Max(bounds.Left, Math.Min(anchor.X - w / 2f, bounds.Right - w));
        var y = Math.Max(bounds.Top, anchor.Y - h - 12);
        var rect = new RectangleF(x, y, w, h);
        using (var path = AppTheme.RoundedRect(rect, 8))
        using (var brush = new SolidBrush(AppTheme.Lime))
        using (var pen = new Pen(AppTheme.LimeDark))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, title, AppTheme.Small, new Rectangle((int)x + 9, (int)y + 5, w, 16), AppTheme.TextSoft, TextFormatFlags.Left);
        TextRenderer.DrawText(g, value, AppTheme.BodyBold, new Rectangle((int)x + 9, (int)y + 21, w, 20), AppTheme.Text, TextFormatFlags.Left);
    }
}

/// <summary>Biểu đồ đường cong có tô vùng (doanh thu theo tháng), hover hiện tooltip.</summary>
public class AreaChart : ChartBase
{
    private string[] labels = [];
    private double[] values = [];
    private int hoverIndex = -1;

    public Color LineColor { get; set; } = ColorTranslator.FromHtml("#B8D44A");
    public Color FillTop { get; set; } = Color.FromArgb(150, 213, 246, 229);

    public void SetData(string[] labels, double[] values)
    {
        this.labels = labels;
        this.values = values;
        hoverIndex = -1;
        Invalidate();
    }

    private Rectangle Plot => new(58, 14, Math.Max(10, Width - 58 - 14), Math.Max(10, Height - 14 - 30));

    private PointF PointAt(int i, double max)
    {
        var p = Plot;
        var x = values.Length == 1 ? p.Left + p.Width / 2f : p.Left + i * (p.Width / (float)(values.Length - 1));
        var y = p.Bottom - (float)(values[i] / max) * p.Height;
        return new PointF(x, y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (values.Length == 0) return;
        var max = NiceMax(values.Max());
        var nearest = Enumerable.Range(0, values.Length).OrderBy(i => Math.Abs(PointAt(i, max).X - e.X)).First();
        if (nearest != hoverIndex) { hoverIndex = nearest; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        if (values.Length == 0) { PaintEmpty(g); return; }

        var p = Plot;
        var max = NiceMax(values.Max());

        // Lưới ngang + nhãn trục Y
        using (var grid = new Pen(AppTheme.Border) { DashStyle = DashStyle.Dash })
        {
            for (var i = 0; i <= 4; i++)
            {
                var y = p.Bottom - i * p.Height / 4f;
                g.DrawLine(grid, p.Left, y, p.Right, y);
                TextRenderer.DrawText(g, Format(max * i / 4), Font, new Rectangle(0, (int)y - 9, p.Left - 8, 18), AppTheme.TextMuted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        // Nhãn trục X
        for (var i = 0; i < labels.Length && i < values.Length; i++)
        {
            var pt = PointAt(i, max);
            TextRenderer.DrawText(g, labels[i], Font, new Rectangle((int)pt.X - 40, p.Bottom + 8, 80, 18), AppTheme.TextMuted,
                TextFormatFlags.HorizontalCenter);
        }

        var points = Enumerable.Range(0, values.Length).Select(i => PointAt(i, max)).ToArray();
        if (points.Length >= 2)
        {
            // Vùng tô gradient
            using (var area = new GraphicsPath())
            {
                area.AddCurve(points, 0.45f);
                area.AddLine(points[^1], new PointF(points[^1].X, p.Bottom));
                area.AddLine(new PointF(points[^1].X, p.Bottom), new PointF(points[0].X, p.Bottom));
                area.CloseFigure();
                using var fill = new LinearGradientBrush(new RectangleF(p.Left, p.Top - 1, p.Width, p.Height + 2),
                    FillTop, Color.FromArgb(10, FillTop), LinearGradientMode.Vertical);
                g.FillPath(fill, area);
            }
            using var line = new Pen(LineColor, 2.5f);
            g.DrawCurve(line, points, 0.45f);
        }

        // Điểm hover + tooltip
        if (hoverIndex >= 0 && hoverIndex < points.Length)
        {
            var pt = points[hoverIndex];
            using (var guide = new Pen(AppTheme.LimeDark) { DashStyle = DashStyle.Dot })
                g.DrawLine(guide, pt.X, p.Top, pt.X, p.Bottom);
            using (var white = new SolidBrush(Color.White))
                g.FillEllipse(white, pt.X - 6, pt.Y - 6, 12, 12);
            using (var ring = new Pen(LineColor, 3f))
                g.DrawEllipse(ring, pt.X - 6, pt.Y - 6, 12, 12);
            DrawTooltip(g, pt, labels.ElementAtOrDefault(hoverIndex) ?? "", Format(values[hoverIndex]), ClientRectangle);
        }
    }
}

/// <summary>Một chuỗi dữ liệu của biểu đồ cột.</summary>
public sealed record BarSeries(string Name, Color Color, double[] Values);

/// <summary>Biểu đồ cột nhóm, đầu cột bo tròn, có chú thích và tooltip.</summary>
public class BarChart : ChartBase
{
    private string[] labels = [];
    private List<BarSeries> series = [];
    private int hoverIndex = -1;

    /// <summary>Giá trị trục Y tối đa cố định (ví dụ 100 cho %); null = tự tính.</summary>
    public double? FixedMax { get; set; }

    public void SetData(string[] labels, params BarSeries[] series)
    {
        this.labels = labels;
        this.series = series.ToList();
        hoverIndex = -1;
        Invalidate();
    }

    private Rectangle Plot => new(46, 34, Math.Max(10, Width - 46 - 10), Math.Max(10, Height - 34 - 28));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (labels.Length == 0) return;
        var p = Plot;
        var slot = p.Width / (float)labels.Length;
        var i = (int)((e.X - p.Left) / slot);
        i = i < 0 || i >= labels.Length ? -1 : i;
        if (i != hoverIndex) { hoverIndex = i; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        if (labels.Length == 0 || series.Count == 0) { PaintEmpty(g); return; }

        // Chú thích
        var lx = 0;
        foreach (var s in series)
        {
            using var b = new SolidBrush(s.Color);
            g.FillEllipse(b, lx, 6, 9, 9);
            TextRenderer.DrawText(g, s.Name, Font, new Point(lx + 13, 2), AppTheme.TextSoft);
            lx += TextRenderer.MeasureText(s.Name, Font).Width + 28;
        }

        var p = Plot;
        var max = FixedMax ?? NiceMax(series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max());
        using (var grid = new Pen(AppTheme.Border) { DashStyle = DashStyle.Dash })
        {
            for (var i = 0; i <= 4; i++)
            {
                var y = p.Bottom - i * p.Height / 4f;
                g.DrawLine(grid, p.Left, y, p.Right, y);
                TextRenderer.DrawText(g, Format(max * i / 4), Font, new Rectangle(0, (int)y - 9, p.Left - 8, 18), AppTheme.TextMuted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        var slot = p.Width / (float)labels.Length;
        var barW = Math.Min(26f, slot * 0.6f / series.Count);
        var groupW = barW * series.Count + (series.Count - 1) * 4;
        for (var i = 0; i < labels.Length; i++)
        {
            var cx = p.Left + slot * i + slot / 2;
            if (i == hoverIndex)
            {
                using var hl = new SolidBrush(Color.FromArgb(120, AppTheme.Hover));
                using var hp = AppTheme.RoundedRect(new RectangleF(cx - slot / 2 + 4, p.Top, slot - 8, p.Height), 8);
                g.FillPath(hl, hp);
            }
            for (var s = 0; s < series.Count; s++)
            {
                var v = i < series[s].Values.Length ? series[s].Values[i] : 0;
                var h = (float)(Math.Max(0, v) / max * p.Height);
                if (h < 1) continue;
                var x = cx - groupW / 2 + s * (barW + 4);
                using var path = AppTheme.RoundedRect(new RectangleF(x, p.Bottom - h, barW, h), Math.Min(6, barW / 2));
                using var brush = new SolidBrush(series[s].Color);
                g.FillPath(brush, path);
            }
            TextRenderer.DrawText(g, labels[i], Font, new Rectangle((int)(cx - slot / 2), p.Bottom + 6, (int)slot, 18), AppTheme.TextMuted,
                TextFormatFlags.HorizontalCenter);
        }

        if (hoverIndex >= 0)
        {
            var cx = p.Left + slot * hoverIndex + slot / 2;
            var text = string.Join("  ·  ", series.Select(s => $"{s.Name}: {Format(hoverIndex < s.Values.Length ? s.Values[hoverIndex] : 0)}"));
            var top = series.Max(s => hoverIndex < s.Values.Length ? s.Values[hoverIndex] : 0);
            DrawTooltip(g, new PointF(cx, p.Bottom - (float)(top / max * p.Height)), labels[hoverIndex], text, ClientRectangle);
        }
    }
}

/// <summary>Một phần của biểu đồ tròn / thanh phân đoạn.</summary>
public sealed record ChartSegment(string Label, double Value, Color Color);

/// <summary>Biểu đồ donut + chú thích bên phải (giống "Booking by Platform").</summary>
public class DonutChart : ChartBase
{
    private List<ChartSegment> segments = [];

    public string CenterTitle { get; set; } = "";
    public string CenterValue { get; set; } = "";

    public void SetData(IEnumerable<ChartSegment> data)
    {
        segments = data.Where(s => s.Value > 0).ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        var total = segments.Sum(s => s.Value);
        if (total <= 0) { PaintEmpty(g); return; }

        var size = Math.Min(Height - 10, (int)(Width * 0.48));
        var rect = new RectangleF(4, (Height - size) / 2f, size, size);
        var thickness = size * 0.26f;
        var start = -90f;
        foreach (var s in segments)
        {
            var sweep = (float)(s.Value / total * 360);
            using var pen = new Pen(s.Color, thickness);
            var inner = RectangleF.Inflate(rect, -thickness / 2, -thickness / 2);
            g.DrawArc(pen, inner, start, Math.Max(0.5f, sweep - 1.2f));
            start += sweep;
        }

        // Chữ giữa
        var center = Rectangle.Round(rect);
        TextRenderer.DrawText(g, CenterValue, AppTheme.KpiSmall, new Rectangle(center.X, center.Y + center.Height / 2 - 22, center.Width, 26),
            AppTheme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, CenterTitle, AppTheme.Small, new Rectangle(center.X, center.Y + center.Height / 2 + 4, center.Width, 18),
            AppTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // Chú thích
        var lx = (int)rect.Right + 18;
        var lineH = 24;
        var ly = (Height - segments.Count * lineH) / 2;
        foreach (var s in segments)
        {
            using var b = new SolidBrush(s.Color);
            g.FillEllipse(b, lx, ly + 7, 10, 10);
            var pct = (s.Value / total * 100).ToString("0") + "%";
            TextRenderer.DrawText(g, pct, AppTheme.SmallBold, new Rectangle(lx + 16, ly, 40, lineH), AppTheme.Text, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, s.Label, AppTheme.Small, new Rectangle(lx + 54, ly, Width - lx - 54, lineH), AppTheme.TextSoft,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            ly += lineH;
        }
    }
}

/// <summary>Thanh phân đoạn + lưới số liệu (giống "Room Availability").</summary>
public class SegmentBar : ChartBase
{
    private List<ChartSegment> segments = [];

    public void SetData(IEnumerable<ChartSegment> data)
    {
        segments = data.ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        var total = segments.Sum(s => s.Value);
        if (segments.Count == 0) { PaintEmpty(g); return; }

        // Thanh ngang
        const int barH = 46;
        var x = 0f;
        var w = Width - 1f;
        foreach (var s in segments)
        {
            if (total <= 0 || s.Value <= 0) continue;
            var sw = (float)(s.Value / total * w);
            using var path = AppTheme.RoundedRect(new RectangleF(x, 0, Math.Max(4, sw - 3), barH), 6);
            using var brush = new SolidBrush(s.Color);
            g.FillPath(brush, path);
            x += sw;
        }

        // Lưới số liệu 2 cột
        var cols = 2;
        var cellW = Width / cols;
        var cellH = 56;
        for (var i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            var cx = (i % cols) * cellW;
            var cy = barH + 16 + (i / cols) * cellH;
            using (var b = new SolidBrush(s.Color))
                g.FillRectangle(b, cx, cy + 4, 4, cellH - 14);
            TextRenderer.DrawText(g, s.Label, AppTheme.Small, new Point(cx + 12, cy + 2), AppTheme.TextMuted);
            TextRenderer.DrawText(g, s.Value.ToString("0"), AppTheme.KpiSmall, new Point(cx + 10, cy + 18), AppTheme.Text);
        }
    }
}
