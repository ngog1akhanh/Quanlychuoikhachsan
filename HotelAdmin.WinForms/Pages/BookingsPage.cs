using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Trang Đặt phòng (mục 7):
///  • Tab "Danh sách booking": tìm theo mã/tên/SĐT/số phòng, lọc trạng thái và khoảng ngày lưu trú, phân trang;
///    bên phải là chi tiết booking (giống mẫu Guest Profile) với các nút thao tác theo trạng thái.
///  • Tab "Yêu cầu hủy": quản lý duyệt/từ chối yêu cầu hủy booking đã cọc (mục 7.6).
/// </summary>
public sealed class BookingsPage : PageBase
{
    private const int PageSize = 30;
    private static readonly string[] Statuses = ["HOLD", "BOOKED", "CHO_DUYET_HUY", "DANG_O", "DA_TRA", "DA_HUY", "NO_SHOW"];

    private readonly SegmentedTabs tabs = new("Danh sách booking", "Yêu cầu hủy");
    private readonly Panel content = new() { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
    private readonly FlowLayoutPanel toolbar = new();

    // Danh sách
    private readonly TableLayoutPanel listLayout = new();
    private readonly RoundedTextBox search = new(Glyph.Search) { Width = 260, Height = 38, PlaceholderText = "Mã, tên khách, SĐT, số phòng…" };
    private readonly ComboBox status = Inputs.Combo([]);
    private readonly DateTimePicker fromDate = Inputs.Date(DateTime.Today.AddDays(-7));
    private readonly DateTimePicker toDate = Inputs.Date(DateTime.Today.AddDays(30));
    private readonly ModernGrid grid = new() { Dock = DockStyle.Fill };
    private readonly Label pageInfo = Inputs.Caption("", AppTheme.Small, AppTheme.TextMuted);
    private readonly BookingDetailView detail;
    private List<FrontDeskBookingItem> rows = [];
    private int page = 1;
    private int total;
    private long? selectedId;

    // Yêu cầu hủy
    private readonly Panel cancelPanel = new() { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
    private readonly ModernGrid cancelGrid = new() { Dock = DockStyle.Fill };
    private readonly CheckBox onlyPending = new() { Text = "Chỉ yêu cầu chờ duyệt", Checked = true, AutoSize = true };
    private List<CancellationRequestItem> cancels = [];

    public BookingsPage(ApiClient api, UserSession session) : base(api, session)
    {
        Padding = new Padding(24, 0, 24, 16);
        detail = new BookingDetailView(session) { Dock = DockStyle.Fill };
        detail.ActionRequested += async (_, action) => await OnDetailActionAsync(action);

        status.Items.Add(new Choice<string?>("Tất cả trạng thái", null));
        foreach (var s in Statuses) status.Items.Add(new Choice<string?>(StatusStyles.ForBooking(s).Label, s));
        status.SelectedIndex = 0;
        fromDate.ShowCheckBox = true;
        toDate.ShowCheckBox = true;
        fromDate.Checked = false;
        toDate.Checked = false;

        BuildToolbar();
        BuildList();
        BuildCancels();
        Controls.Add(content);
        Controls.Add(toolbar);
        tabs.SelectedIndexChanged += async (_, _) => await ReloadSafeAsync();
        ShowTab();
    }

    public override string Title => "Đặt phòng";

    // =====================================================================
    private void BuildToolbar()
    {
        toolbar.Dock = DockStyle.Top;
        toolbar.Height = 56;
        toolbar.WrapContents = false;
        toolbar.BackColor = AppTheme.Background;
        toolbar.Padding = new Padding(0, 4, 0, 8);
        toolbar.Controls.Add(tabs);
        if (Session.HasPermission("BOOKING_WRITE"))
        {
            var create = new FlatButton { Text = "Tạo booking", IconGlyph = "", Size = new Size(150, 40), Margin = new Padding(12, 0, 0, 0) };
            create.Click += async (_, _) => await CreateBookingAsync();
            toolbar.Controls.Add(create);
        }
    }

    private void BuildList()
    {
        listLayout.Dock = DockStyle.Fill;
        listLayout.ColumnCount = 2;
        listLayout.RowCount = 1;
        listLayout.BackColor = AppTheme.Background;
        listLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        listLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ---- Trái: bộ lọc + lưới + phân trang ----
        var (listCard, listBody, _) = CreateCard("Danh sách booking", 0);
        listCard.Margin = new Padding(0, 0, 12, 0);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, WrapContents = false, BackColor = Color.Transparent };
        status.Width = 160;
        fromDate.Width = 130;
        toDate.Width = 130;
        var apply = new FlatButton { Text = "Lọc", Size = new Size(70, 36) };
        apply.Click += async (_, _) => { page = 1; await ReloadSafeAsync(); };
        search.Inner.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            page = 1;
            await ReloadSafeAsync();
        };
        filters.Controls.AddRange([search, status, Inputs.Caption("Lưu trú từ", AppTheme.Small, AppTheme.TextMuted), fromDate,
            Inputs.Caption("đến", AppTheme.Small, AppTheme.TextMuted), toDate, apply]);
        foreach (Control c in filters.Controls) c.Margin = new Padding(0, c is Label ? 12 : 4, 8, 0);

        grid.AutoGenerateColumns = false;
        grid.AddText("code", "Mã", 90);
        grid.AddText("guest", "Khách", 120).DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.AddText("rooms", "Phòng", 60);
        grid.AddText("in", "Nhận", 70);
        grid.AddText("out", "Trả", 70);
        grid.AddText("total", "Tổng tiền", 80, "#,0", DataGridViewContentAlignment.MiddleRight);
        grid.AddText("channel", "Kênh", 60);
        grid.AddStatus("status", "Trạng thái", v => StatusStyles.ForBooking(v as string), 95);
        grid.SelectionChanged += async (_, _) => await OnSelectionChangedAsync();

        var pager = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
        var next = new FlatButton { IconGlyph = "", Size = new Size(40, 32), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border };
        var prev = new FlatButton { IconGlyph = "", Size = new Size(40, 32), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border };
        next.Click += async (_, _) => { if (page * PageSize < total) { page++; await ReloadSafeAsync(); } };
        prev.Click += async (_, _) => { if (page > 1) { page--; await ReloadSafeAsync(); } };
        pageInfo.Margin = new Padding(0, 8, 12, 0);
        pager.Controls.AddRange([next, prev, pageInfo]);

        listBody.Controls.Add(grid);
        listBody.Controls.Add(pager);
        listBody.Controls.Add(filters);

        // ---- Phải: chi tiết ----
        var (detailCard, detailBody, _) = CreateCard("Chi tiết booking", 0);
        detailBody.Controls.Add(detail);

        listLayout.Controls.Add(listCard, 0, 0);
        listLayout.Controls.Add(detailCard, 1, 0);
    }

