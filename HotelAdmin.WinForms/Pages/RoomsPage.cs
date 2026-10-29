using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Trang Phòng (mục 9), 3 tab:
///  • Sơ đồ phòng: ô phòng theo tầng, tô màu theo trạng thái, lọc nhanh theo trạng thái; bấm ô để sửa/đổi trạng thái.
///  • Loại phòng: thẻ loại phòng (giá, sức chứa, số phòng), thêm/sửa.
///  • Lịch phòng: lưới phòng × ngày từ khoa_lich_phong; bấm đúp ô để mở booking.
/// </summary>
public sealed class RoomsPage : PageBase
{
    private static readonly string[] StatusOrder = ["SAN_SANG", "DANG_O", "CAN_DON", "DANG_DON", "BAO_TRI", "NGUNG_SU_DUNG"];

    private readonly SegmentedTabs tabs = new("Sơ đồ phòng", "Loại phòng", "Lịch phòng");
    private readonly Panel content = new() { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
    private readonly FlowLayoutPanel toolbar = new();

    // Sơ đồ
    private readonly Panel boardPanel = new() { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
    private readonly FlowLayoutPanel filterBar = new();
    private readonly FlowLayoutPanel board = new();
    private List<RoomBoardItem> boardItems = [];
    private string? statusFilter;

    // Loại phòng
    private readonly FlowLayoutPanel typesPanel = new();
    private List<RoomTypeSummary> types = [];

    // Lịch phòng
    private readonly Panel calendarPanel = new() { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
    private readonly DateTimePicker calFrom = Inputs.Date(DateTime.Today);
    private readonly ComboBox calDays = Inputs.Combo([new Choice<int>("7 ngày", 7), new Choice<int>("14 ngày", 14), new Choice<int>("30 ngày", 30)], 1);
    private readonly DataGridView calendar = new();
    private RoomCalendar? calendarData;

    public RoomsPage(ApiClient api, UserSession session) : base(api, session)
    {
        Padding = new Padding(24, 0, 24, 16);
        BuildToolbar();
        BuildBoard();
        BuildTypes();
        BuildCalendar();
        Controls.Add(content);
        Controls.Add(toolbar);
        tabs.SelectedIndexChanged += async (_, _) => await ReloadSafeAsync();
        ShowTab();
    }

    public override string Title => "Phòng";

    // =====================================================================
    // Bố cục
    // =====================================================================
    private void BuildToolbar()
    {
        toolbar.Dock = DockStyle.Top;
        toolbar.Height = 56;
        toolbar.WrapContents = false;
        toolbar.BackColor = AppTheme.Background;
        toolbar.Padding = new Padding(0, 4, 0, 8);
        toolbar.Controls.Add(tabs);
    }

    private void RebuildToolbarButtons()
    {
        while (toolbar.Controls.Count > 1) toolbar.Controls.RemoveAt(1);
        void Add(string text, string glyph, Func<Task> onClick)
        {
            var b = new FlatButton { Text = text, IconGlyph = glyph, Size = new Size(TextRenderer.MeasureText(text, AppTheme.BodyBold).Width + 50, 40), Margin = new Padding(12, 0, 0, 0) };
            b.Click += async (_, _) => await onClick();
            toolbar.Controls.Add(b);
        }
        switch (tabs.SelectedIndex)
        {
            case 0 when Session.HasPermission("ROOM_WRITE"):
                Add("Thêm phòng", "", AddRoomAsync);
                break;
            case 1 when Session.HasPermission("ROOM_TYPE_WRITE"):
                Add("Thêm loại phòng", "", () => EditTypeAsync(null));
                break;
        }
    }

    private void BuildBoard()
    {
        filterBar.Dock = DockStyle.Top;
        filterBar.Height = 48;
        filterBar.WrapContents = false;
        filterBar.BackColor = AppTheme.Background;

        board.Dock = DockStyle.Fill;
        board.AutoScroll = true;
        board.BackColor = AppTheme.Background;
        board.Padding = new Padding(0, 4, 0, 16);

        boardPanel.Controls.Add(board);
        boardPanel.Controls.Add(filterBar);
    }

    private void BuildTypes()
    {
        typesPanel.Dock = DockStyle.Fill;
        typesPanel.AutoScroll = true;
        typesPanel.BackColor = AppTheme.Background;
        typesPanel.Padding = new Padding(0, 4, 0, 16);
    }

    private void BuildCalendar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, WrapContents = false, BackColor = AppTheme.Background, Padding = new Padding(0, 6, 0, 6) };
        calFrom.Width = 140;
        calDays.Width = 110;
        var prev = SmallButton("", "Lùi");
        var next = SmallButton("", "Tới");
        var today = new FlatButton { Text = "Hôm nay", Size = new Size(100, 34), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border, Margin = new Padding(8, 0, 0, 0) };
        prev.Click += async (_, _) => { calFrom.Value = calFrom.Value.AddDays(-SelectedDays()); await ReloadSafeAsync(); };
        next.Click += async (_, _) => { calFrom.Value = calFrom.Value.AddDays(SelectedDays()); await ReloadSafeAsync(); };
        today.Click += async (_, _) => { calFrom.Value = DateTime.Today; await ReloadSafeAsync(); };
        calFrom.CloseUp += async (_, _) => await ReloadSafeAsync();
        calDays.SelectedIndexChanged += async (_, _) => await ReloadSafeAsync();
        bar.Controls.AddRange([Inputs.Caption("Từ ngày", AppTheme.SmallBold, AppTheme.TextSoft), calFrom, calDays, prev, next, today]);
        foreach (Control c in bar.Controls) c.Margin = new Padding(c.Margin.Left, 4, 8, 0);

        var legend = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34, BackColor = AppTheme.Background, Padding = new Padding(0, 6, 0, 0) };
        foreach (var code in new[] { "HOLD", "BOOKED", "DANG_O", "DA_TRA" })
        {
            var pill = new PillLabel { Margin = new Padding(0, 0, 8, 0) };
            pill.ApplyStatus(StatusStyles.ForBooking(code));
            legend.Controls.Add(pill);
        }
        legend.Controls.Add(Inputs.Caption("Bấm đúp vào ô để mở booking.", AppTheme.Small, AppTheme.TextMuted));

        calendar.Dock = DockStyle.Fill;
        calendar.ReadOnly = true;
        calendar.AllowUserToAddRows = false;
        calendar.AllowUserToDeleteRows = false;
        calendar.AllowUserToResizeRows = false;
        calendar.RowHeadersVisible = false;
        calendar.BackgroundColor = AppTheme.Surface;
        calendar.BorderStyle = BorderStyle.None;
        calendar.GridColor = AppTheme.Border;
        calendar.EnableHeadersVisualStyles = false;
        calendar.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.MintSoft;
        calendar.ColumnHeadersDefaultCellStyle.Font = AppTheme.SmallBold;
        calendar.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        calendar.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        calendar.ColumnHeadersHeight = 44;
        calendar.RowTemplate.Height = 34;
        calendar.DefaultCellStyle.Font = AppTheme.Small;
        calendar.SelectionMode = DataGridViewSelectionMode.CellSelect;
        calendar.DefaultCellStyle.SelectionBackColor = AppTheme.LimeHover;
        calendar.DefaultCellStyle.SelectionForeColor = AppTheme.Text;
        calendar.CellDoubleClick += async (_, e) => await OpenCalendarCellAsync(e.RowIndex, e.ColumnIndex);

        var card = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        card.Controls.Add(calendar);
        calendarPanel.Controls.Add(card);
        calendarPanel.Controls.Add(legend);
        calendarPanel.Controls.Add(bar);
    }

