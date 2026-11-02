using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Thống kê hóa đơn (3 tab):
///  - "HĐ theo thời gian": tổng tiền hóa đơn theo tháng / quý / năm; chọn "Năm" với nhiều năm để so sánh các năm.
///  - "HĐ theo nhân viên": tổng tiền hóa đơn theo nhân viên lập, theo quý / năm.
///  - "Sản phẩm theo năm": doanh thu từng dịch vụ và từng loại phòng theo năm.
/// Các tab này chọn theo NĂM (bộ lọc riêng trong tab), không dùng ô Từ/đến ngày trên thanh công cụ.
/// API: GET /v1/reports/invoice-stats/{periods|staff|products}
/// </summary>
public sealed partial class ReportsPage
{
    private static readonly Color[] SeriesPalette =
    [
        AppTheme.LimeDark, AppTheme.MintDark, ColorTranslator.FromHtml("#9DB8F2"), ColorTranslator.FromHtml("#F5D86B"),
        ColorTranslator.FromHtml("#F2A3A3"), AppTheme.Sage, ColorTranslator.FromHtml("#C3A6E8"), ColorTranslator.FromHtml("#D9DDD6")
    ];

    /// <summary>Thêm bộ lọc "Từ năm … đến năm …" vào tab; đổi giá trị là tải lại.</summary>
    private static (NumericUpDown From, NumericUpDown To) YearFilter(ReportView r, int yearsBack)
    {
        var now = DateTime.Today.Year;
        var from = Inputs.Number(now - yearsBack, 2000, 2100);
        var to = Inputs.Number(now, 2000, 2100);
        from.ThousandsSeparator = to.ThousandsSeparator = false;
        from.Width = to.Width = 80;
        r.AddFilter(Inputs.Caption("Từ năm", AppTheme.SmallBold, AppTheme.TextSoft));
        r.AddFilter(from);
        r.AddFilter(Inputs.Caption("đến năm", AppTheme.SmallBold, AppTheme.TextSoft));
        r.AddFilter(to);
        from.ValueChanged += async (_, _) => await r.ReloadAsync();
        to.ValueChanged += async (_, _) => await r.ReloadAsync();
        return (from, to);
    }

    private static ComboBox GroupFilter(ReportView r, params Choice<string>[] choices)
    {
        var combo = Inputs.Combo(choices);
        combo.Width = 190;
        r.AddFilter(Inputs.Caption("Gộp theo", AppTheme.SmallBold, AppTheme.TextSoft));
        r.AddFilter(combo);
        combo.SelectionChangeCommitted += async (_, _) => await r.ReloadAsync();
        return combo;
    }

    private bool ValidYears(int from, int to)
    {
        if (to < from) { Ui.Error(FindForm(), "Năm kết thúc phải lớn hơn hoặc bằng năm bắt đầu."); return false; }
        if (to - from + 1 > 10) { Ui.Error(FindForm(), "Chỉ thống kê tối đa 10 năm."); return false; }
        return true;
    }

    private Task<JsonElement> Stats(string name, params (string, object?)[] query)
        => Api.GetJsonAsync(ApiClient.Url($"v1/reports/invoice-stats/{name}", query));

