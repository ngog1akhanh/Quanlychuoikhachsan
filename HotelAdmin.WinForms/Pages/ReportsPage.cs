using System.Text;
using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Module Báo cáo (mục 18): doanh thu, đặt phòng & công suất, dịch vụ, khách hàng, nhập–xuất–tồn, vận hành trong ngày.
/// Chọn khoảng ngày trên thanh công cụ rồi bấm "Xem"; "Xuất CSV" lưu các bảng của tab đang mở (mở được bằng Excel).
/// </summary>
public sealed partial class ReportsPage : ModulePage
{
    private readonly DateTimePicker fromDate = Inputs.Date(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
    private readonly DateTimePicker toDate = Inputs.Date(DateTime.Today);
    private readonly Lookups lookups;

    public ReportsPage(ApiClient api, UserSession session) : base(api, session)
    {
        lookups = new Lookups(api);
        fromDate.Width = toDate.Width = 155;
        var view = new FlatButton { Text = "Xem", Size = new Size(70, 36) };
        view.Click += async (_, _) => await ReloadCurrentAsync();
        var export = new FlatButton { Text = "Xuất CSV", Size = new Size(100, 36), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border };
        export.Click += (_, _) => ExportCsv();
        Toolbar.Controls.AddRange([Inputs.Caption("Từ", AppTheme.SmallBold, AppTheme.TextSoft), fromDate,
            Inputs.Caption("đến", AppTheme.SmallBold, AppTheme.TextSoft), toDate, view, export]);
        foreach (Control c in Toolbar.Controls) c.Margin = new Padding(0, c is Label ? 12 : 4, 8, 0);

        AddTab("Doanh thu", Revenue());
        AddTab("HĐ theo thời gian", InvoicesByPeriod());   // Thống kê hóa đơn: ReportsPage.InvoiceStats.cs
        AddTab("HĐ theo nhân viên", InvoicesByStaff());
        AddTab("Sản phẩm theo năm", ProductsByYear());
        AddTab("Đặt phòng & công suất", Bookings());
        AddTab("Dịch vụ", Services());
        AddTab("Khách hàng", Customers());
        AddTab("Nhập – xuất – tồn", Inventory());
        AddTab("Vận hành hôm nay", Operations());
        BuildTabs();
        Toolbar.SetFlowBreak(Toolbar.Controls.OfType<SegmentedTabs>().Single(), true);
    }

    public override string Title => "Báo cáo";

    private DateOnly From => DateOnly.FromDateTime(fromDate.Value);
    private DateOnly To => DateOnly.FromDateTime(toDate.Value);

    private Task<JsonElement> Report(string name, params (string, object?)[] extra)
        => Api.GetJsonAsync(ApiClient.Url($"v1/reports/{name}", [("from", From), ("to", To), .. extra]));

    private bool ValidRange()
    {
        if (To < From) { Ui.Error(FindForm(), "Ngày kết thúc phải sau ngày bắt đầu."); return false; }
        if (To.DayNumber - From.DayNumber > 366) { Ui.Error(FindForm(), "Khoảng báo cáo tối đa 366 ngày."); return false; }
        return true;
    }

    // =====================================================================
    private ReportView Revenue()
    {
        var r = new ReportView(2);
        var rooms = r.Kpi("Doanh thu phòng", Glyph.Bed, true);
        var services = r.Kpi("Doanh thu dịch vụ", Glyph.Shop);
        var total = r.Kpi("Tổng doanh thu", Glyph.Money);
        var collected = r.Kpi("Tiền đã thu", Glyph.Card);
        var chart = new BarChart { Dock = DockStyle.Fill, Format = v => AppTheme.Money((decimal)v) };
        r.Card("Doanh thu theo thời gian (hóa đơn đã phát hành)", chart, 0, 0, 2);
        var methods = r.Grid("Tiền thu theo phương thức", 0, 1, ("Phương thức", 120, false), ("Số tiền", 100, true));
        var types = r.Grid("Tiền phòng theo loại phòng", 1, 1, ("Loại phòng", 120, false), ("Doanh thu", 100, true));
        r.Loader = async () =>
        {
            if (!ValidRange()) return;
            var data = await Report("revenue-breakdown");
            var daily = J.Items(data, "daily").Select(e => (Day: J.Day(e, "date") ?? DateTime.Today, Rooms: J.Dec(e, "rooms"), Services: J.Dec(e, "services"))).ToList();
            var roomSum = daily.Sum(d => d.Rooms);
            var serviceSum = daily.Sum(d => d.Services);
            rooms.Value = AppTheme.Money(roomSum);
            services.Value = AppTheme.Money(serviceSum);
            total.Value = AppTheme.Money(roomSum + serviceSum);
            rooms.Note = $"{J.DateText(From.ToDateTime(TimeOnly.MinValue))} – {J.DateText(To.ToDateTime(TimeOnly.MinValue))}";

            // > 45 ngày: gộp theo tháng cho dễ đọc
            var groups = daily.Count > 45
                ? daily.GroupBy(d => d.Day.ToString("MM/yyyy")).Select(g => (Label: g.Key, Rooms: g.Sum(x => x.Rooms), Services: g.Sum(x => x.Services))).ToList()
                : daily.Select(d => (Label: d.Day.ToString("dd/MM"), d.Rooms, d.Services)).ToList();
            chart.SetData(groups.Select(g => g.Label).ToArray(),
                new BarSeries("Phòng", AppTheme.LimeDark, groups.Select(g => (double)g.Rooms).ToArray()),
                new BarSeries("Dịch vụ", AppTheme.MintDark, groups.Select(g => (double)g.Services).ToArray()));

            var methodNames = await lookups.NamesAsync(Lookups.PaymentMethodsUrl, "ten_phuong_thuc");
            var paid = J.Items(data, "payment_methods");
            collected.Value = AppTheme.Money(paid.Sum(e => J.Dec(e, "collected")));
            collected.Note = "theo thời điểm thu tiền";
            ReportView.Fill(methods, paid.Select(e => new object?[] { methodNames.GetValueOrDefault(J.Long(e, "payment_method_id"), "#" + J.Long(e, "payment_method_id")), J.Money(J.Dec(e, "collected")) }));

            Dictionary<long, string> typeNames;
            try { typeNames = await lookups.NamesAsync(Lookups.RoomTypesUrl, "name"); }
            catch (ApiException) { typeNames = []; }
            ReportView.Fill(types, J.Items(data, "room_types").Select(e => new object?[] { typeNames.GetValueOrDefault(J.Long(e, "room_type_id"), "#" + J.Long(e, "room_type_id")), J.Money(J.Dec(e, "revenue")) }));
        };
        return r;
    }

    // =====================================================================
    private ReportView Bookings()
    {
        var r = new ReportView(2);
        var occupancy = r.Kpi("Công suất phòng", Glyph.Rooms, true);
        var adr = r.Kpi("ADR (giá TB/đêm)", Glyph.Money);
        var revpar = r.Kpi("RevPAR", Glyph.Chart);
        var deposits = r.Kpi("Cọc thu / hoàn", Glyph.Card);
        var donut = new DonutChart { Dock = DockStyle.Fill, CenterTitle = "Booking" };
        r.Card("Booking tạo trong kỳ theo trạng thái", donut, 0, 0);
        var statuses = r.Grid("Chi tiết theo trạng thái", 1, 0, ("Trạng thái", 110, false), ("Số booking", 60, true), ("Giá trị", 90, true));
        var cancels = r.Grid("Yêu cầu hủy trong kỳ", 0, 1, ("Trạng thái", 110, false), ("Số yêu cầu", 60, true), ("Hoàn đã duyệt", 90, true));
        var occ = r.Grid("Công suất", 1, 1, ("Chỉ tiêu", 120, false), ("Giá trị", 90, true));
        r.Loader = async () =>
        {
            if (!ValidRange()) return;
            var data = await Report("bookings");
            var rows = J.Items(data, "bookings");
            var palette = new[] { AppTheme.LimeDark, AppTheme.MintDark, ColorTranslator.FromHtml("#9DB8F2"), ColorTranslator.FromHtml("#F5D86B"),
                ColorTranslator.FromHtml("#F2A3A3"), AppTheme.Sage, ColorTranslator.FromHtml("#D9DDD6") };
            donut.SetData(rows.Select((e, i) => new ChartSegment(Labels.Of(J.Str(e, "status")), J.Int(e, "count"), palette[i % palette.Length])));
            donut.CenterValue = rows.Sum(e => J.Int(e, "count")).ToString();
            ReportView.Fill(statuses, rows.Select(e => new object?[] { Labels.Of(J.Str(e, "status")), J.Int(e, "count"), J.Money(J.Dec(e, "total")) }));
            ReportView.Fill(cancels, J.Items(data, "cancellations").Select(e => new object?[] { Labels.Of(J.Str(e, "status")), J.Int(e, "count"), J.Money(J.Dec(e, "approved_refund")) }));
            deposits.Value = AppTheme.Money(J.Dec(data, "deposits"));
            deposits.Note = "hoàn: " + AppTheme.Money(J.Dec(data, "refunds"));

            var o = await Api.GetOccupancyAsync(From, To);
            occupancy.Value = $"{o.Occupancy:0.#}%";
            occupancy.Note = $"{o.OccupiedRoomNights}/{o.AvailableRoomNights} đêm phòng";
            adr.Value = AppTheme.Money(o.Adr);
            revpar.Value = AppTheme.Money(o.RevPar);
            ReportView.Fill(occ, [
                ["Đêm phòng có khách", o.OccupiedRoomNights.ToString("#,0")],
                ["Đêm phòng khả dụng", o.AvailableRoomNights.ToString("#,0")],
                ["Công suất", $"{o.Occupancy:0.##}%"],
                ["ADR", J.MoneyVnd(o.Adr)],
                ["RevPAR", J.MoneyVnd(o.RevPar)]]);
        };
        return r;
    }

    // =====================================================================
    private ReportView Services()
    {
        var r = new ReportView(2);
        var amount = r.Kpi("Doanh thu dịch vụ", Glyph.Shop, true);
        var requests = r.Kpi("Lượt phục vụ", Glyph.Check);
        var top = r.Kpi("Dịch vụ dẫn đầu", Glyph.Chart);
        var chart = new BarChart { Dock = DockStyle.Fill, Format = v => AppTheme.Money((decimal)v) };
        r.Card("Top dịch vụ theo doanh thu", chart, 0, 0, 2);
        var grid = r.Grid("Chi tiết dịch vụ đã phục vụ", 0, 1, ("Dịch vụ", 140, false), ("Lượt", 50, true), ("Số lượng", 60, true), ("Doanh thu", 90, true));
        r.SpanLast(2);
        r.Loader = async () =>
        {
            if (!ValidRange()) return;
            var rows = J.Items(await Report("services"));
            amount.Value = AppTheme.Money(rows.Sum(e => J.Dec(e, "amount")));
            requests.Value = rows.Sum(e => J.Int(e, "requests")).ToString("#,0");
            top.Value = rows.Count == 0 ? "—" : J.Str(rows[0], "name");
            var best = rows.Take(10).ToList();
            chart.SetData(best.Select(e => J.Str(e, "name")).ToArray(),
                new BarSeries("Doanh thu", AppTheme.LimeDark, best.Select(e => (double)J.Dec(e, "amount")).ToArray()));
            ReportView.Fill(grid, rows.Select(e => new object?[] { J.Str(e, "name"), J.Int(e, "requests"), J.Qty(J.Dec(e, "quantity")), J.Money(J.Dec(e, "amount")) }));
        };
        return r;
    }

    // =====================================================================
    private ReportView Customers()
    {
        var r = new ReportView(2);
        var guests = r.Kpi("Khách lưu trú", Glyph.People, true);
        var returning = r.Kpi("Khách quay lại", Glyph.Refresh);
        var profiles = r.Kpi("Hồ sơ mới", Glyph.User);
        var rating = r.Kpi("Điểm đánh giá TB", Glyph.Check);
        var donut = new DonutChart { Dock = DockStyle.Fill, CenterTitle = "Khách" };
        r.Card("Khách mới và khách quay lại", donut, 0, 0);
        var grid = r.Grid("Chỉ tiêu khách hàng", 1, 0, ("Chỉ tiêu", 140, false), ("Giá trị", 80, true));
        r.Loader = async () =>
        {
            if (!ValidRange()) return;
            var d = await Report("customers");
            guests.Value = J.Int(d, "guests").ToString("#,0");
            returning.Value = J.Int(d, "returning").ToString("#,0");
            returning.Note = J.Int(d, "guests") == 0 ? "" : $"{J.Int(d, "returning") * 100.0 / J.Int(d, "guests"):0.#}% số khách";
            profiles.Value = J.Int(d, "new_profiles").ToString("#,0");
            rating.Value = J.Int(d, "reviews") == 0 ? "—" : $"{J.Dec(d, "average_rating"):0.0}/5";
            rating.Note = $"{J.Int(d, "reviews")} đánh giá";
            donut.SetData([new ChartSegment("Lần đầu", J.Int(d, "first_time"), AppTheme.LimeDark), new ChartSegment("Quay lại", J.Int(d, "returning"), AppTheme.MintDark)]);
            donut.CenterValue = J.Int(d, "guests").ToString();
            ReportView.Fill(grid, [
                ["Khách có lưu trú trong kỳ", J.Int(d, "guests")],
                ["Khách lần đầu", J.Int(d, "first_time")],
                ["Khách quay lại", J.Int(d, "returning")],
                ["Hồ sơ khách tạo mới", J.Int(d, "new_profiles")],
                ["Số đánh giá", J.Int(d, "reviews")],
                ["Điểm trung bình", $"{J.Dec(d, "average_rating"):0.00}"]]);
        };
        return r;
    }

    // =====================================================================
    private ReportView Inventory()
    {
        var r = new ReportView(1);
        var warehouse = Inputs.Combo([]);
        warehouse.Width = 220;
        r.AddFilter(Inputs.Caption("Kho", AppTheme.SmallBold, AppTheme.TextSoft));
        r.AddFilter(warehouse);
        warehouse.SelectionChangeCommitted += async (_, _) => await r.ReloadAsync();
        var items = r.Kpi("Mặt hàng", Glyph.Package, true);
        var below = r.Kpi("Dưới mức tối thiểu", Glyph.Warning);
        var received = r.Kpi("Tổng nhập trong kỳ", Glyph.ArrowIn);
        var issued = r.Kpi("Tổng xuất trong kỳ", Glyph.ArrowOut);
        var grid = r.Grid("Nhập – xuất – tồn theo mặt hàng", 0, 0, ("Mã hàng", 70, false), ("Tên hàng", 150, false), ("Tồn đầu", 60, true),
            ("Nhập", 60, true), ("Xuất", 60, true), ("Tồn cuối", 60, true), ("Tối thiểu", 60, true), ("Cảnh báo", 70, false));
        r.Loader = async () =>
        {
            if (!ValidRange()) return;
            if (warehouse.Items.Count == 0)
            {
                foreach (var (text, value) in await lookups.WarehousesAsync(false)) warehouse.Items.Add(new Choice<long>(text, value));
                if (warehouse.Items.Count > 0) warehouse.SelectedIndex = 0;
            }
            if (warehouse.SelectedItem is not Choice<long> w) { Ui.Info(FindForm(), "Chưa có kho nào."); return; }
            var rows = J.Items(await Report("inventory", ("warehouseId", w.Value)));
            items.Value = rows.Count.ToString();
            below.Value = rows.Count(e => J.Bool(e, "below_minimum")).ToString();
            received.Value = J.Qty(rows.Sum(e => J.Dec(e, "received")));
            issued.Value = J.Qty(rows.Sum(e => J.Dec(e, "issued")));
            ReportView.Fill(grid, rows.OrderByDescending(e => J.Bool(e, "below_minimum")).Select(e => new object?[]
            {
                J.Str(e, "ma_hang"), J.Str(e, "ten_hang"), J.Qty(J.Dec(e, "opening")), J.Qty(J.Dec(e, "received")), J.Qty(J.Dec(e, "issued")),
                J.Qty(J.Dec(e, "closing")), J.Qty(J.Dec(e, "minimum")), J.Bool(e, "below_minimum") ? "⚠ Cần nhập" : ""
            }));
        };
        return r;
    }

    // =====================================================================
    private ReportView Operations()
    {
        var r = new ReportView(2);
        var arrivals = r.Kpi("Khách đến (chưa nhận)", Glyph.ArrowIn, true);
        var departures = r.Kpi("Khách đi (chưa trả)", Glyph.ArrowOut);
        var cancels = r.Kpi("Yêu cầu hủy chờ duyệt", Glyph.Warning);
        var ready = r.Kpi("Phòng sẵn sàng", Glyph.Rooms);
        var donut = new DonutChart { Dock = DockStyle.Fill, CenterTitle = "Phòng" };
        r.Card("Tình trạng phòng hiện tại", donut, 0, 0);
        var grid = r.Grid("Số phòng theo trạng thái", 1, 0, ("Trạng thái", 140, false), ("Số phòng", 70, true));
        r.Loader = async () =>
        {
            var d = await Api.GetJsonAsync(ApiClient.Url("v1/reports/operations", ("date", DateOnly.FromDateTime(DateTime.Today))));
            arrivals.Value = J.Int(d, "arrivals").ToString();
            departures.Value = J.Int(d, "departures").ToString();
            cancels.Value = J.Int(d, "pending_cancellations").ToString();
            var rooms = J.Items(d, "rooms");
            ready.Value = rooms.Where(e => J.Str(e, "status") == "SAN_SANG").Sum(e => J.Int(e, "count")).ToString();
            ready.Note = $"/{rooms.Sum(e => J.Int(e, "count"))} phòng";
            donut.SetData(rooms.Select(e => new ChartSegment(Labels.Of(J.Str(e, "status")), J.Int(e, "count"), StatusStyles.RoomChartColor(J.Str(e, "status")))));
            donut.CenterValue = rooms.Sum(e => J.Int(e, "count")).ToString();
            ReportView.Fill(grid, rooms.Select(e => new object?[] { Labels.Of(J.Str(e, "status")), J.Int(e, "count") }));
        };
        return r;
    }

    // =====================================================================
    private void ExportCsv()
    {
        if (CurrentView is not ReportView view || view.Grids.Count == 0) return;
        using var save = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = view.ExportTitle is null ? $"bao-cao-{From:yyyyMMdd}-{To:yyyyMMdd}.csv" : $"thong-ke-hoa-don-{DateTime.Now:yyyyMMdd-HHmm}.csv"
        };
        if (save.ShowDialog(FindForm()) != DialogResult.OK) return;
        var sb = new StringBuilder();
        sb.AppendLine(view.ExportTitle ?? $"Báo cáo từ {From:dd/MM/yyyy} đến {To:dd/MM/yyyy}");
        foreach (var (title, grid) in view.Grids)
        {
            sb.AppendLine().AppendLine(Csv(title));
            sb.AppendLine(string.Join(",", grid.Columns.Cast<DataGridViewColumn>().Select(c => Csv(c.HeaderText))));
            foreach (DataGridViewRow row in grid.Rows)
                sb.AppendLine(string.Join(",", row.Cells.Cast<DataGridViewCell>().Select(c => Csv(c.Value?.ToString() ?? ""))));
        }
        File.WriteAllText(save.FileName, sb.ToString(), new UTF8Encoding(true));
        Ui.Info(FindForm(), "Đã lưu " + save.FileName);
    }