    private static FlatButton SmallButton(string glyph, string tip)
    {
        var b = new FlatButton { IconGlyph = glyph, Size = new Size(40, 34), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border, Margin = new Padding(8, 0, 0, 0) };
        new ToolTip().SetToolTip(b, tip);
        return b;
    }

    private int SelectedDays() => calDays.SelectedItem is Choice<int> c ? c.Value : 14;

    private void ShowTab()
    {
        content.Controls.Clear();
        content.Controls.Add(tabs.SelectedIndex switch
        {
            0 => boardPanel,
            1 => typesPanel,
            _ => calendarPanel
        });
        RebuildToolbarButtons();
    }

    // =====================================================================
    // Nạp dữ liệu
    // =====================================================================
    public override async Task LoadDataAsync()
    {
        ShowTab();
        switch (tabs.SelectedIndex)
        {
            case 0: await LoadBoardAsync(); break;
            case 1: await LoadTypesAsync(); break;
            default: await LoadCalendarAsync(); break;
        }
    }

    private async Task LoadBoardAsync()
    {
        boardItems = await Api.GetRoomBoardAsync();
        RenderFilters();
        RenderBoard();
    }

    private void RenderFilters()
    {
        filterBar.Controls.Clear();
        AddFilter(null, $"Tất cả  {boardItems.Count}");
        foreach (var code in StatusOrder)
        {
            var count = boardItems.Count(r => r.Status == code);
            if (count == 0 && code is "NGUNG_SU_DUNG" or "DANG_DON") continue;
            AddFilter(code, $"{StatusStyles.ForRoom(code).Label}  {count}");
        }
    }

