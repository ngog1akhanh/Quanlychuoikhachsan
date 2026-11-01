using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Module Dịch vụ (mục 11): yêu cầu dịch vụ của khách đang lưu trú (tiếp nhận → hoàn thành / hủy)
/// và danh mục dịch vụ. Hoàn thành một yêu cầu sẽ lập hóa đơn dịch vụ riêng (thu ở module Hóa đơn).
/// </summary>
public sealed class ServicesPage : ModulePage
{
    public ServicesPage(ApiClient api, UserSession session) : base(api, session)
    {
        AddTab("Yêu cầu dịch vụ", Requests());
        AddTab("Danh mục dịch vụ", Catalog());
        BuildTabs();
    }

    public override string Title => "Dịch vụ";

    private IWin32Window? Owner => FindForm();

    // =====================================================================
    // Yêu cầu dịch vụ
    // =====================================================================
    private DataView Requests()
    {
        var view = new DataView(Session, "Yêu cầu dịch vụ")
        {
            Paged = true,
            Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/app/service-requests", ("page", q.Page), ("pageSize", q.PageSize),
                ("search", q.Search), ("status", q.Status)))
        };
        view.AddSearch("Mã booking, tên khách, dịch vụ…")
            .AddStatusFilter(null, "CHO_XU_LY", "DANG_XU_LY", "HOAN_THANH", "DA_HUY")
            .Columns(
                Col.Time("Yêu cầu lúc", "requested_at", 80),
                Col.Text("Booking", "booking_code", 75),
                Col.Text("Khách", "customer_name", 105),
                Col.Text("Phòng", "room_number", 45),
                Col.Text("Dịch vụ", "service_name", 110),
                Col.Custom("Số lượng", e => $"{J.Qty(J.Dec(e, "quantity"))} {J.Str(e, "unit")}", 60),
                Col.Money("Đơn giá", "unit_price", 65),
                Col.Money("Thành tiền", "amount", 70),
                Col.Text("Ghi chú", "note", 100),
                Col.Time("Phục vụ lúc", "served_at", 80),
                Col.Status("Trạng thái", "status", 80))
            .AddAction("Hoàn thành", r => TransitionAsync(r!.Value, "HOAN_THANH"), "SERVICE_WRITE", primary: true,
                when: r => J.Str(r, "status") == "DANG_XU_LY")
            .AddAction("Tiếp nhận", r => TransitionAsync(r!.Value, "DANG_XU_LY"), "SERVICE_WRITE",
                when: r => J.Str(r, "status") == "CHO_XU_LY")
            .AddAction("Hủy yêu cầu", r => TransitionAsync(r!.Value, "DA_HUY"), "SERVICE_WRITE",
                when: r => J.Str(r, "status") == "CHO_XU_LY")
            .AddAction("Tạo yêu cầu", _ => CreateRequestAsync(), "SERVICE_WRITE", needsRow: false);
        return view;
    }

    private async Task<bool> CreateRequestAsync()
    {
        var stays = J.Items(await Api.GetJsonAsync("v1/app/active-stays"));
        var targets = new List<(string Text, string Value)>();
        foreach (var stay in stays)
        {
            var rooms = J.Items(stay, "rooms");
            if (rooms.Count == 0)
                targets.Add(($"{J.Str(stay, "booking_code")} · {J.Str(stay, "customer_name")}", $"{J.Long(stay, "id")}:"));
            foreach (var room in rooms)
                targets.Add(($"Phòng {J.Str(room, "room_number")} · {J.Str(stay, "booking_code")} · {J.Str(stay, "customer_name")}",
                    $"{J.Long(stay, "id")}:{J.Long(room, "room_id")}"));
        }
        if (targets.Count == 0)
        {
            Ui.Info(Owner, "Hiện không có khách đang lưu trú. Chỉ tạo yêu cầu dịch vụ cho booking đang ở.");
            return false;
        }
        var services = J.Items(await Api.GetJsonAsync("v1/services/manage?status=HOAT_DONG&pageSize=100"));
        var serviceOptions = services.Select(e => ($"{J.Str(e, "ten_dich_vu")} · {J.MoneyVnd(J.Dec(e, "don_gia"))}/{J.Str(e, "don_vi_tinh")}", J.Long(e, "id"))).ToList();
        if (serviceOptions.Count == 0)
        {
            Ui.Info(Owner, "Chưa có dịch vụ nào đang hoạt động. Thêm dịch vụ ở tab Danh mục dịch vụ.");
            return false;
        }

        var dlg = new FormDialog("Tạo yêu cầu dịch vụ", 560, "Giá được chốt theo đơn giá hiện tại của dịch vụ.");
        dlg.Combo("target", "Phòng / khách đang ở", targets, targets[0].Value, span: 2);
        dlg.Combo("service_id", "Dịch vụ", serviceOptions, serviceOptions[0].Item2, span: 2);
        dlg.Number("quantity", "Số lượng", 1, 0.01m, 9999, 2, 1);
        dlg.TextField("note", "Ghi chú", null, span: 2, multiline: true);
        dlg.OnSubmit = v =>
        {
            var parts = ((string)v["target"]!).Split(':');
            return Api.PostJsonAsync("v1/service-requests", new
            {
                booking_id = long.Parse(parts[0]),
                room_id = parts[1].Length == 0 ? (long?)null : long.Parse(parts[1]),
                service_id = (long)v["service_id"]!,
                quantity = (decimal)v["quantity"]!,
                note = (string?)v["note"]
            });
        };
        return dlg.ShowFor(Owner);
    }

    private async Task<bool> TransitionAsync(JsonElement row, string status)
    {
        var question = status switch
        {
            "DANG_XU_LY" => $"Tiếp nhận yêu cầu \"{J.Str(row, "service_name")}\" cho phòng {J.Str(row, "room_number")}?",
            "HOAN_THANH" => $"Xác nhận đã phục vụ xong \"{J.Str(row, "service_name")}\"?\nHệ thống sẽ lập hóa đơn dịch vụ {J.MoneyVnd(J.Dec(row, "amount"))}.",
            _ => $"Hủy yêu cầu \"{J.Str(row, "service_name")}\" của {J.Str(row, "customer_name")}?"
        };
        if (!Ui.Confirm(Owner, question)) return false;
        await Api.PatchJsonAsync($"v1/service-requests/{J.Long(row, "id")}/status", new { status });
        if (status == "HOAN_THANH" && J.Dec(row, "amount") > 0)
            Ui.Info(Owner, "Đã lập hóa đơn dịch vụ. Thu tiền ở module Hóa đơn & thanh toán → tab Hóa đơn dịch vụ.");
        return true;
    }

    // =====================================================================
    // Danh mục dịch vụ
    // =====================================================================
    private DataView Catalog()
    {
        var view = new DataView(Session, "Danh mục dịch vụ")
        {
            Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/services/manage", ("pageSize", 100), ("status", q.Status)))
        };
        view.AddSearch("Tên, mã, nhóm dịch vụ…")
            .AddStatusFilter(null, "HOAT_DONG", "NGUNG_HOAT_DONG")
            .Columns(
                Col.Text("Mã", "ma_dich_vu", 60),
                Col.Text("Tên dịch vụ", "ten_dich_vu", 140),
                Col.Text("Nhóm", "nhom_dich_vu", 80),
                Col.Text("Đơn vị", "don_vi_tinh", 55),
                Col.Money("Đơn giá", "don_gia", 70),
                Col.Text("Mô tả", "mo_ta", 160),
                Col.Status("Trạng thái", "trang_thai", 75))
            .AddAction("Ngừng / mở bán", r => ToggleServiceAsync(r!.Value), "SERVICE_CATALOG_WRITE")
            .AddAction("Sửa", r => EditServiceAsync(r!.Value), "SERVICE_CATALOG_WRITE")
            .AddAction("Thêm dịch vụ", _ => EditServiceAsync(null), "SERVICE_CATALOG_WRITE", needsRow: false, primary: true);
        if (Session.HasPermission("SERVICE_CATALOG_WRITE")) view.DefaultAction = r => EditServiceAsync(r!.Value);
        return view;
    }

    private Task<bool> EditServiceAsync(JsonElement? row)
    {
        var dlg = new FormDialog(row is null ? "Thêm dịch vụ" : "Sửa dịch vụ", 560);
        dlg.TextField("code", "Mã dịch vụ", FormDialog.S(row, "ma_dich_vu"), required: true);
        dlg.TextField("name", "Tên dịch vụ", FormDialog.S(row, "ten_dich_vu"), required: true);
        dlg.TextField("group", "Nhóm (ăn uống, giặt ủi, spa…)", FormDialog.S(row, "nhom_dich_vu"));
        dlg.TextField("unit", "Đơn vị tính", FormDialog.S(row, "don_vi_tinh") ?? "lần", required: true);
        dlg.Money("price", "Đơn giá", FormDialog.D(row, "don_gia"));
        dlg.Codes("status", "Trạng thái", FormDialog.S(row, "trang_thai") ?? "HOAT_DONG", "HOAT_DONG", "NGUNG_HOAT_DONG");
        dlg.TextField("description", "Mô tả", FormDialog.S(row, "mo_ta"), span: 2, multiline: true);
        dlg.OnSubmit = v => row is { } r ? Api.PutJsonAsync($"v1/services/{J.Long(r, "id")}", v) : Api.PostJsonAsync("v1/services", v);
        return Task.FromResult(dlg.ShowFor(Owner));
    }

    private async Task<bool> ToggleServiceAsync(JsonElement row)
    {
        var next = J.Str(row, "trang_thai") == "HOAT_DONG" ? "NGUNG_HOAT_DONG" : "HOAT_DONG";
        if (!Ui.Confirm(Owner, $"{(next == "HOAT_DONG" ? "Mở bán lại" : "Ngừng bán")} dịch vụ \"{J.Str(row, "ten_dich_vu")}\"?")) return false;
        await Api.PutJsonAsync($"v1/services/{J.Long(row, "id")}", new
        {
            code = J.Str(row, "ma_dich_vu"), name = J.Str(row, "ten_dich_vu"), group = J.StrOrNull(row, "nhom_dich_vu"),
            unit = J.Str(row, "don_vi_tinh"), price = J.Dec(row, "don_gia"), description = J.StrOrNull(row, "mo_ta"), status = next
        });
        return true;
    }
}