    private static string Csv(string value) => value.Contains(',') || value.Contains('"') || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}

/// <summary>Bố cục một tab báo cáo: hàng thẻ KPI phía trên, lưới thẻ biểu đồ/bảng phía dưới.</summary>
public sealed class ReportView : UserControl, IReloadable
{
    private readonly FlowLayoutPanel filters = new() { Dock = DockStyle.Top, Height = 0, WrapContents = true, AutoScroll = true, BackColor = AppTheme.Background };
    private readonly TableLayoutPanel kpis = new() { Dock = DockStyle.Top, Height = 140, RowCount = 1, BackColor = AppTheme.Background };
    private readonly TableLayoutPanel body = new() { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
    private Control? last;

    public ReportView(int columns)
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.Background;
        body.ColumnCount = columns;
        for (var i = 0; i < columns; i++) body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        Controls.Add(body);
        Controls.Add(kpis);
        Controls.Add(filters);
    }

    public Func<Task>? Loader { get; set; }

    /// <summary>Dòng tiêu đề khi xuất CSV; null = dùng khoảng ngày trên thanh công cụ.</summary>
    public string? ExportTitle { get; set; }

    public List<(string Title, ModernGrid Grid)> Grids { get; } = [];

    public async Task ReloadAsync()
    {
        if (Loader is null) return;
        UseWaitCursor = true;
        try { await Loader(); }
        catch (ApiException ex) { if (!ex.IsUnauthorized) Ui.Error(FindForm(), ex.Message); }
        finally { UseWaitCursor = false; }
    }

