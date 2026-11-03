using System.Globalization;
using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Kiểu cột của bảng nhập dòng chi tiết.</summary>
public enum LineKind { Text, Number, Combo, Date, ReadOnly }

/// <summary>Cột của LinesEditor. Combo dùng danh sách (nhãn, giá trị).</summary>
public sealed record LineCol(string Key, string Header, LineKind Kind = LineKind.Text, float Weight = 100,
    IReadOnlyList<(string Text, object Value)>? Items = null, int Decimals = 0);

/// <summary>
/// Bảng nhập nhiều dòng (dòng phiếu nhập/xuất, chi tiết chính sách giá, quy tắc hủy, đối soát ca...).
/// Đọc kết quả bằng Lines(): mỗi dòng là Dictionary khóa JSON → giá trị.
/// </summary>
public sealed class LinesEditor : Panel
{
    private readonly DataGridView grid = new();
    private readonly LineCol[] cols;

    public LinesEditor(int height, bool allowAdd, params LineCol[] cols)
    {
        this.cols = cols;
        Height = height;
        BackColor = AppTheme.Surface;
        Padding = new Padding(1);
        grid.Dock = DockStyle.Fill;
        grid.BackgroundColor = AppTheme.Surface;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.RowHeadersWidth = 28;
        grid.AllowUserToAddRows = allowAdd;
        grid.AllowUserToDeleteRows = allowAdd;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EditMode = DataGridViewEditMode.EditOnEnter;
        grid.Font = AppTheme.Body;
        grid.ColumnHeadersDefaultCellStyle.Font = AppTheme.SmallBold;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.MintSoft;
        grid.RowTemplate.Height = 30;
        grid.DataError += (_, e) => e.ThrowException = false;

        foreach (var c in cols)
        {
            DataGridViewColumn column;
            if (c.Kind == LineKind.Combo)
            {
                var combo = new DataGridViewComboBoxColumn { DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat };
                foreach (var (text, value) in c.Items ?? []) combo.Items.Add(new ComboItem(text, value));
                combo.DisplayMember = nameof(ComboItem.Text);
                combo.ValueMember = nameof(ComboItem.Value);
                column = combo;
            }
            else
            {
                column = new DataGridViewTextBoxColumn();
                if (c.Kind == LineKind.Number) column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                if (c.Kind == LineKind.ReadOnly) { column.ReadOnly = true; column.DefaultCellStyle.BackColor = AppTheme.Background; }
            }
            column.Name = c.Key;
            column.HeaderText = c.Header + (c.Kind == LineKind.Date ? " (dd/MM/yyyy)" : "");
            column.FillWeight = c.Weight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(column);
        }
        Controls.Add(grid);
    }

    /// <summary>Thêm một dòng với giá trị ban đầu theo khóa.</summary>
    public void AddRow(IDictionary<string, object?> values)
    {
        var index = grid.Rows.Add();
        var row = grid.Rows[index];
        foreach (var c in cols)
            if (values.TryGetValue(c.Key, out var v) && v is not null)
                row.Cells[c.Key].Value = c.Kind switch
                {
                    LineKind.Number when v is decimal d => d.ToString("0.###", CultureInfo.CurrentCulture),
                    LineKind.Date when v is DateTime t => t.ToString("dd/MM/yyyy"),
                    _ => v
                };
    }

    /// <summary>false: giữ cả dòng trống (khi số dòng cố định, cần khớp thứ tự).</summary>
    public bool SkipEmptyRows { get; set; } = true;

    public void Clear() => grid.Rows.Clear();

    public int Count => grid.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow);

    /// <summary>
    /// Đọc các dòng. Dòng trống (mọi ô rỗng) bị bỏ qua. Ném FormatException nếu số/ngày sai định dạng.
    /// Number → decimal, Date → "yyyy-MM-dd", Combo → giá trị, Text → chuỗi hoặc null.
    /// </summary>
    public List<Dictionary<string, object?>> Lines()
    {
        grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        grid.EndEdit();
        var result = new List<Dictionary<string, object?>>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            var line = new Dictionary<string, object?>();
            var empty = true;
            foreach (var c in cols)
            {
                var raw = row.Cells[c.Key].Value;
                var text = raw?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(text) && c.Kind != LineKind.ReadOnly) empty = false;
                line[c.Key] = c.Kind switch
                {
                    _ when string.IsNullOrEmpty(text) => null,
                    LineKind.Number => ParseNumber(text!, c.Header),
                    LineKind.Date => ParseDate(text!, c.Header),
                    LineKind.Combo => raw,
                    _ => text
                };
            }
            if (!empty || !SkipEmptyRows) result.Add(line);
        }
        return result;
    }

    private static decimal ParseNumber(string text, string header)
    {
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var d)) return d;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out d)) return d;
        throw new FormatException($"Cột \"{header}\": \"{text}\" không phải là số.");
    }

    private static string ParseDate(string text, string header)
    {
        if (DateTime.TryParseExact(text, ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        throw new FormatException($"Cột \"{header}\": \"{text}\" không đúng định dạng ngày dd/MM/yyyy.");
    }

    /// <summary>Mục của cột ComboBox.</summary>
    public sealed class ComboItem(string text, object value)
    {
        public string Text { get; } = text;
        public object Value { get; } = value;
        public override string ToString() => Text;
    }
}
