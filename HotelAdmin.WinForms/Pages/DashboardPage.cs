using System.Globalization;
using HotelAdmin.Controls;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Dashboard tổng quan (mục 18.1): khách đến/đi hôm nay, tình trạng phòng, doanh thu hôm nay,
/// công suất phòng, biểu đồ doanh thu 6 tháng, lượt nhận phòng 7 ngày, booking theo trạng thái, danh sách booking.
/// </summary>
public sealed class DashboardPage : PageBase
{
    private static readonly CultureInfo Vi = new("vi-VN");

    // KPI
    private readonly StatCard cardArrivals = new("Khách đến hôm nay", Glyph.ArrowIn, highlight: true);
    private readonly StatCard cardDepartures = new("Khách trả phòng hôm nay", Glyph.ArrowOut);
    private readonly StatCard cardRevenue = new("Doanh thu hôm nay", Glyph.Money);
    private readonly StatCard cardOccupancy = new("Công suất phòng hôm nay", Glyph.Rooms);

    // Biểu đồ
    private readonly SegmentBar roomBar = new() { Dock = DockStyle.Fill };
    private readonly AreaChart revenueChart = new() { Dock = DockStyle.Fill };
    private readonly BarChart arrivalsChart = new() { Dock = DockStyle.Fill };
    private readonly DonutChart statusDonut = new() { Dock = DockStyle.Fill, CenterTitle = "booking" };
    private readonly MetricList monthMetrics = new() { Dock = DockStyle.Fill };
    private readonly TimelineList todayList = new() { Dock = DockStyle.Fill };

