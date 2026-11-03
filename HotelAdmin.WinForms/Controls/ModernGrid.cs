using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>
/// DataGridView đã làm phẳng: header màu mint, dòng cao, kẻ ngang mảnh.
/// Cột có Tag = Func&lt;object?, StatusStyle&gt; sẽ được vẽ thành "pill" trạng thái.
/// </summary>
public class ModernGrid : DataGridView
{
    public ModernGrid()
    {
        DoubleBuffered = true;
        BorderStyle = BorderStyle.None;
        BackgroundColor = AppTheme.Surface;
        GridColor = AppTheme.Border;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        EnableHeadersVisualStyles = false;
        RowHeadersVisible = false;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        ReadOnly = true;
        MultiSelect = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = 44;
        RowTemplate.Height = 48;
        Font = AppTheme.Body;

        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = AppTheme.MintSoft,
            ForeColor = AppTheme.TextSoft,
            SelectionBackColor = AppTheme.MintSoft,
            SelectionForeColor = AppTheme.TextSoft,
            Font = AppTheme.SmallBold,
            Padding = new Padding(10, 0, 0, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = AppTheme.Surface,
            ForeColor = AppTheme.Text,
            SelectionBackColor = AppTheme.Hover,
            SelectionForeColor = AppTheme.Text,
            Font = AppTheme.Body,
            Padding = new Padding(10, 0, 4, 0)
        };
    }

    /// <summary>Thêm cột văn bản nhanh.</summary>
    public DataGridViewTextBoxColumn AddText(string name, string header, float weight = 100, string? format = null,
        DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
    {
        var col = new DataGridViewTextBoxColumn
        {
            Name = name,
            DataPropertyName = name,
            HeaderText = header,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        col.DefaultCellStyle.Alignment = align;
        if (format is not null) col.DefaultCellStyle.Format = format;
        Columns.Add(col);
        return col;
    }

    /// <summary>Thêm cột trạng thái hiển thị dạng pill.</summary>
    public DataGridViewTextBoxColumn AddStatus(string name, string header, Func<object?, StatusStyle> style, float weight = 100)
    {
        var col = AddText(name, header, weight);
        col.Tag = style;
        return col;
    }

    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);
        if (e.Handled || e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (Columns[e.ColumnIndex].Tag is not Func<object?, StatusStyle> styleOf || e.Graphics is null) return;

        e.PaintBackground(e.CellBounds, true);
        var style = styleOf(e.Value);
        var g = e.Graphics;
        AppTheme.HighQuality(g);
        var size = TextRenderer.MeasureText(style.Label, AppTheme.SmallBold);
        var rect = new RectangleF(e.CellBounds.X + 10, e.CellBounds.Y + (e.CellBounds.Height - 24) / 2f, size.Width + 16, 24);
        using (var path = AppTheme.RoundedRect(rect, 12))
        using (var brush = new SolidBrush(style.Back))
            g.FillPath(brush, path);
        TextRenderer.DrawText(g, style.Label, AppTheme.SmallBold, Rectangle.Round(rect), style.Fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        e.Handled = true;
    }
}