    // =====================================================================
    // 1. Tổng tiền hóa đơn theo tháng / quý / năm / các năm
    private ReportView InvoicesByPeriod()
    {
        var r = new ReportView(2);
        var (fromYear, toYear) = YearFilter(r, 0);
        var groupBy = GroupFilter(r,
            new Choice<string>("Tháng", "month"),
            new Choice<string>("Quý", "quarter"),
            new Choice<string>("Năm (so sánh các năm)", "year"));
        var total = r.Kpi("Tổng tiền hóa đơn", Glyph.Money, true);
        var rooms = r.Kpi("Hóa đơn phòng", Glyph.Bed);
        var services = r.Kpi("Hóa đơn dịch vụ", Glyph.Shop);
        var count = r.Kpi("Số hóa đơn", Glyph.Card);
        var chart = new BarChart { Dock = DockStyle.Fill, Format = v => AppTheme.Money((decimal)v) };
        r.Card("Tổng tiền hóa đơn theo kỳ (đã lập, không tính hóa đơn nháp/đã hủy)", chart, 0, 0, 2);
        var grid = r.Grid("Chi tiết theo kỳ", 0, 1, ("Kỳ", 90, false), ("Số HĐ", 60, true), ("HĐ phòng", 110, true), ("HĐ dịch vụ", 110, true), ("Tổng tiền", 120, true));
        r.SpanLast(2);
        r.Loader = async () =>
        {
            int from = (int)fromYear.Value, to = (int)toYear.Value;
            if (!ValidYears(from, to)) return;
            var choice = (Choice<string>)groupBy.SelectedItem!;
            var data = await Stats("periods", ("fromYear", from), ("toYear", to), ("groupBy", choice.Value));
            var periods = J.Items(data, "periods");
            total.Value = AppTheme.Money(J.Dec(data, "total"));
            total.Note = from == to ? $"năm {from}" : $"{from} – {to}";
            rooms.Value = AppTheme.Money(J.Dec(data, "room_total"));
            services.Value = AppTheme.Money(J.Dec(data, "service_total"));
            count.Value = J.Int(data, "invoice_count").ToString("#,0");
            count.Note = "gộp theo " + choice.Text.ToLowerInvariant();
            chart.SetData(periods.Select(e => J.Str(e, "label")).ToArray(),
                new BarSeries("Phòng", AppTheme.LimeDark, periods.Select(e => (double)J.Dec(e, "room_total")).ToArray()),
                new BarSeries("Dịch vụ", AppTheme.MintDark, periods.Select(e => (double)J.Dec(e, "service_total")).ToArray()));
            ReportView.Fill(grid, periods.Select(e => new object?[]
            {
                J.Str(e, "label"), J.Int(e, "invoice_count"), J.Money(J.Dec(e, "room_total")), J.Money(J.Dec(e, "service_total")), J.Money(J.Dec(e, "total"))
            }).Append(new object?[] { "Tổng cộng", J.Int(data, "invoice_count"), J.Money(J.Dec(data, "room_total")), J.Money(J.Dec(data, "service_total")), J.Money(J.Dec(data, "total")) }));
            r.ExportTitle = $"Tổng tiền hóa đơn theo {choice.Text.ToLowerInvariant()}, {from}–{to}";
        };
        return r;
    }

    // =====================================================================
    // 2. Tổng tiền hóa đơn theo nhân viên theo quý / năm
    private ReportView InvoicesByStaff()
    {
        var r = new ReportView(2);
        var (fromYear, toYear) = YearFilter(r, 0);
        var groupBy = GroupFilter(r, new Choice<string>("Quý", "quarter"), new Choice<string>("Năm", "year"));
        var total = r.Kpi("Tổng tiền hóa đơn", Glyph.Money, true);
        var people = r.Kpi("Nhân viên lập HĐ", Glyph.People);
        var top = r.Kpi("Nhân viên dẫn đầu", Glyph.User);
        var chart = new BarChart { Dock = DockStyle.Fill, Format = v => AppTheme.Money((decimal)v) };
        r.Card("Tổng tiền hóa đơn của từng nhân viên theo kỳ", chart, 0, 0, 2);
        var totals = r.Grid("Tổng theo nhân viên", 0, 1, ("Mã NV", 60, false), ("Nhân viên", 140, false), ("Số HĐ", 50, true), ("Tổng tiền", 100, true));
        var detail = r.Grid("Chi tiết theo kỳ", 1, 1, ("Kỳ", 60, false), ("Nhân viên", 130, false), ("Số HĐ", 45, true), ("HĐ phòng", 90, true), ("HĐ dịch vụ", 90, true), ("Tổng tiền", 100, true));
        r.Loader = async () =>
        {
            int from = (int)fromYear.Value, to = (int)toYear.Value;
            if (!ValidYears(from, to)) return;
            var choice = (Choice<string>)groupBy.SelectedItem!;
            var data = await Stats("staff", ("fromYear", from), ("toYear", to), ("groupBy", choice.Value));
            var labels = J.Items(data, "periods").Select(e => e.GetString() ?? "").ToArray();
            var staff = J.Items(data, "staff");
            var rows = J.Items(data, "rows");

            total.Value = AppTheme.Money(staff.Sum(e => J.Dec(e, "total")));
            total.Note = from == to ? $"năm {from}" : $"{from} – {to}";
            people.Value = staff.Count.ToString();
            top.Value = staff.Count == 0 ? "—" : J.Str(staff[0], "employee_name");
            top.Note = staff.Count == 0 ? "" : AppTheme.Money(J.Dec(staff[0], "total"));

            // Mỗi nhân viên một cột màu trong từng kỳ
            var series = staff.Select((s, i) =>
            {
                var id = J.Str(s, "employee_id");
                var byLabel = rows.Where(e => J.Str(e, "employee_id") == id).ToDictionary(e => J.Str(e, "label"), e => (double)J.Dec(e, "total"));
                return new BarSeries(J.Str(s, "employee_name"), SeriesPalette[i % SeriesPalette.Length], labels.Select(l => byLabel.GetValueOrDefault(l)).ToArray());
            }).ToArray();
            chart.SetData(labels, series);

            ReportView.Fill(totals, staff.Select(e => new object?[] { J.Str(e, "employee_code"), J.Str(e, "employee_name"), J.Int(e, "invoice_count"), J.Money(J.Dec(e, "total")) }));
            ReportView.Fill(detail, rows.Select(e => new object?[]
            {
                J.Str(e, "label"), J.Str(e, "employee_name"), J.Int(e, "invoice_count"), J.Money(J.Dec(e, "room_total")), J.Money(J.Dec(e, "service_total")), J.Money(J.Dec(e, "total"))
            }));
            r.ExportTitle = $"Tổng tiền hóa đơn theo nhân viên, theo {choice.Text.ToLowerInvariant()}, {from}–{to}";
        };
        return r;
    }

