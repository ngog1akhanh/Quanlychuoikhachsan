using System.Text.Json;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Kiểu hiển thị của một cột.</summary>
public enum ColKind { Text, Right, Status }

/// <summary>Định nghĩa cột của DataView: tiêu đề + hàm lấy giá trị từ dòng JSON.</summary>
public sealed record Col(string Header, Func<JsonElement, object?> Value, float Weight = 100, ColKind Kind = ColKind.Text)
{
    public static Col Text(string header, string path, float weight = 100) => new(header, e => J.Str(e, path), weight);
    public static Col Custom(string header, Func<JsonElement, object?> value, float weight = 100) => new(header, value, weight);
    public static Col Money(string header, string path, float weight = 80) => new(header, e => J.Money(J.Dec(e, path)), weight, ColKind.Right);
    public static Col Qty(string header, string path, float weight = 60) => new(header, e => J.Qty(J.Dec(e, path)), weight, ColKind.Right);
    public static Col Time(string header, string path, float weight = 90) => new(header, e => J.TimeText(J.Time(e, path)), weight);
    public static Col Day(string header, string path, float weight = 80) => new(header, e => J.DateText(J.Day(e, path) ?? J.Time(e, path)), weight);
    public static Col Label(string header, string path, float weight = 80) => new(header, e => Labels.Of(J.StrOrNull(e, path)), weight);
    public static Col Status(string header, string path, float weight = 90) => new(header, e => J.StrOrNull(e, path), weight, ColKind.Status);
    public static Col Bool(string header, string path, float weight = 50) => new(header, e => J.Bool(e, path) ? "Có" : "", weight);
}

/// <summary>Tham số tải danh sách.</summary>
public sealed record DataQuery(int Page, int PageSize, string? Search, string? Status);

/// <summary>Trang con có thể tải lại (một tab của module).</summary>
public interface IReloadable
{
    Task ReloadAsync();
}

