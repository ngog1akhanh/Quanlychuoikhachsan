using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Module Giá phòng & chính sách (mục 10): chính sách giá theo loại phòng/khoảng ngày/thứ (khuyến mãi nằm trong mức giảm),
/// chính sách cọc, chính sách hủy, phụ thu nhận sớm/trả trễ. Giá đã chốt của booking cũ không bị đổi.
/// </summary>
public sealed class PricingPage : ModulePage
{
    private static readonly string[] Weekdays = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];
    private static readonly string[] Adjustments = ["GIA_CO_DINH", "GIAM_%", "TANG_%", "GIAM_TIEN", "TANG_TIEN"];
    private readonly Lookups lookups;

    public PricingPage(ApiClient api, UserSession session) : base(api, session)
    {
        lookups = new Lookups(api);
        AddTab("Chính sách giá", Pricing());
        AddTab("Chính sách cọc", Deposits());
        AddTab("Chính sách hủy", Cancellations());
        AddTab("Phụ thu sớm / trễ", Surcharges());
        BuildTabs();
    }

    public override string Title => "Giá phòng & chính sách";

    private IWin32Window? Owner => FindForm();

    // =====================================================================
    // Chính sách giá
    // =====================================================================
    private DataView Pricing()
    {
        Dictionary<long, string> typeNames = [];
        var view = new DataView(Session, "Chính sách giá theo mùa / ngày trong tuần")
        {
            Loader = async q =>
            {
                typeNames = await lookups.NamesAsync(Lookups.RoomTypesUrl, "code");
                return await Api.GetJsonAsync("v1/pricing-policies");
            }
        };
        view.AddSearch("Mã, tên chính sách…")
            .Columns(
                Col.Text("Mã", "ma_chinh_sach", 70),
                Col.Text("Tên chính sách", "ten_chinh_sach", 130),
                Col.Day("Từ ngày", "tu_ngay", 70),
                Col.Day("Đến ngày", "den_ngay", 70),
                Col.Text("Ưu tiên", "muc_uu_tien", 45),
                Col.Custom("Áp dụng", e => DaysText(e), 90),
                Col.Custom("Điều chỉnh", e => string.Join("; ", J.Items(e, "chi_tiet").Select(l =>
                    $"{typeNames.GetValueOrDefault(J.Long(l, "loai_phong_id"), "#" + J.Long(l, "loai_phong_id"))}: {Labels.Of(J.Str(l, "kieu_dieu_chinh"))} {J.Money(J.Dec(l, "gia_tri"))}")), 200),
                Col.Status("Trạng thái", "trang_thai", 75))
            .AddAction("Bật / tắt", r => TogglePricingAsync(r!.Value), "PRICING_WRITE")
            .AddAction("Sửa", r => EditPricingAsync(r!.Value), "PRICING_WRITE")
            .AddAction("Thêm chính sách", _ => EditPricingAsync(null), "PRICING_WRITE", needsRow: false, primary: true);
        view.DefaultAction = r => EditPricingAsync(r!.Value);
        return view;
    }

    private static List<int> Days(JsonElement e) => J.Items(e, "cac_thu_ap_dung").Select(d => J.Int(d, "thu")).OrderBy(d => d).ToList();

    private static string DaysText(JsonElement e)
    {
        var days = Days(e);
        return days.Count == 7 ? "Mọi ngày" : string.Join(", ", days.Where(d => d is >= 1 and <= 7).Select(d => Weekdays[d - 1]));
    }

    private async Task<bool> EditPricingAsync(JsonElement? row)
    {
        var types = await lookups.RoomTypesAsync();
        var dlg = new FormDialog(row is null ? "Thêm chính sách giá" : "Sửa chính sách giá", 720,
            "Khi nhiều chính sách cùng áp dụng một đêm, chính sách có mức ưu tiên cao hơn được dùng.");
        dlg.TextField("code", "Mã chính sách", FormDialog.S(row, "ma_chinh_sach"), required: true);
        dlg.TextField("name", "Tên chính sách", FormDialog.S(row, "ten_chinh_sach"), required: true);
        dlg.Date("from", "Từ ngày", FormDialog.Day(row, "tu_ngay") ?? DateTime.Today);
        dlg.Date("to", "Đến ngày", FormDialog.Day(row, "den_ngay") ?? DateTime.Today.AddMonths(1));
        dlg.Number("priority", "Mức ưu tiên", FormDialog.D(row, "muc_uu_tien", 1), 0, 1000);
        dlg.Codes("status", "Trạng thái", FormDialog.S(row, "trang_thai"), "HOAT_DONG", "NGUNG_HOAT_DONG");

        var selected = row is { } r0 ? Days(r0) : new List<int> { 1, 2, 3, 4, 5, 6, 7 };
        var dayBoxes = new FlowLayoutPanel { Height = 34, WrapContents = false, BackColor = Color.Transparent };
        var checks = Weekdays.Select((d, i) => new CheckBox { Text = d, Checked = selected.Contains(i + 1), AutoSize = true, Margin = new Padding(0, 6, 14, 0) }).ToList();
        dayBoxes.Controls.AddRange(checks.ToArray<Control>());
        dlg.Custom("weekdays", "Áp dụng vào các thứ", dayBoxes, () => checks.Select((c, i) => (c, i)).Where(x => x.c.Checked).Select(x => x.i + 1).ToList());

        var typeItems = types.Select(t => (t.Text, (object)t.Value)).ToList();
        var adjustItems = Adjustments.Select(a => (Labels.Of(a), (object)a)).ToList();
        var lines = new LinesEditor(170, true,
            new LineCol("room_type_id", "Loại phòng", LineKind.Combo, 120, typeItems),
            new LineCol("adjustment_type", "Cách điều chỉnh", LineKind.Combo, 100, adjustItems),
            new LineCol("value", "Giá trị (đ hoặc %)", LineKind.Number, 80));
        if (row is { } r1)
            foreach (var l in J.Items(r1, "chi_tiet"))
                lines.AddRow(new Dictionary<string, object?>
                {
                    ["room_type_id"] = J.Long(l, "loai_phong_id"), ["adjustment_type"] = J.Str(l, "kieu_dieu_chinh"), ["value"] = J.Dec(l, "gia_tri")
                });
        dlg.Custom("lines", "Điều chỉnh giá theo loại phòng (mỗi loại một dòng)", lines, () => lines.Lines());
        dlg.TextField("description", "Mô tả", FormDialog.S(row, "mo_ta"), span: 2, multiline: true);
        dlg.Validator = v =>
        {
            if (((List<int>)v["weekdays"]!).Count == 0) return "Chọn ít nhất một thứ áp dụng.";
            var l = (List<Dictionary<string, object?>>)v["lines"]!;
            if (l.Count == 0) return "Thêm ít nhất một dòng điều chỉnh giá.";
            if (l.Any(x => x["room_type_id"] is null || x["adjustment_type"] is null || x["value"] is null)) return "Mỗi dòng cần đủ loại phòng, cách điều chỉnh và giá trị.";
            if (string.CompareOrdinal((string)v["from"]!, (string)v["to"]!) > 0) return "Ngày kết thúc phải sau ngày bắt đầu.";
            return null;
        };
        dlg.OnSubmit = v => row is { } r ? Api.PutJsonAsync($"v1/pricing-policies/{J.Long(r, "id")}", v) : Api.PostJsonAsync("v1/pricing-policies", v);
        return dlg.ShowFor(Owner);
    }

    private async Task<bool> TogglePricingAsync(JsonElement row)
    {
        var next = J.Str(row, "trang_thai") == "HOAT_DONG" ? "NGUNG_HOAT_DONG" : "HOAT_DONG";
        if (!Ui.Confirm(Owner, $"{(next == "HOAT_DONG" ? "Bật" : "Tắt")} chính sách giá \"{J.Str(row, "ten_chinh_sach")}\"?")) return false;
        await Api.PutJsonAsync($"v1/pricing-policies/{J.Long(row, "id")}", new
        {
            code = J.Str(row, "ma_chinh_sach"), name = J.Str(row, "ten_chinh_sach"), from = J.Str(row, "tu_ngay"), to = J.Str(row, "den_ngay"),
            priority = J.Int(row, "muc_uu_tien"), weekdays = Days(row), description = J.StrOrNull(row, "mo_ta"), status = next,
            lines = J.Items(row, "chi_tiet").Select(l => new { room_type_id = J.Long(l, "loai_phong_id"), adjustment_type = J.Str(l, "kieu_dieu_chinh"), value = J.Dec(l, "gia_tri") }).ToList()
        });
        return true;
    }

    // =====================================================================
    // Chính sách cọc
    // =====================================================================
    private DataView Deposits()
    {
        var view = new DataView(Session, "Chính sách đặt cọc") { Loader = _ => Api.GetJsonAsync("v1/deposit-policies") };
        view.Columns(
                Col.Text("Tên chính sách", "ten_chinh_sach", 150),
                Col.Label("Cách tính", "kieu_tinh", 90),
                Col.Custom("Giá trị", e => J.Str(e, "kieu_tinh") == "PHAN_TRAM" ? $"{J.Dec(e, "gia_tri"):0.##}%" : J.Str(e, "kieu_tinh") == "DEM_DAU" ? "Tiền đêm đầu" : J.MoneyVnd(J.Dec(e, "gia_tri")), 80),
                Col.Day("Từ ngày", "tu_ngay", 70),
                Col.Day("Đến ngày", "den_ngay", 70),
                Col.Status("Trạng thái", "trang_thai", 75))
            .AddAction("Bật / tắt", r => ToggleStatusAsync(r!.Value, "deposit-policies"), "PRICING_WRITE")
            .AddAction("Thêm chính sách", _ => AddDepositAsync(), "PRICING_WRITE", needsRow: false, primary: true);
        return view;
    }

    private Task<bool> AddDepositAsync()
    {
        var dlg = new FormDialog("Thêm chính sách cọc", 560, "Áp dụng cho booking mới; không đổi số cọc của booking đã tạo.");
        dlg.TextField("name", "Tên chính sách", null, required: true, span: 2);
        dlg.Codes("type", "Cách tính", "PHAN_TRAM", "PHAN_TRAM", "DEM_DAU", "SO_TIEN_CO_DINH");
        dlg.Number("value", "Giá trị (% hoặc số tiền)", 30, 0, 1_000_000_000, 2);
        dlg.Date("from", "Áp dụng từ", null, nullable: true);
        dlg.Date("to", "Đến ngày", null, nullable: true);
        dlg.Codes("status", "Trạng thái", "HOAT_DONG", "HOAT_DONG", "NGUNG_HOAT_DONG");
        dlg.Validator = v => (string?)v["type"] == "PHAN_TRAM" && (decimal)v["value"]! > 100 ? "Phần trăm cọc tối đa 100." : null;
        dlg.OnSubmit = v => Api.PostJsonAsync("v1/deposit-policies", v);
        return Task.FromResult(dlg.ShowFor(Owner));
    }

    // =====================================================================
    // Chính sách hủy
    // =====================================================================
    private DataView Cancellations()
    {
        var view = new DataView(Session, "Chính sách hủy phòng") { Loader = _ => Api.GetJsonAsync("v1/app/cancellation-policies") };
        view.Columns(
                Col.Text("Tên chính sách", "name", 130),
                Col.Text("Mô tả", "description", 160),
                Col.Custom("Quy tắc hoàn tiền", e => string.Join("; ", J.Items(e, "rules").Select(r =>
                    $"≥{J.Int(r, "minimum_hours")}h: hoàn {J.Dec(r, "refund_percent"):0.##}%" + (J.Dec(r, "fixed_fee") > 0 ? $", phí {J.Money(J.Dec(r, "fixed_fee"))}" : ""))), 220),
                Col.Status("Trạng thái", "status", 75))
            .AddAction("Bật / tắt", r => ToggleStatusAsync(r!.Value, "cancellation-policies", "status"), "PRICING_WRITE")
            .AddAction("Thêm chính sách", _ => AddCancellationAsync(), "PRICING_WRITE", needsRow: false, primary: true);
        return view;
    }

    private Task<bool> AddCancellationAsync()
    {
        var dlg = new FormDialog("Thêm chính sách hủy", 620, "Mỗi quy tắc: hủy trước ít nhất N giờ so với giờ nhận phòng thì hoàn X% tiền cọc, trừ phí cố định.");
        dlg.TextField("name", "Tên chính sách", null, required: true, span: 2);
        dlg.TextField("description", "Mô tả", null, span: 2, multiline: true);
        var rules = new LinesEditor(160, true,
            new LineCol("minimum_hours", "Hủy trước tối thiểu (giờ)", LineKind.Number, 90),
            new LineCol("refund_percent", "Hoàn (%)", LineKind.Number, 70),
            new LineCol("fixed_fee", "Phí cố định (đ)", LineKind.Number, 80));
        rules.AddRow(new Dictionary<string, object?> { ["minimum_hours"] = 48m, ["refund_percent"] = 100m, ["fixed_fee"] = 0m });
        rules.AddRow(new Dictionary<string, object?> { ["minimum_hours"] = 24m, ["refund_percent"] = 50m, ["fixed_fee"] = 0m });
        rules.AddRow(new Dictionary<string, object?> { ["minimum_hours"] = 0m, ["refund_percent"] = 0m, ["fixed_fee"] = 0m });
        dlg.Custom("rules", "Quy tắc hoàn tiền", rules, () => rules.Lines().Select(l => new
        {
            minimum_hours = (int)((decimal?)l["minimum_hours"] ?? 0), refund_percent = (decimal?)l["refund_percent"] ?? 0, fixed_fee = (decimal?)l["fixed_fee"] ?? 0
        }).ToList());
        dlg.Codes("status", "Trạng thái", "HOAT_DONG", "HOAT_DONG", "NGUNG_HOAT_DONG");
        dlg.OnSubmit = v => Api.PostJsonAsync("v1/cancellation-policies", v);
        return Task.FromResult(dlg.ShowFor(Owner));
    }

    // =====================================================================
    // Phụ thu nhận sớm / trả trễ
    // =====================================================================
    private DataView Surcharges()
    {
        var view = new DataView(Session, "Phụ thu nhận phòng sớm / trả phòng trễ") { Loader = _ => Api.GetJsonAsync("v1/surcharge-policies") };
        view.Columns(
                Col.Label("Loại", "loai", 70),
                Col.Text("Tên", "ten_chinh_sach", 140),
                Col.Custom("Khoảng thời gian", e => $"{J.Int(e, "tu_phut")} – {(J.Has(e, "den_phut") ? J.Int(e, "den_phut") + " phút" : "trở lên")}", 100),
                Col.Custom("Mức phụ thu", e => J.Str(e, "kieu_tinh") == "PHAN_TRAM" ? $"{J.Dec(e, "gia_tri"):0.##}% giá đêm" : J.MoneyVnd(J.Dec(e, "gia_tri")), 90),
                Col.Status("Trạng thái", "trang_thai", 75))
            .AddAction("Bật / tắt", r => ToggleStatusAsync(r!.Value, "surcharge-policies"), "PRICING_WRITE")
            .AddAction("Thêm mức phụ thu", _ => AddSurchargeAsync(), "PRICING_WRITE", needsRow: false, primary: true);
        return view;
    }

    private Task<bool> AddSurchargeAsync()
    {
        var dlg = new FormDialog("Thêm mức phụ thu", 560, "Số phút tính so với giờ nhận phòng 14:00 / trả phòng 12:00 chuẩn.");
        dlg.Codes("type", "Loại", "NHAN_SOM", "NHAN_SOM", "TRA_TRE");
        dlg.TextField("name", "Tên", null, required: true);
        dlg.Number("from_minute", "Từ phút", 1, 0, 1440);
        var to = dlg.Number("to_minute", "Đến phút (0 = không giới hạn)", 180, 0, 1440);
        dlg.Codes("calculation", "Cách tính", "PHAN_TRAM", "PHAN_TRAM", "SO_TIEN_CO_DINH");
        dlg.Number("value", "Giá trị (% hoặc số tiền)", 30, 0, 1_000_000_000, 2);
        dlg.Codes("status", "Trạng thái", "HOAT_DONG", "HOAT_DONG", "NGUNG_HOAT_DONG");
        dlg.OnSubmit = v =>
        {
            var toMinute = (decimal)v["to_minute"]!;
            return Api.PostJsonAsync("v1/surcharge-policies", new
            {
                name = v["name"], type = v["type"], from_minute = (int)(decimal)v["from_minute"]!, to_minute = toMinute == 0 ? (int?)null : (int)toMinute,
                calculation = v["calculation"], value = v["value"], status = v["status"]
            });
        };
        return Task.FromResult(dlg.ShowFor(Owner));
    }

    // =====================================================================
    private async Task<bool> ToggleStatusAsync(JsonElement row, string resource, string statusPath = "trang_thai")
    {
        var next = J.Str(row, statusPath) == "HOAT_DONG" ? "NGUNG_HOAT_DONG" : "HOAT_DONG";
        if (!Ui.Confirm(Owner, next == "HOAT_DONG" ? "Bật lại chính sách này?" : "Ngừng áp dụng chính sách này?")) return false;
        await Api.PatchJsonAsync($"v1/{resource}/{J.Long(row, "id")}/status", new { status = next });
        return true;
    }
}