    public void AddFilter(Control control)
    {
        filters.Height = 88;
        control.Margin = new Padding(0, control is Label ? 12 : 6, 8, 0);
        filters.Controls.Add(control);
    }

    public StatCard Kpi(string title, string glyph, bool highlight = false)
    {
        var card = new StatCard(title, glyph, highlight) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 12) };
        kpis.ColumnCount = kpis.Controls.Count + 1;
        kpis.ColumnStyles.Clear();
        for (var i = 0; i < kpis.ColumnCount; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / kpis.ColumnCount));
        kpis.Controls.Add(card, kpis.ColumnCount - 1, 0);
        return card;
    }

    /// <summary>Thẻ có tiêu đề chứa control (biểu đồ, bảng) ở ô (col, row).</summary>
    public RoundedPanel Card(string title, Control content, int col, int row, int colSpan = 1)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 12), Padding = new Padding(18, 12, 18, 14) };
        content.Dock = DockStyle.Fill;
        card.Controls.Add(content);
        card.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 32, Font = AppTheme.CardTitle, ForeColor = AppTheme.Text, BackColor = Color.Transparent });
        while (body.RowCount <= row)
        {
            body.RowCount++;
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        }
        body.Controls.Add(card, col, row);
        if (colSpan > 1) body.SetColumnSpan(card, colSpan);
        last = card;
        return card;
    }

    /// <summary>Thẻ chứa bảng; cột: (tiêu đề, độ rộng, căn phải).</summary>
    public ModernGrid Grid(string title, int col, int row, params (string Header, float Weight, bool Right)[] cols)
    {
        var grid = new ModernGrid { AutoGenerateColumns = false };
        grid.RowTemplate.Height = 36;
        for (var i = 0; i < cols.Length; i++)
            grid.AddText("c" + i, cols[i].Header, cols[i].Weight, null,
                cols[i].Right ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft);
        Card(title, grid, col, row);
        Grids.Add((title, grid));
        return grid;
    }

    /// <summary>Cho thẻ vừa thêm chiếm nhiều cột.</summary>
    public void SpanLast(int columns)
    {
        if (last is not null) body.SetColumnSpan(last, columns);
    }

    public static void Fill(ModernGrid grid, IEnumerable<object?[]> rows)
    {
        grid.Rows.Clear();
        foreach (var row in rows) grid.Rows.Add(row.Select(v => v ?? "").ToArray());
    }
}