/// <summary>
/// Thẻ danh sách dùng chung cho các module: tiêu đề + nút thao tác, ô tìm kiếm, lọc trạng thái,
/// lưới dữ liệu JSON và phân trang. Mỗi module chỉ khai báo cột, hàm tải và các thao tác.
/// </summary>
public sealed class DataView : UserControl, IReloadable
{
    private readonly UserSession session;
    private readonly RoundedPanel card = new() { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 14) };
    private readonly FlowLayoutPanel actions = new()
    {
        Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
        BackColor = Color.Transparent, Margin = new Padding(0)
    };
    private readonly FlowLayoutPanel filters = new()
    {
        Dock = DockStyle.Top, Height = 0, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 6)
    };
    private readonly ModernGrid grid = new() { Dock = DockStyle.Fill };
    private readonly Label info = Inputs.Caption("", AppTheme.Small, AppTheme.TextMuted);
    private readonly FlowLayoutPanel pager = new()
    {
        Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0)
    };
    private readonly List<(FlatButton Button, bool NeedsRow, Func<JsonElement, bool>? When)> buttons = [];
    private Col[] columns = [];
    private RoundedTextBox? search;
    private ComboBox? statusBox;
    private List<JsonElement> all = [];
    private List<JsonElement> rows = [];
    private int page = 1;
    private int total = -1;
    private bool loading;

    public DataView(UserSession session, string title)
    {
        this.session = session;
        Dock = DockStyle.Fill;
        BackColor = AppTheme.Background;
        Padding = new Padding(0);

        var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Transparent };
        header.Controls.Add(new Label
        {
            Text = title, Font = AppTheme.CardTitle, ForeColor = AppTheme.Text, AutoSize = true, Location = new Point(0, 8), BackColor = Color.Transparent
        });
        header.Controls.Add(actions);

        grid.AutoGenerateColumns = false;
        grid.SelectionChanged += (_, _) => { UpdateButtons(); SelectionChanged?.Invoke(this, EventArgs.Empty); };
        grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0 && DefaultAction is not null) await RunActionAsync(DefaultAction, true);
        };

        var next = new FlatButton { IconGlyph = "", Size = new Size(40, 30), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border };
        var prev = new FlatButton { IconGlyph = "", Size = new Size(40, 30), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border };
        next.Click += async (_, _) => { if (Paged && total > page * PageSize) { page++; await ReloadAsync(); } };
        prev.Click += async (_, _) => { if (Paged && page > 1) { page--; await ReloadAsync(); } };
        info.Margin = new Padding(0, 7, 12, 0);
        pager.Controls.AddRange([next, prev, info]);

        card.Controls.Add(grid);
        card.Controls.Add(pager);
        card.Controls.Add(filters);
        card.Controls.Add(header);
        Controls.Add(card);
    }

    /// <summary>Hàm tải dữ liệu (bắt buộc).</summary>
    public Func<DataQuery, Task<JsonElement>>? Loader { get; set; }

    /// <summary>true: API phân trang (page/pageSize, trả total); false: tải hết, tìm kiếm tại máy.</summary>
    public bool Paged { get; set; }

    public int PageSize { get; set; } = 30;

    /// <summary>Thao tác khi bấm đúp một dòng.</summary>
    public Func<JsonElement?, Task<bool>>? DefaultAction { get; set; }

    /// <summary>Đường dẫn trường id dùng để giữ dòng đang chọn sau khi tải lại.</summary>
    public string IdPath { get; set; } = "id";

    public event EventHandler? SelectionChanged;

    public IReadOnlyList<JsonElement> Rows => rows;

    public JsonElement? Selected
        => grid.CurrentRow is { Index: var i } && i >= 0 && i < rows.Count ? rows[i] : null;

    public string? SearchText => string.IsNullOrWhiteSpace(search?.Text) ? null : search!.Text.Trim();

    public string? StatusFilter => (statusBox?.SelectedItem as Choice<string?>)?.Value;

    // ---------------- Cấu hình ----------------
    public DataView Columns(params Col[] cols)
    {
        columns = cols;
        grid.Columns.Clear();
        for (var i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c.Kind == ColKind.Status) grid.AddStatus("c" + i, c.Header, v => Labels.Style(v as string), c.Weight);
            else grid.AddText("c" + i, c.Header, c.Weight, null,
                c.Kind == ColKind.Right ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft);
        }
        return this;
    }

    public DataView AddSearch(string placeholder, int width = 240)
    {
        search = new RoundedTextBox(Glyph.Search) { Width = width, Height = 36, PlaceholderText = placeholder };
        search.Inner.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            page = 1;
            await ReloadAsync();
        };
        AddFilter(search);
        return this;
    }

    /// <summary>Lọc trạng thái; codes rỗng = không có bộ lọc.</summary>
    public DataView AddStatusFilter(string? initial, params string[] codes)
    {
        statusBox = Inputs.Combo([]);
        statusBox.Width = 170;
        statusBox.Items.Add(new Choice<string?>("Tất cả trạng thái", null));
        foreach (var code in codes) statusBox.Items.Add(new Choice<string?>(Labels.Style(code).Label, code));
        statusBox.SelectedIndex = initial is null ? 0 : Math.Max(0, Array.IndexOf(codes, initial) + 1);
        statusBox.SelectionChangeCommitted += async (_, _) => { page = 1; await ReloadAsync(); };
        AddFilter(statusBox);
        return this;
    }

    /// <summary>Thêm control lọc tùy ý vào thanh lọc (ComboBox kho, ô ngày...).</summary>
    public T AddFilter<T>(T control) where T : Control
    {
        filters.Height = 46;
        control.Margin = new Padding(0, control is Label ? 10 : 2, 8, 0);
        filters.Controls.Add(control);
        return control;
    }

    /// <summary>Thêm nút "Lọc" tải lại theo bộ lọc hiện tại.</summary>
    public DataView AddApplyButton(string text = "Lọc")
    {
        var apply = new FlatButton { Text = text, Size = new Size(70, 34), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border };
        apply.Click += async (_, _) => { page = 1; await ReloadAsync(); };
        AddFilter(apply);
        return this;
    }

    /// <summary>
    /// Thêm nút thao tác ở góc phải tiêu đề (thêm theo thứ tự từ phải sang trái).
    /// Trả true trong run để tải lại danh sách. Ẩn nút nếu người dùng không có quyền.
    /// </summary>
    public DataView AddAction(string text, Func<JsonElement?, Task<bool>> run, string? permission = null, bool needsRow = true,
        bool primary = false, Func<JsonElement, bool>? when = null)
    {
        if (permission is not null && !permission.Split('|').Any(session.HasPermission)) return this;
        var button = new FlatButton
        {
            Text = text,
            Size = new Size(TextRenderer.MeasureText(text, AppTheme.BodyBold).Width + 28, 34),
            FillColor = primary ? AppTheme.Lime : AppTheme.Surface,
            HoverColor = primary ? AppTheme.LimeHover : AppTheme.Hover,
            BorderColor = primary ? Color.Transparent : AppTheme.Border,
            Margin = new Padding(6, 4, 0, 0)
        };
        button.Click += async (_, _) => await RunActionAsync(run, needsRow);
        actions.Controls.Add(button);
        buttons.Add((button, needsRow, when));
        UpdateButtons();
        return this;
    }

    // ---------------- Tải dữ liệu ----------------
    public async Task ReloadAsync()
    {
        if (Loader is null || loading) return;
        loading = true;
        var keepId = Selected is { } current ? J.Str(current, IdPath) : null;
        UseWaitCursor = true;
        try
        {
            var data = await Loader(new DataQuery(page, PageSize, SearchText, StatusFilter));
            all = J.Items(data);
            total = J.Total(data);
            rows = Paged || SearchText is null ? all : all.Where(MatchesSearch).ToList();
            Fill(keepId);
        }
        catch (ApiException ex)
        {
            all = rows = [];
            Fill(null);
            info.Text = ex.Message.Length > 90 ? ex.Message[..90] + "…" : ex.Message;
            if (!ex.IsUnauthorized) Ui.Error(FindForm(), ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
            loading = false;
        }
    }

    private bool MatchesSearch(JsonElement row)
    {
        var s = SearchText!;
        return columns.Any(c => c.Value(row)?.ToString()?.Contains(s, StringComparison.CurrentCultureIgnoreCase) == true);
    }

    private void Fill(string? keepId)
    {
        grid.Rows.Clear();
        foreach (var row in rows)
            grid.Rows.Add(columns.Select(c => c.Value(row) ?? "").ToArray());
        if (keepId is not null)
        {
            var index = rows.FindIndex(r => J.Str(r, IdPath) == keepId);
            if (index >= 0) grid.CurrentCell = grid.Rows[index].Cells[0];
        }
        if (Paged && total >= 0)
        {
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            info.Text = $"{total} dòng · trang {page}/{pages}";
        }
        else info.Text = rows.Count == all.Count ? $"{rows.Count} dòng" : $"{rows.Count}/{all.Count} dòng";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var selected = Selected;
        foreach (var (button, needsRow, when) in buttons)
            button.Enabled = !needsRow || (selected is { } row && (when is null || when(row)));
    }

    private async Task RunActionAsync(Func<JsonElement?, Task<bool>> run, bool needsRow)
    {
        var selected = Selected;
        if (needsRow && selected is null)
        {
            Ui.Error(FindForm(), "Chọn một dòng trong danh sách.");
            return;
        }
        try
        {
            if (await run(selected)) await ReloadAsync();
        }
        catch (ApiException ex)
        {
            Ui.Error(FindForm(), ex.Message);
        }
    }

    /// <summary>Đổi về trang 1 (khi bộ lọc bên ngoài thay đổi).</summary>
    public void ResetPage() => page = 1;
}
