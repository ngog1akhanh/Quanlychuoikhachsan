using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Thanh tab dạng "viên thuốc" (Sơ đồ phòng | Loại phòng | Lịch phòng...).</summary>
public class SegmentedTabs : Control
{
    private readonly List<string> items = [];
    private int selectedIndex;
    private int hoverIndex = -1;

    public SegmentedTabs(params string[] tabs)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = AppTheme.BodyBold;
        Height = 40;
        Cursor = Cursors.Hand;
        items.AddRange(tabs);
        Width = MeasureWidth();
    }

    public event EventHandler? SelectedIndexChanged;

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < 0 || value >= items.Count || value == selectedIndex) return;
            selectedIndex = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int MeasureWidth() => items.Sum(t => TextRenderer.MeasureText(t, Font).Width + 32) + 8;

    private Rectangle ItemRect(int index)
    {
        var x = 4;
        for (var i = 0; i < index; i++) x += TextRenderer.MeasureText(items[i], Font).Width + 32;
        return new Rectangle(x, 4, TextRenderer.MeasureText(items[index], Font).Width + 32, Height - 8);
    }

    private int HitTest(Point p)
    {
        for (var i = 0; i < items.Count; i++) if (ItemRect(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var h = HitTest(e.Location);
        if (h != hoverIndex) { hoverIndex = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        var h = HitTest(e.Location);
        if (h >= 0) SelectedIndex = h;
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        using (var path = AppTheme.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 12))
        using (var bg = new SolidBrush(AppTheme.Surface))
        using (var pen = new Pen(AppTheme.Border))
        {
            g.FillPath(bg, path);
            g.DrawPath(pen, path);
        }
        for (var i = 0; i < items.Count; i++)
        {
            var r = ItemRect(i);
            if (i == selectedIndex || i == hoverIndex)
            {
                using var path = AppTheme.RoundedRect(r, 9);
                using var brush = new SolidBrush(i == selectedIndex ? AppTheme.Lime : AppTheme.Hover);
                g.FillPath(brush, path);
            }
            TextRenderer.DrawText(g, items[i], Font, r, i == selectedIndex ? AppTheme.Text : AppTheme.TextSoft,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

/// <summary>Ô phòng trên sơ đồ tầng: số phòng, loại, trạng thái (màu), khách đang ở / khách sắp đến.</summary>
public class RoomTile : Control
{
    private bool hover;

    public RoomTile()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(156, 112);
        Margin = new Padding(0, 0, 12, 12);
        Cursor = Cursors.Hand;
    }

    public string RoomNumber { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string StatusCode { get; set; } = "SAN_SANG";
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        var style = StatusStyles.ForRoom(StatusCode);
        var accent = StatusStyles.RoomChartColor(StatusCode);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

        using (var path = AppTheme.RoundedRect(rect, 12))
        {
            using var fill = new SolidBrush(hover ? AppTheme.Hover : AppTheme.Surface);
            g.FillPath(fill, path);
            using var pen = new Pen(hover ? accent : AppTheme.Border, hover ? 1.5f : 1f);
            g.DrawPath(pen, path);
        }
        using (var bar = new SolidBrush(accent))
            g.FillRectangle(bar, 12, 14, 4, 26);

        TextRenderer.DrawText(g, RoomNumber, AppTheme.KpiSmall, new Point(20, 10), AppTheme.Text);
        TextRenderer.DrawText(g, TypeName, AppTheme.Small, new Rectangle(22, 40, Width - 30, 18), AppTheme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        // Pill trạng thái góc trên phải
        var pillW = TextRenderer.MeasureText(style.Label, AppTheme.SmallBold).Width + 12;
        var pill = new RectangleF(Width - pillW - 10, 12, pillW, 20);
        using (var pp = AppTheme.RoundedRect(pill, 10))
        using (var pb = new SolidBrush(style.Back))
            g.FillPath(pb, pp);
        TextRenderer.DrawText(g, style.Label, AppTheme.SmallBold, Rectangle.Round(pill), style.Fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        if (!string.IsNullOrEmpty(Line1))
            TextRenderer.DrawText(g, Line1, AppTheme.SmallBold, new Rectangle(12, 64, Width - 20, 18), AppTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (!string.IsNullOrEmpty(Line2))
            TextRenderer.DrawText(g, Line2, AppTheme.Small, new Rectangle(12, 84, Width - 20, 18), AppTheme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

/// <summary>
/// Lưới nhập liệu: mỗi ô gồm nhãn phía trên + control phía dưới. Dùng cho các dialog thêm/sửa.
/// </summary>
public class FormGrid : TableLayoutPanel
{
    private int nextColumn;
    private int nextRow;

    public FormGrid(int columns = 2)
    {
        ColumnCount = columns;
        for (var i = 0; i < columns; i++) ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Top;
        BackColor = Color.Transparent;
        Padding = new Padding(0);
        Margin = new Padding(0);
    }

    /// <summary>Thêm một trường. span = số cột chiếm.</summary>
    public T Add<T>(string label, T control, int span = 1) where T : Control
    {
        span = Math.Min(span, ColumnCount);
        if (nextColumn + span > ColumnCount) { nextColumn = 0; nextRow++; }

        var cell = new Panel { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = control.Height + 24, Margin = new Padding(0, 0, 14, 10), BackColor = Color.Transparent };
        var lbl = new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 22,
            Font = AppTheme.SmallBold,
            ForeColor = AppTheme.TextSoft,
            BackColor = Color.Transparent
        };
        control.Dock = DockStyle.Top;
        cell.Controls.Add(control);
        cell.Controls.Add(lbl);

        while (RowStyles.Count <= nextRow) RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowCount = Math.Max(RowCount, nextRow + 1);
        Controls.Add(cell, nextColumn, nextRow);
        if (span > 1) SetColumnSpan(cell, span);
        nextColumn += span;
        if (nextColumn >= ColumnCount) { nextColumn = 0; nextRow++; }
        return control;
    }

    /// <summary>Thêm một control chiếm trọn hàng (không nhãn).</summary>
    public T AddFull<T>(T control) where T : Control
    {
        if (nextColumn != 0) { nextColumn = 0; nextRow++; }
        while (RowStyles.Count <= nextRow) RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowCount = Math.Max(RowCount, nextRow + 1);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        control.Margin = new Padding(0, 0, 14, 10);
        Controls.Add(control, 0, nextRow);
        SetColumnSpan(control, ColumnCount);
        nextRow++;
        return control;
    }
}

/// <summary>Tạo nhanh các control nhập liệu cùng một kiểu dáng.</summary>
public static class Inputs
{
    public static TextBox Text(string? value = null, bool multiline = false, int height = 30) => new()
    {
        Text = value ?? "",
        Font = AppTheme.Body,
        BorderStyle = BorderStyle.FixedSingle,
        Multiline = multiline,
        Height = multiline ? height : 30,
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
    };

    public static NumericUpDown Number(decimal value, decimal min, decimal max, int decimals = 0, decimal increment = 1) => new()
    {
        Minimum = min,
        Maximum = max,
        DecimalPlaces = decimals,
        Increment = increment,
        ThousandsSeparator = true,
        Value = Math.Min(max, Math.Max(min, value)),
        Font = AppTheme.Body,
        Height = 30,
        TextAlign = HorizontalAlignment.Right
    };

    public static DateTimePicker Date(DateTime value, bool withTime = false) => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = withTime ? "dd/MM/yyyy HH:mm" : "dd/MM/yyyy",
        Value = value,
        Font = AppTheme.Body,
        Height = 30
    };

    public static ComboBox Combo(IEnumerable<object> items, int selectedIndex = 0)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = AppTheme.Body,
            Height = 30
        };
        foreach (var item in items) combo.Items.Add(item);
        if (combo.Items.Count > selectedIndex) combo.SelectedIndex = selectedIndex;
        return combo;
    }

    public static Label Caption(string text, Font? font = null, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Font = font ?? AppTheme.Body,
        ForeColor = color ?? AppTheme.Text,
        BackColor = Color.Transparent
    };
}

/// <summary>Một lựa chọn trong ComboBox: hiển thị Text, giữ Value.</summary>
public sealed record Choice<T>(string Text, T Value)
{
    public override string ToString() => Text;
}