    // Danh sách booking
    private readonly ModernGrid grid = new() { Dock = DockStyle.Fill };
    private readonly RoundedTextBox search = new(Glyph.Search) { Width = 220, Height = 36, Margin = new Padding(8, 2, 0, 0), PlaceholderText = "Tìm mã booking..." };
    private readonly ComboBox statusFilter = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        Width = 150,
        Font = AppTheme.Body,
        Margin = new Padding(8, 8, 0, 0)
    };
    private readonly Label todayTitle = new();
    private readonly Label monthTitle = new();

    private List<BookingListItem> bookings = [];
    private CancellationTokenSource? loading;

    public DashboardPage(ApiClient api, UserSession session) : base(api, session)
    {
        Padding = new Padding(16, 0, 16, 16);
        revenueChart.Format = v => AppTheme.Money((decimal)v);
        arrivalsChart.Format = v => v.ToString("0");
        BuildUi();
    }

    public override string Title => "Tổng quan";

    // ======================================================================
    // Bố cục
    // ======================================================================
    private void BuildUi()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.Background };
        Controls.Add(scroll);

        // Lưới ngoài: trái 75% (nội dung chính) | phải 25% (cột phụ)
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 144 + 330 + 330 + 470,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = AppTheme.Background,
            Margin = new Padding(0)
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        scroll.Controls.Add(outer);

        // ---- Cột trái ----
        var left = NewTable(1, [144, 330, 330, 470]);
        outer.Controls.Add(left, 0, 0);

        var kpiRow = NewTable(4, [100], horizontal: true);
        foreach (var c in new[] { cardArrivals, cardDepartures, cardRevenue, cardOccupancy })
        {
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(8);
            kpiRow.Controls.Add(c);
        }
        left.Controls.Add(kpiRow, 0, 0);

        var chartRow1 = NewTable(2, [100], horizontal: true, weights: [36, 64]);
        var (roomCard, roomBody, _) = CreateCard("Tình trạng phòng");
        roomBody.Controls.Add(roomBar);
        chartRow1.Controls.Add(roomCard, 0, 0);
        var (revCard, revBody, revHeader) = CreateCard("Doanh thu 6 tháng gần nhất");
        revHeader.Controls.Add(Chip("6 tháng"));
        revBody.Controls.Add(revenueChart);
        chartRow1.Controls.Add(revCard, 1, 0);
        left.Controls.Add(chartRow1, 0, 1);

        var chartRow2 = NewTable(2, [100], horizontal: true, weights: [55, 45]);
        var (arrCard, arrBody, arrHeader) = CreateCard("Lượt nhận phòng ±3 ngày");
        arrHeader.Controls.Add(Chip("7 ngày"));
        arrBody.Controls.Add(arrivalsChart);
        chartRow2.Controls.Add(arrCard, 0, 0);
        var (donutCard, donutBody, _) = CreateCard("Booking theo trạng thái");
        donutBody.Controls.Add(statusDonut);
        chartRow2.Controls.Add(donutCard, 1, 0);
        left.Controls.Add(chartRow2, 0, 2);

        var (listCard, listBody, listHeader) = CreateCard("Danh sách đặt phòng");
        statusFilter.Items.Add("Tất cả trạng thái");
        foreach (var code in new[] { "CHO_COC", "BOOKED", "DANG_O", "DA_TRA", "DA_HUY", "NO_SHOW" })
            statusFilter.Items.Add(new StatusOption(code));
        statusFilter.SelectedIndex = 0;
        statusFilter.SelectedIndexChanged += (_, _) => BindGrid();
        search.Inner.TextChanged += (_, _) => BindGrid();
        listHeader.Controls.Add(statusFilter);
        listHeader.Controls.Add(search);
        listHeader.Height = 44;
        BuildGridColumns();
        listBody.Controls.Add(grid);
        left.Controls.Add(listCard, 0, 3);

        // ---- Cột phải ----
        var right = NewTable(1, [474, 800]);
        outer.Controls.Add(right, 1, 0);

        var (monthCard, monthBody, _) = CreateCard("Hiệu quả tháng này");
        monthTitle.Dock = DockStyle.Top;
        monthTitle.Height = 70;
        monthTitle.Font = AppTheme.Kpi;
        monthTitle.ForeColor = AppTheme.Text;
        monthTitle.BackColor = Color.Transparent;
        monthTitle.TextAlign = ContentAlignment.MiddleLeft;
        monthBody.Controls.Add(monthMetrics);
        monthBody.Controls.Add(monthTitle);
        right.Controls.Add(monthCard, 0, 0);

        var (todayCard, todayBody, _) = CreateCard("Lịch hôm nay");
        todayTitle.Dock = DockStyle.Top;
        todayTitle.Height = 30;
        todayTitle.Font = AppTheme.Small;
        todayTitle.ForeColor = AppTheme.TextMuted;
        todayTitle.BackColor = Color.Transparent;
        todayBody.Controls.Add(todayList);
        todayBody.Controls.Add(todayTitle);
        right.Controls.Add(todayCard, 0, 1);

        // Chiều cao lưới ngoài = tổng 2 cột lớn hơn
        outer.Height = Math.Max(144 + 330 + 330 + 470, 474 + 800);
    }

    private static TableLayoutPanel NewTable(int count, int[] sizes, bool horizontal = false, int[]? weights = null)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = AppTheme.Background,
            ColumnCount = horizontal ? count : 1,
            RowCount = horizontal ? 1 : count
        };
        if (horizontal)
        {
            for (var i = 0; i < count; i++)
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, weights is null ? 100f / count : weights[i]));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        }
        else
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var s in sizes) table.RowStyles.Add(new RowStyle(SizeType.Absolute, s));
        }
        return table;
    }

    private static PillLabel Chip(string text)
    {
        var chip = new PillLabel { Text = text, FillColor = AppTheme.Lime, ForeColor = AppTheme.Text, Height = 28, Margin = new Padding(0, 4, 0, 0) };
        chip.AutoFit();
        return chip;
    }

    private void BuildGridColumns()
    {
        grid.AutoGenerateColumns = false;
        grid.AddText(nameof(BookingRow.BookingCode), "Mã booking", 120);
        grid.AddText(nameof(BookingRow.CheckIn), "Nhận phòng", 110, "dd/MM/yyyy HH:mm");
        grid.AddText(nameof(BookingRow.CheckOut), "Trả phòng", 110, "dd/MM/yyyy HH:mm");
        grid.AddText(nameof(BookingRow.Nights), "Số đêm", 60, null, DataGridViewContentAlignment.MiddleCenter);
        grid.AddText(nameof(BookingRow.Total), "Tổng tiền", 100, "#,0 đ", DataGridViewContentAlignment.MiddleRight);
        grid.AddStatus(nameof(BookingRow.Status), "Trạng thái", v => StatusStyles.ForBooking(v as string), 110);
    }

    // ======================================================================
    // Nạp dữ liệu
    // ======================================================================
    public override async Task LoadDataAsync()
    {
        loading?.Cancel();
        loading = new CancellationTokenSource();
        var ct = loading.Token;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // Gọi song song; mỗi khối tự xử lý lỗi riêng (ví dụ không có quyền ROOM_READ).
        var bookingsTask = Try(() => Api.GetAllBookingsAsync(ct: ct));
        var roomsTask = Try(() => Api.GetRoomsAsync(ct));
        var todayRevenueTask = Try(() => Api.GetRevenueAsync(today, today, ct));
        var yesterdayRevenueTask = Try(() => Api.GetRevenueAsync(today.AddDays(-1), today.AddDays(-1), ct));
        var occupancyTask = Try(() => Api.GetOccupancyAsync(today, today, ct));
        var monthTask = Try(() => Api.GetDashboardAsync(monthStart, today, ct));
        var months = Enumerable.Range(0, 6).Select(i => monthStart.AddMonths(i - 5)).ToArray();
        var revenueTasks = months
            .Select(m => Try(() => Api.GetRevenueAsync(m, Min(m.AddMonths(1).AddDays(-1), today), ct)))
            .ToArray();

        await Task.WhenAll(bookingsTask, roomsTask, todayRevenueTask, yesterdayRevenueTask, occupancyTask, monthTask, Task.WhenAll(revenueTasks));
        if (ct.IsCancellationRequested || IsDisposed) return;

        ApplyBookings(bookingsTask.Result, today);
        ApplyRooms(roomsTask.Result);
        ApplyRevenueToday(todayRevenueTask.Result, yesterdayRevenueTask.Result);
        ApplyOccupancy(occupancyTask.Result);
        ApplyMonth(monthTask.Result, today);
        ApplyRevenueChart(months, revenueTasks.Select(t => t.Result).ToArray());
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static async Task<Result<T>> Try<T>(Func<Task<T>> call)
    {
        try { return new Result<T>(await call(), null); }
        catch (ApiException ex) { return new Result<T>(default, ex); }
        catch (OperationCanceledException) { return new Result<T>(default, new ApiException(System.Net.HttpStatusCode.RequestTimeout, "Đã hủy")); }
    }

    private sealed record Result<T>(T? Value, ApiException? Error)
    {
        public string ErrorText => Error is null ? "" : Error.IsForbidden ? "Không có quyền xem dữ liệu này" : Error.Message;
    }

    private void ApplyBookings(Result<List<BookingListItem>> result, DateOnly today)
    {
        bookings = result.Value ?? [];
        var active = new[] { "HOLD", "CHO_COC", "BOOKED", "DANG_O", "DA_TRA" };

        // KPI khách đến / đi
        var arrivals = bookings.Where(b => DateOnly.FromDateTime(b.CheckIn) == today && active.Contains(b.Status)).ToList();
        var departures = bookings.Where(b => DateOnly.FromDateTime(b.CheckOut) == today && b.Status is "DANG_O" or "DA_TRA").ToList();
        cardArrivals.Value = result.Error is null ? arrivals.Count.ToString() : "—";
        cardArrivals.Note = result.Error?.Message is { } e1 ? Short(e1) : $"{arrivals.Count(b => b.Status == "DANG_O")} đã nhận phòng";
        cardArrivals.SetPill(null);
        cardDepartures.Value = result.Error is null ? departures.Count.ToString() : "—";
        cardDepartures.Note = result.Error is null ? $"{departures.Count(b => b.Status == "DA_TRA")} đã trả phòng" : "";

        // Biểu đồ lượt nhận phòng ±3 ngày
        var days = Enumerable.Range(-3, 7).Select(today.AddDays).ToArray();
        var booked = days.Select(d => (double)bookings.Count(b => DateOnly.FromDateTime(b.CheckIn) == d && b.Status is not ("DA_HUY" or "NO_SHOW"))).ToArray();
        var cancelled = days.Select(d => (double)bookings.Count(b => DateOnly.FromDateTime(b.CheckIn) == d && b.Status is "DA_HUY" or "NO_SHOW")).ToArray();
        arrivalsChart.EmptyText = result.Error is null ? "Chưa có dữ liệu" : result.ErrorText;
        if (result.Error is null)
            arrivalsChart.SetData(days.Select(d => d == today ? "Hôm nay" : d.ToString("dd/MM")).ToArray(),
                new BarSeries("Đặt phòng", AppTheme.MintDark, booked),
                new BarSeries("Hủy / không đến", AppTheme.Lime, cancelled));
        else
            arrivalsChart.SetData([]);

        // Donut trạng thái
        statusDonut.EmptyText = arrivalsChart.EmptyText;
        statusDonut.CenterValue = bookings.Count.ToString("#,0");
        var donutColors = new Dictionary<string, Color>
        {
            ["BOOKED"] = AppTheme.MintDark,
            ["DANG_O"] = AppTheme.LimeDark,
            ["DA_TRA"] = AppTheme.Sage,
            ["CHO_COC"] = AppTheme.Lime,
            ["HOLD"] = AppTheme.Lime,
            ["DA_HUY"] = ColorTranslator.FromHtml("#F2B8B5"),
            ["NO_SHOW"] = ColorTranslator.FromHtml("#E8D9A8"),
        };
        statusDonut.SetData(bookings.GroupBy(b => b.Status)
            .OrderByDescending(g => g.Count())
            .Select(g => new ChartSegment(StatusStyles.ForBooking(g.Key).Label, g.Count(),
                donutColors.TryGetValue(g.Key, out var c) ? c : AppTheme.NeutralBg)));

        // Lịch hôm nay: khách đến + khách trả phòng
        var dateText = today.ToString("dddd, dd/MM/yyyy", Vi);
        todayTitle.Text = $"{dateText} · {arrivals.Count} đến · {departures.Count} đi";
        todayList.EmptyText = result.Error is null ? "Hôm nay không có lịch đến/đi" : result.ErrorText;
        var items = arrivals.OrderBy(b => b.CheckIn)
            .Select(b => new TimelineItem($"Nhận phòng · {b.CheckIn:HH:mm} · {StatusStyles.ForBooking(b.Status).Label}",
                $"{b.BookingCode}  ·  {Nights(b)} đêm", b.Status == "DANG_O" ? AppTheme.MintSoft : AppTheme.Mint))
            .Concat(departures.OrderBy(b => b.CheckOut)
            .Select(b => new TimelineItem($"Trả phòng · {b.CheckOut:HH:mm} · {StatusStyles.ForBooking(b.Status).Label}",
                $"{b.BookingCode}  ·  {AppTheme.MoneyFull(b.Total)}", b.Status == "DA_TRA" ? AppTheme.NeutralBg : AppTheme.Lime)));
        todayList.SetData(items);

        grid.Tag = result.ErrorText;
        BindGrid();
    }

    private void ApplyRooms(Result<List<RoomResponse>> result)
    {
        roomBar.EmptyText = result.ErrorText;
        if (result.Value is null) { roomBar.SetData([]); return; }
        var order = new[] { "DANG_O", "SAN_SANG", "CAN_DON", "DANG_DON", "BAO_TRI" };
        var groups = result.Value.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
        roomBar.SetData(order.Select(code => new ChartSegment(
            StatusStyles.ForRoom(code).Label, groups.GetValueOrDefault(code), StatusStyles.RoomChartColor(code))));
    }

    private void ApplyRevenueToday(Result<RevenueReportResponse> today, Result<RevenueReportResponse> yesterday)
    {
        if (today.Value is null)
        {
            cardRevenue.Value = "—";
            cardRevenue.Note = Short(today.ErrorText);
            return;
        }
        cardRevenue.Value = AppTheme.Money(today.Value.TotalRevenue);
        var prev = yesterday.Value?.TotalRevenue ?? 0;
        if (prev > 0)
        {
            var change = (today.Value.TotalRevenue - prev) / prev * 100;
            cardRevenue.SetPill($"{(change >= 0 ? "▲" : "▼")} {Math.Abs(change):0.#}%", change >= 0);
            cardRevenue.Note = "so với hôm qua";
        }
        else
        {
            cardRevenue.SetPill(null);
            cardRevenue.Note = $"Phòng {AppTheme.Money(today.Value.RoomRevenue)} · DV {AppTheme.Money(today.Value.ServiceRevenue)}";
        }
    }

    private void ApplyOccupancy(Result<OccupancyReportResponse> result)
    {
        if (result.Value is null)
        {
            cardOccupancy.Value = "—";
            cardOccupancy.Note = Short(result.ErrorText);
            return;
        }
        cardOccupancy.Value = $"{result.Value.Occupancy:0.#}%";
        cardOccupancy.SetPill(null);
        cardOccupancy.Note = $"{result.Value.OccupiedRoomNights}/{result.Value.AvailableRoomNights} phòng có khách";
    }

    private void ApplyMonth(Result<DashboardResponse> result, DateOnly today)
    {
        if (result.Value is not { } d)
        {
            monthTitle.Text = "—";
            monthMetrics.EmptyText = result.ErrorText;
            monthMetrics.SetData([]);
            return;
        }
        var total = d.RoomRevenue + d.ServiceRevenue + d.Compensation;
        monthTitle.Text = AppTheme.Money(total);
        monthMetrics.SetData(
        [
            new MetricItem($"Công suất phòng (tháng {today.Month})", $"{d.Occupancy:0.#}%", (double)d.Occupancy / 100),
            new MetricItem("Giá phòng bình quân (ADR)", AppTheme.MoneyFull(d.Adr)),
            new MetricItem("RevPAR", AppTheme.MoneyFull(d.RevPar)),
            new MetricItem("Doanh thu tiền phòng", AppTheme.Money(d.RoomRevenue), total == 0 ? 0 : (double)(d.RoomRevenue / total)),
            new MetricItem("Doanh thu dịch vụ", AppTheme.Money(d.ServiceRevenue), total == 0 ? 0 : (double)(d.ServiceRevenue / total)),
            new MetricItem("Booking mới trong tháng", d.Bookings.ToString("#,0")),
            new MetricItem("Hóa đơn chưa thu", AppTheme.Money(d.OutstandingInvoices)),
        ]);
    }

    private void ApplyRevenueChart(DateOnly[] months, Result<RevenueReportResponse>[] results)
    {
        var failed = results.FirstOrDefault(r => r.Error is not null);
        if (failed is not null)
        {
            revenueChart.EmptyText = failed.ErrorText;
            revenueChart.SetData([], []);
            return;
        }
        revenueChart.SetData(
            months.Select(m => $"Th{m.Month}/{m.Year % 100:00}").ToArray(),
            results.Select(r => (double)(r.Value?.TotalRevenue ?? 0)).ToArray());
    }

    // ======================================================================
    // Lưới booking
    // ======================================================================
    private void BindGrid()
    {
        IEnumerable<BookingListItem> rows = bookings.OrderByDescending(b => b.Id);
        var q = search.Text.Trim();
        if (q.Length > 0) rows = rows.Where(b => b.BookingCode.Contains(q, StringComparison.OrdinalIgnoreCase));
        if (statusFilter.SelectedItem is StatusOption opt) rows = rows.Where(b => b.Status == opt.Code);
        grid.DataSource = rows.Take(200).Select(b => new BookingRow(b.BookingCode, b.CheckIn, b.CheckOut, Nights(b), b.Total, b.Status)).ToList();
    }

    private static int Nights(BookingListItem b) => Math.Max(1, (b.CheckOut.Date - b.CheckIn.Date).Days);

    private static string Short(string text) => text.Length > 60 ? text[..57] + "..." : text;

    /// <summary>Dòng hiển thị trên lưới (DataPropertyName khớp tên property).</summary>
    public sealed record BookingRow(string BookingCode, DateTime CheckIn, DateTime CheckOut, int Nights, decimal Total, string Status);

    private sealed record StatusOption(string Code)
    {
        public override string ToString() => StatusStyles.ForBooking(Code).Label;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) loading?.Cancel();
        base.Dispose(disposing);
    }
}