    private void AddFilter(string? code, string text)
    {
        var selected = statusFilter == code;
        var style = code is null ? new StatusStyle("", AppTheme.Lime, AppTheme.Text) : StatusStyles.ForRoom(code);
        var b = new FlatButton
        {
            Text = text,
            Size = new Size(TextRenderer.MeasureText(text, AppTheme.BodyBold).Width + 28, 34),
            FillColor = selected ? style.Back : AppTheme.Surface,
            HoverColor = style.Back,
            BorderColor = selected ? Color.Transparent : AppTheme.Border,
            ForeColor = selected ? style.Fore : AppTheme.TextSoft,
            Radius = 17,
            Margin = new Padding(0, 4, 8, 0)
        };
        b.Click += (_, _) =>
        {
            statusFilter = code;
            RenderFilters();
            RenderBoard();
        };
        filterBar.Controls.Add(b);
    }

    private void RenderBoard()
    {
        board.SuspendLayout();
        board.Controls.Clear();
        var rooms = boardItems.Where(r => statusFilter is null || r.Status == statusFilter).ToList();
        if (rooms.Count == 0)
            board.Controls.Add(Inputs.Caption("Không có phòng nào ở trạng thái này.", AppTheme.Body, AppTheme.TextMuted));

        foreach (var floor in rooms.GroupBy(r => r.Floor ?? "?"))
        {
            var header = new Label
            {
                Text = int.TryParse(floor.Key, out _) ? $"Tầng {floor.Key}" : floor.Key,
                Font = AppTheme.CardTitle,
                ForeColor = AppTheme.Text,
                AutoSize = false,
                Size = new Size(300, 36),
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, 6, 0, 6)
            };
            board.Controls.Add(header);
            board.SetFlowBreak(header, true);

            RoomTile? last = null;
            foreach (var r in floor)
            {
                var tile = new RoomTile
                {
                    RoomNumber = r.RoomNumber,
                    TypeName = r.RoomTypeName,
                    StatusCode = r.Status,
                    Line1 = r.CurrentGuest ?? (r.ArrivalGuest is null ? null : $"Đến: {r.ArrivalGuest}"),
                    Line2 = r.CurrentCheckOut is { } co ? $"Trả {co:dd/MM HH:mm}"
                        : r.ArrivalTime is { } at ? $"Hôm nay {at:HH:mm} · {r.ArrivalBookingCode}"
                        : r.Note
                };
                tile.Click += async (_, _) => await OpenRoomAsync(r);
                board.Controls.Add(tile);
                last = tile;
            }
            if (last is not null) board.SetFlowBreak(last, true);
        }
        board.ResumeLayout();
    }

    private async Task LoadTypesAsync()
    {
        types = await Api.GetRoomTypeSummariesAsync();
        typesPanel.SuspendLayout();
        typesPanel.Controls.Clear();
        foreach (var t in types) typesPanel.Controls.Add(TypeCard(t));
        typesPanel.ResumeLayout();
    }

    private Control TypeCard(RoomTypeSummary t)
    {
        var card = new RoundedPanel { Size = new Size(330, 230), Margin = new Padding(0, 0, 16, 16), Padding = new Padding(20) };
        var status = new PillLabel { Location = new Point(220, 20) };
        status.ApplyStatus(t.Status == "HOAT_DONG"
            ? new StatusStyle("Đang kinh doanh", AppTheme.SuccessBg, AppTheme.SuccessFg)
            : new StatusStyle("Ngừng", AppTheme.NeutralBg, AppTheme.NeutralFg));
        status.Left = card.Width - status.Width - 20;
        card.Controls.Add(CardLabel(t.Name, AppTheme.CardTitle, AppTheme.Text, 20, 18));
        card.Controls.Add(CardLabel(t.Code, AppTheme.Small, AppTheme.TextMuted, 20, 46));
        card.Controls.Add(status);
        card.Controls.Add(CardLabel($"{t.BaseNightlyPrice:#,0} đ", AppTheme.KpiSmall, AppTheme.Text, 18, 70));
        card.Controls.Add(CardLabel("/ đêm (giá cơ bản)", AppTheme.Small, AppTheme.TextMuted, 20, 100));
        var area = t.AreaM2 is { } a ? $"{a:0.#} m²  ·  " : "";
        card.Controls.Add(CardLabel($"{area}{t.MaxAdults} người lớn, {t.MaxChildren} trẻ em", AppTheme.Body, AppTheme.TextSoft, 20, 126));
        card.Controls.Add(CardLabel($"{t.RoomCount} phòng  ·  {t.ReadyCount} sẵn sàng  ·  {t.OccupiedCount} đang ở", AppTheme.Small, AppTheme.TextSoft, 20, 150));
        var desc = CardLabel(t.Description ?? "", AppTheme.Small, AppTheme.TextMuted, 20, 172);
        desc.AutoSize = false;
        desc.Size = new Size(200, 36);
        desc.AutoEllipsis = true;
        card.Controls.Add(desc);
        if (Session.HasPermission("ROOM_TYPE_WRITE"))
        {
            var edit = new FlatButton { Text = "Sửa", Size = new Size(76, 34), Location = new Point(card.Width - 96, 176), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border };
            edit.Click += async (_, _) => await EditTypeAsync(t);
            card.Controls.Add(edit);
        }
        return card;
    }

    private static Label CardLabel(string text, Font font, Color color, int x, int y) => new()
    {
        Text = text,
        Font = font,
        ForeColor = color,
        AutoSize = true,
        Location = new Point(x, y),
        BackColor = Color.Transparent
    };

    private async Task LoadCalendarAsync()
    {
        var fromDate = DateOnly.FromDateTime(calFrom.Value);
        var toDate = fromDate.AddDays(SelectedDays() - 1);
        calendarData = await Api.GetRoomCalendarAsync(fromDate, toDate);
        RenderCalendar();
    }

    private void RenderCalendar()
    {
        if (calendarData is null) return;
        var data = calendarData;
        calendar.SuspendLayout();
        calendar.Columns.Clear();
        calendar.Rows.Clear();
        var roomCol = new DataGridViewTextBoxColumn { Name = "room", HeaderText = "Phòng", Width = 110, Frozen = true };
        roomCol.DefaultCellStyle.Font = AppTheme.BodyBold;
        calendar.Columns.Add(roomCol);
        var days = new List<DateOnly>();
        for (var d = data.From; d <= data.To; d = d.AddDays(1)) days.Add(d);
        var vi = new System.Globalization.CultureInfo("vi-VN");
        foreach (var d in days)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = d.ToString("yyyyMMdd"),
                HeaderText = $"{vi.DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek)}\n{d:dd/MM}",
                Width = days.Count > 14 ? 74 : 96,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (d == DateOnly.FromDateTime(DateTime.Today)) col.HeaderCell.Style.BackColor = AppTheme.Lime;
            calendar.Columns.Add(col);
        }
        calendar.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;

        var cells = data.Cells.ToLookup(c => c.RoomId);
        foreach (var room in data.Rooms)
        {
            var rowIndex = calendar.Rows.Add();
            var row = calendar.Rows[rowIndex];
            row.Cells[0].Value = $"{room.RoomNumber} · {room.RoomTypeCode}";
            row.Tag = room;
            foreach (var cell in cells[room.Id])
            {
                var colIndex = days.IndexOf(cell.Date) + 1;
                if (colIndex <= 0) continue;
                var target = row.Cells[colIndex];
                var lastName = cell.Guest.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? cell.Guest;
                target.Value = lastName;
                target.Tag = cell;
                var style = StatusStyles.ForBooking(cell.LockStatus == "HOLD" ? "HOLD" : cell.BookingStatus);
                target.Style.BackColor = style.Back;
                target.Style.ForeColor = style.Fore;
                target.ToolTipText = $"{cell.BookingCode} · {cell.Guest} · {style.Label}";
            }
        }
        calendar.ResumeLayout();
    }

    // =====================================================================
    // Thao tác
    // =====================================================================
    private async Task OpenRoomAsync(RoomBoardItem room)
    {
        if (types.Count == 0) types = await SafeAsync(ct => Api.GetRoomTypeSummariesAsync(ct), new List<RoomTypeSummary>());
        using var dlg = new DlgRoom(Api, Session, room, types);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) await ReloadSafeAsync();
    }

    private async Task AddRoomAsync()
    {
        if (types.Count == 0) types = await SafeAsync(ct => Api.GetRoomTypeSummariesAsync(ct), new List<RoomTypeSummary>());
        using var dlg = new DlgRoom(Api, Session, null, types);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) await ReloadSafeAsync();
    }

    private async Task EditTypeAsync(RoomTypeSummary? type)
    {
        using var dlg = new DlgRoomType(Api, type);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) await ReloadSafeAsync();
    }

    private async Task OpenCalendarCellAsync(int row, int col)
    {
        if (row < 0 || col <= 0) return;
        if (calendar.Rows[row].Cells[col].Tag is not RoomCalendarCell cell) return;
        using var dlg = new DlgBookingDetail(Api, Session, cell.BookingId);
        dlg.ShowDialog(FindForm());
        if (dlg.Changed) await ReloadSafeAsync();
    }

    private async Task ReloadSafeAsync()
    {
        try { await LoadDataAsync(); }
        catch (ApiException ex) { Ui.Error(FindForm(), ex.Message); }
    }

    private async Task<T> SafeAsync<T>(Func<CancellationToken, Task<T>> call, T fallback)
    {
        try { return await call(CancellationToken.None); }
        catch (ApiException ex) { Ui.Error(FindForm(), ex.Message); return fallback; }
    }
}