    private void BuildCancels()
    {
        var (card, body, header) = CreateCard("Yêu cầu hủy booking", 0);
        onlyPending.Margin = new Padding(0, 10, 0, 0);
        onlyPending.CheckedChanged += async (_, _) => await ReloadSafeAsync();
        header.Controls.Add(onlyPending);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false, BackColor = Color.Transparent };
        var canApprove = Session.HasPermission("CANCELLATION_APPROVE");
        var approve = new FlatButton { Text = "Duyệt hủy", IconGlyph = "", Size = new Size(130, 36), Enabled = canApprove };
        var reject = new FlatButton { Text = "Từ chối", IconGlyph = "", Size = new Size(110, 36), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border, Enabled = canApprove, Margin = new Padding(8, 0, 0, 0) };
        var view = new FlatButton { Text = "Xem booking", Size = new Size(130, 36), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border, Margin = new Padding(8, 0, 0, 0) };
        approve.Click += async (_, _) => await DecideAsync(true);
        reject.Click += async (_, _) => await DecideAsync(false);
        view.Click += async (_, _) =>
        {
            if (SelectedCancel() is not { } c) return;
            using var dlg = new DlgBookingDetail(Api, Session, c.BookingId);
            dlg.ShowDialog(FindForm());
            if (dlg.Changed) await ReloadSafeAsync();
        };
        actions.Controls.AddRange([approve, reject, view]);
        if (!canApprove) actions.Controls.Add(Inputs.Caption("Chỉ Quản lý/Admin được duyệt.", AppTheme.Small, AppTheme.TextMuted));

        cancelGrid.AutoGenerateColumns = false;
        cancelGrid.AddText("time", "Thời gian", 80);
        cancelGrid.AddText("booking", "Booking", 90);
        cancelGrid.AddText("guest", "Khách", 110);
        cancelGrid.AddText("reason", "Lý do", 170);
        cancelGrid.AddText("expected", "Hoàn dự kiến", 80, "#,0", DataGridViewContentAlignment.MiddleRight);
        cancelGrid.AddText("approved", "Hoàn duyệt", 80, "#,0", DataGridViewContentAlignment.MiddleRight);
        cancelGrid.AddText("status", "Trạng thái", 70);