    // =====================================================================
    // 3. Tổng tiền về sản phẩm (dịch vụ + loại phòng) theo năm
    private ReportView ProductsByYear()
    {
        var r = new ReportView(2);
        var (fromYear, toYear) = YearFilter(r, 2);
        var kind = GroupFilter(r,
            new Choice<string>("Dịch vụ + loại phòng", ""),
            new Choice<string>("Chỉ dịch vụ", "DICH_VU"),
            new Choice<string>("Chỉ loại phòng", "LOAI_PHONG"));
        var serviceKpi = r.Kpi("Doanh thu dịch vụ", Glyph.Shop, true);
        var roomKpi = r.Kpi("Tiền phòng (chưa VAT)", Glyph.Bed);
        var topKpi = r.Kpi("Sản phẩm dẫn đầu", Glyph.Chart);
        var chart = new BarChart { Dock = DockStyle.Fill, Format = v => AppTheme.Money((decimal)v) };
        r.Card("Top 6 sản phẩm: doanh thu qua các năm", chart, 0, 0, 2);
        var grid = r.Grid("Doanh thu theo sản phẩm và năm", 0, 1, ("Năm", 50, true), ("Loại", 80, false), ("Sản phẩm", 160, false), ("Số lượng", 70, true), ("ĐVT", 50, false), ("Doanh thu", 110, true));
        r.SpanLast(2);
        r.Loader = async () =>
        {
            int from = (int)fromYear.Value, to = (int)toYear.Value;
            if (!ValidYears(from, to)) return;
            var filter = ((Choice<string>)kind.SelectedItem!).Value;
            var data = await Stats("products", ("fromYear", from), ("toYear", to));
            var years = J.Items(data, "years").Select(e => e.GetInt32()).ToArray();
            var rows = J.Items(data, "rows").Where(e => filter == "" || J.Str(e, "kind") == filter).ToList();

            serviceKpi.Value = AppTheme.Money(J.Dec(data, "service_total"));
            serviceKpi.Note = from == to ? $"năm {from}" : $"{from} – {to}";
            roomKpi.Value = AppTheme.Money(J.Dec(data, "room_total"));
            roomKpi.Note = "dòng tiền phòng theo loại phòng";

            var products = rows.GroupBy(e => (Kind: J.Str(e, "kind"), Id: J.Long(e, "product_id")))
                .Select(g => (g.Key, Name: J.Str(g.First(), "name"), Total: g.Sum(e => J.Dec(e, "amount")), ByYear: g.ToDictionary(e => J.Int(e, "year"), e => (double)J.Dec(e, "amount"))))
                .OrderByDescending(x => x.Total).ToList();
            topKpi.Value = products.Count == 0 ? "—" : products[0].Name;
            topKpi.Note = products.Count == 0 ? "" : AppTheme.Money(products[0].Total);

            chart.SetData(years.Select(y => y.ToString()).ToArray(),
                products.Take(6).Select((p, i) => new BarSeries(p.Name, SeriesPalette[i % SeriesPalette.Length], years.Select(y => p.ByYear.GetValueOrDefault(y)).ToArray())).ToArray());

            ReportView.Fill(grid, rows.Select(e => new object?[]
            {
                J.Int(e, "year"), J.Str(e, "kind") == "DICH_VU" ? "Dịch vụ" : "Loại phòng", J.Str(e, "name"),
                J.Qty(J.Dec(e, "quantity")), J.Str(e, "unit"), J.Money(J.Dec(e, "amount"))
            }));
            r.ExportTitle = $"Doanh thu theo sản phẩm theo năm, {from}–{to}";
        };
        return r;
    }
}
