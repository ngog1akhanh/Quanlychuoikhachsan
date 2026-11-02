using System.Globalization;
using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Forms.Dialogs;

/// <summary>
/// Hộp thoại nhập liệu dựng theo khai báo trường. Mỗi trường có khóa JSON (snake_case) dùng để tạo body gửi API.
/// Ví dụ: dlg.TextField("name", "Tên", required: true); dlg.Money("price", "Đơn giá", 0); dlg.OnSubmit = v => api.PostJsonAsync(url, v);
/// </summary>
public sealed class FormDialog : DialogBase
{
    private readonly FormGrid grid;
    private readonly Dictionary<string, Func<object?>> readers = [];
    private readonly List<(string Label, Control Control, Func<bool> IsEmpty)> required = [];
    private readonly List<Control> extras = [];

    public FormDialog(string title, int width = 560, string? subtitle = null, int columns = 2, string okText = "Lưu")
        : base(title, width, 300, subtitle)
    {
        grid = new FormGrid(columns);
        Body.Controls.Add(grid);
        AddButton(okText, true, async (_, _) => await SubmitAsync());
        AddCancelButton();
    }

    /// <summary>Gọi khi bấm Lưu (đã qua kiểm tra bắt buộc). Ném ApiException để báo lỗi và giữ hộp thoại.</summary>
    public Func<Dictionary<string, object?>, Task>? OnSubmit { get; set; }

    /// <summary>Kiểm tra thêm; trả chuỗi lỗi để chặn lưu.</summary>
    public Func<Dictionary<string, object?>, string?>? Validator { get; set; }

    /// <summary>Giá trị đã nhập (sau khi bấm Lưu).</summary>
    public Dictionary<string, object?> Values { get; private set; } = [];

    // ---------------- Trường nhập ----------------
    public TextBox TextField(string key, string label, string? value = null, bool required = false, int span = 1, bool multiline = false, bool readOnly = false)
    {
        var box = grid.Add(label + (required ? " *" : ""), Inputs.Text(value, multiline, 70), span);
        box.ReadOnly = readOnly;
        readers[key] = () => string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim();
        if (required) this.required.Add((label, box, () => string.IsNullOrWhiteSpace(box.Text)));
        return box;
    }

    public TextBox Password(string key, string label, bool required = true)
    {
        var box = TextField(key, label, null, required);
        box.UseSystemPasswordChar = true;
        return box;
    }

    public NumericUpDown Number(string key, string label, decimal value, decimal min = 0, decimal max = 1_000_000_000, int decimals = 0, decimal step = 1, int span = 1)
    {
        var box = grid.Add(label, Inputs.Number(value, min, max, decimals, step), span);
        readers[key] = () => box.Value;
        return box;
    }

    public NumericUpDown Money(string key, string label, decimal value, decimal max = 100_000_000_000, int span = 1)
        => Number(key, label, value, 0, max, 0, 10_000, span);

    /// <summary>Ngày (gửi yyyy-MM-dd). nullable = có ô tick để bỏ trống.</summary>
    public DateTimePicker Date(string key, string label, DateTime? value, bool nullable = false, int span = 1)
    {
        var picker = grid.Add(label, Inputs.Date(value ?? DateTime.Today), span);
        if (nullable)
        {
            picker.ShowCheckBox = true;
            picker.Checked = value is not null;
        }
        readers[key] = () => nullable && !picker.Checked ? null : picker.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return picker;
    }

    /// <summary>Ngày giờ theo giờ máy, gửi ISO 8601 UTC (có Z).</summary>
    public DateTimePicker DateTimeField(string key, string label, DateTime? value, bool nullable = false, int span = 1)
    {
        var picker = grid.Add(label, Inputs.Date(value ?? DateTime.Now, withTime: true), span);
        if (nullable)
        {
            picker.ShowCheckBox = true;
            picker.Checked = value is not null;
        }
        readers[key] = () => nullable && !picker.Checked ? null : picker.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return picker;
    }