        body.Controls.Add(cancelGrid);
        body.Controls.Add(actions);
        cancelPanel.Controls.Add(card);
    }

    private void ShowTab()
    {
        content.Controls.Clear();
        content.Controls.Add(tabs.SelectedIndex == 0 ? listLayout : cancelPanel);
    }

    // =====================================================================
    public override async Task LoadDataAsync()
    {
        ShowTab();
        if (tabs.SelectedIndex == 0) await LoadListAsync();
        else await LoadCancelsAsync();
    }

    private async Task LoadListAsync()
    {
        var st = status.SelectedItem is Choice<string?> c ? c.Value : null;
        DateOnly? fromD = fromDate.Checked ? DateOnly.FromDateTime(fromDate.Value) : null;
        DateOnly? toD = toDate.Checked ? DateOnly.FromDateTime(toDate.Value) : null;
        var result = await Api.SearchBookingsAsync(search.Text, st, fromD, toD, page, PageSize);
        rows = result.Items;
        total = result.Total;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        pageInfo.Text = $"{total} booking · trang {page}/{pages}";

        var keep = selectedId;
        grid.Rows.Clear();
        foreach (var b in rows)
            grid.Rows.Add(b.BookingCode, $"{b.CustomerName}\n{b.CustomerPhone}".Trim(), b.Rooms, b.CheckIn.ToString("dd/MM HH:mm"),
                b.CheckOut.ToString("dd/MM HH:mm"), b.Total, BookingDetailView.ChannelText(b.Channel), b.Status);

        // Giữ dòng đang chọn sau khi tải lại.
        var index = keep is null ? -1 : rows.FindIndex(r => r.Id == keep);
        if (index >= 0)
        {
            grid.ClearSelection();
            grid.Rows[index].Selected = true;
            grid.CurrentCell = grid.Rows[index].Cells[0];
            await ShowDetailAsync(rows[index].Id);
        }
        else if (rows.Count == 0)
        {
            detail.ShowMessage("Không có booking phù hợp bộ lọc.");
        }
    }

    private async Task OnSelectionChangedAsync()
    {
        if (grid.CurrentRow is null || grid.CurrentRow.Index >= rows.Count) return;
        var id = rows[grid.CurrentRow.Index].Id;
        if (id == selectedId && detail.Detail?.Summary.Id == id) return;
        selectedId = id;
        try { await ShowDetailAsync(id); }
        catch (ApiException ex) { detail.ShowMessage(ex.Message); }
    }

    private async Task ShowDetailAsync(long id) => detail.SetDetail(await Api.GetBookingDetailAsync(id));

    private async Task OnDetailActionAsync(string action)
    {
        if (detail.Detail is null) return;
        if (action == "refresh" || await BookingActions.RunAsync(action, FindForm()!, Api, detail.Detail))
            await ReloadSafeAsync();
    }

    private async Task CreateBookingAsync()
    {
        using var dlg = new DlgCreateBooking(Api);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK || dlg.CreatedBooking is null) return;
        selectedId = dlg.CreatedBooking.Summary.Id;
        tabs.SelectedIndex = 0;
        search.Text = dlg.CreatedBooking.Summary.BookingCode;
        page = 1;
        await ReloadSafeAsync();
    }

    // ---------------- Yêu cầu hủy ----------------
    private async Task LoadCancelsAsync()
    {
        cancels = await Api.GetCancellationRequestsAsync(onlyPending.Checked ? "CHO_DUYET" : null);
        cancelGrid.Rows.Clear();
        foreach (var c in cancels)
            cancelGrid.Rows.Add(c.RequestedAt.ToString("dd/MM HH:mm"), c.BookingCode, c.CustomerName, c.Reason,
                c.ExpectedRefund, c.ApprovedRefund, BookingDetailView.CancelStatusText(c.Status));
    }

    private CancellationRequestItem? SelectedCancel()
        => cancelGrid.CurrentRow is { } r && r.Index < cancels.Count ? cancels[r.Index] : null;

    private async Task DecideAsync(bool approve)
    {
        if (SelectedCancel() is not { } c) { Ui.Error(FindForm(), "Chọn một yêu cầu."); return; }
        if (c.Status != "CHO_DUYET") { Ui.Error(FindForm(), "Yêu cầu này đã được xử lý."); return; }
        try
        {
            if (approve)
            {
                var refund = Ui.PromptMoney(FindForm(), $"Duyệt hủy {c.BookingCode}", $"Số tiền hoàn cọc (tối đa {c.ExpectedRefund:#,0} đ)", c.ExpectedRefund, c.ExpectedRefund);
                if (refund is null) return;
                var note = Ui.Prompt(FindForm(), "Ghi chú duyệt", "Ghi chú (không bắt buộc):", null, required: false);
                await Api.ApproveCancellationAsync(c.Id, refund.Value, note);
                Ui.Info(FindForm(), $"Đã hủy booking {c.BookingCode}, hoàn {refund.Value:#,0} đ. Phiếu hoàn tiền xử lý ở module Hóa đơn (GĐ3).");
            }
            else
            {
                var note = Ui.Prompt(FindForm(), "Từ chối hủy", "Lý do từ chối:");
                if (note is null) return;
                await Api.RejectCancellationAsync(c.Id, note);
                Ui.Info(FindForm(), $"Đã từ chối. Booking {c.BookingCode} trở lại trạng thái Đã xác nhận.");
            }
            await ReloadSafeAsync();
        }
        catch (ApiException ex) { Ui.Error(FindForm(), ex.Message); }
    }

    private async Task ReloadSafeAsync()
    {
        try { await LoadDataAsync(); }
        catch (ApiException ex) { Ui.Error(FindForm(), ex.Message); }
    }
}
