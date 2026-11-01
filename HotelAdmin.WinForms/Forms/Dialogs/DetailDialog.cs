using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Theme;

namespace HotelAdmin.Forms.Dialogs;

/// <summary>
/// Hộp thoại xem chi tiết: lưới thông tin (nhãn – giá trị) và các bảng dòng con (dòng hóa đơn, thanh toán...).
/// Có thể thêm nút thao tác; nút trả true sẽ đóng hộp thoại với DialogResult.OK để trang tải lại.
/// </summary>
public sealed class DetailDialog : DialogBase
{
    private readonly TableLayoutPanel info = new()
    {
        Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 8)
    };
    private readonly Panel sections = new() { Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent };
    private int sectionTop;

    public DetailDialog(string title, string? subtitle = null, int width = 820, int height = 600) : base(title, width, height, subtitle)
    {
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        Body.Controls.Add(sections);
        Body.Controls.Add(info);
        AddCancelButton("Đóng");
    }

    /// <summary>Thêm các cặp nhãn – giá trị (2 cặp mỗi hàng).</summary>
    public DetailDialog Info(params (string Label, string? Value)[] pairs)
    {
        foreach (var (label, value) in pairs)
        {
            info.Controls.Add(new Label { Text = label, AutoSize = true, Font = AppTheme.SmallBold, ForeColor = AppTheme.TextSoft, Margin = new Padding(0, 6, 8, 6) });
            info.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(value) ? "—" : value, AutoSize = true, MaximumSize = new Size(Width / 2 - 140, 0),
                Font = AppTheme.Body, ForeColor = AppTheme.Text, Margin = new Padding(0, 6, 12, 6)
            });
        }
        return this;
    }

    /// <summary>Thêm bảng dòng con có tiêu đề.</summary>
    public DetailDialog Table(string title, IEnumerable<JsonElement> rows, int height, params Col[] cols)
    {
        var caption = Inputs.Caption(title, AppTheme.CardTitle, AppTheme.Text);
        caption.Location = new Point(0, sectionTop + 6);
        sections.Controls.Add(caption);
        var grid = new ModernGrid { Location = new Point(0, sectionTop + 36), Height = height, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
        grid.Width = ClientSize.Width - Body.Padding.Horizontal - 20;
        grid.RowTemplate.Height = 36;
        grid.AutoGenerateColumns = false;
        for (var i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c.Kind == ColKind.Status) grid.AddStatus("c" + i, c.Header, v => Labels.Style(v as string), c.Weight);
            else grid.AddText("c" + i, c.Header, c.Weight, null,
                c.Kind == ColKind.Right ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft);
        }
        var list = rows.ToList();
        foreach (var row in list) grid.Rows.Add(cols.Select(c => c.Value(row) ?? "").ToArray());
        if (list.Count == 0)
        {
            var empty = Inputs.Caption("Không có dữ liệu", AppTheme.Small, AppTheme.TextMuted);
            empty.Location = new Point(12, sectionTop + 36 + 52);
            sections.Controls.Add(empty);
            empty.BringToFront();
        }
        sections.Controls.Add(grid);
        Tables.Add((grid, list));
        sectionTop += 36 + height + 12;
        sections.Height = sectionTop;
        return this;
    }

    /// <summary>Các bảng đã thêm (để đọc dòng đang chọn trong thao tác).</summary>
    public List<(ModernGrid Grid, List<JsonElement> Rows)> Tables { get; } = [];

    /// <summary>Dòng đang chọn của bảng thứ index.</summary>
    public JsonElement? SelectedOf(int index)
    {
        if (index >= Tables.Count) return null;
        var (grid, rows) = Tables[index];
        return grid.CurrentRow is { Index: var i } && i >= 0 && i < rows.Count ? rows[i] : null;
    }

    /// <summary>Nút thao tác: trả true để đóng hộp thoại (OK).</summary>
    public DetailDialog Action(string text, Func<Task<bool>> run, bool primary = false)
    {
        AddButton(text, primary, async (_, _) =>
        {
            var done = false;
            if (await RunAsync(async () => done = await run()) && done) CloseOk();
        });
        return this;
    }
}