    /// <summary>ComboBox chọn giá trị; allowEmpty thêm dòng "(Không chọn)" trả null.</summary>
    public ComboBox Combo<T>(string key, string label, IEnumerable<(string Text, T Value)> items, object? selected = null,
        bool allowEmpty = false, string emptyText = "(Không chọn)", int span = 1, bool required = false)
    {
        var combo = grid.Add(label + (required ? " *" : ""), Inputs.Combo([]), span);
        if (allowEmpty) combo.Items.Add(new Choice<object?>(emptyText, null));
        foreach (var (text, value) in items) combo.Items.Add(new Choice<object?>(text, value));
        var index = -1;
        for (var i = 0; i < combo.Items.Count; i++)
            if (combo.Items[i] is Choice<object?> c && Equals(c.Value, selected)) { index = i; break; }
        if (index < 0 && combo.Items.Count > 0 && (!required || allowEmpty || selected is null)) index = 0;
        if (index >= 0) combo.SelectedIndex = index;
        readers[key] = () => (combo.SelectedItem as Choice<object?>)?.Value;
        if (required) this.required.Add((label, combo, () => (combo.SelectedItem as Choice<object?>)?.Value is null));
        return combo;
    }

    /// <summary>ComboBox từ mã trạng thái/loại (nhãn tiếng Việt).</summary>
    public ComboBox Codes(string key, string label, string? selected, params string[] codes)
        => Combo(key, label, Labels.Options(codes), selected ?? codes.FirstOrDefault());

    public CheckBox Check(string key, string label, bool value, int span = 1)
    {
        var box = new CheckBox { Text = label, Checked = value, AutoSize = true, Font = AppTheme.Body, Height = 30 };
        grid.Add("", box, span);
        readers[key] = () => box.Checked;
        return box;
    }

    /// <summary>Thêm control bất kỳ (lưới dòng chi tiết...) chiếm cả hàng; reader cung cấp giá trị cho key.</summary>
    public T Custom<T>(string? key, string label, T control, Func<object?>? reader = null) where T : Control
    {
        if (label.Length > 0) grid.AddFull(Inputs.Caption(label, AppTheme.SmallBold, AppTheme.TextSoft));
        grid.AddFull(control);
        extras.Add(control);
        if (key is not null && reader is not null) readers[key] = reader;
        return control;
    }

    /// <summary>Ghi chú/hướng dẫn chiếm cả hàng.</summary>
    public Label Note(string text)
    {
        var label = new Label { Text = text, AutoSize = false, Height = 40, Font = AppTheme.Small, ForeColor = AppTheme.TextMuted };
        grid.AddFull(label);
        return label;
    }

    /// <summary>Giá trị cố định gửi kèm (không hiển thị).</summary>
    public void Fixed(string key, object? value) => readers[key] = () => value;

    // ---------------- Hiển thị ----------------
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var headerHeight = string.IsNullOrEmpty(Subtitle) ? 64 : 84;
        var content = grid.GetPreferredSize(new Size(Body.ClientSize.Width - Body.Padding.Horizontal, 0)).Height + Body.Padding.Vertical;
        var screen = Screen.FromControl(this).WorkingArea.Height - 80;
        ClientSize = new Size(ClientSize.Width, Math.Clamp(content + headerHeight + 68 + 8, 220, Math.Max(400, screen)));
        CenterToParent();
    }

    private async Task SubmitAsync()
    {
        var missing = required.Where(r => r.IsEmpty()).Select(r => r.Label).ToList();
        if (missing.Count > 0)
        {
            Ui.Error(this, "Vui lòng nhập: " + string.Join(", ", missing) + ".");
            return;
        }
        try
        {
            Values = readers.ToDictionary(r => r.Key, r => r.Value());
        }
        catch (FormatException ex)
        {
            Ui.Error(this, ex.Message);
            return;
        }
        if (Validator?.Invoke(Values) is { } error)
        {
            Ui.Error(this, error);
            return;
        }
        if (OnSubmit is null || await RunAsync(() => OnSubmit(Values))) CloseOk();
    }

    /// <summary>Hiển thị và trả true nếu đã lưu thành công.</summary>
    public bool ShowFor(IWin32Window? owner) => ShowDialog(owner) == DialogResult.OK;

    // ---------------- Tiện ích nạp giá trị từ JSON ----------------
    public static string? S(JsonElement? e, string path) => e is { } v ? J.StrOrNull(v, path) : null;
    public static decimal D(JsonElement? e, string path, decimal fallback = 0) => e is { } v ? J.Dec(v, path, fallback) : fallback;
    public static long? L(JsonElement? e, string path) => e is { } v ? J.LongOrNull(v, path) : null;
    public static bool B(JsonElement? e, string path) => e is { } v && J.Bool(v, path);
    public static DateTime? Day(JsonElement? e, string path) => e is { } v ? J.Day(v, path) : null;
}
