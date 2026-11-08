using System.Diagnostics;
using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Module Hóa đơn & thanh toán (mục 12): hóa đơn phòng, hóa đơn dịch vụ, phiếu cọc, đơn cọc QR, phiếu hoàn tiền, ca thu ngân.
/// Hóa đơn đã phát hành không sửa; sai thì hủy và lập phiếu hoàn theo đúng khoản đã thu.
/// </summary>
public sealed class BillingPage : ModulePage
{
    private readonly Lookups lookups;

    public BillingPage(ApiClient api, UserSession session) : base(api, session)
    {
        lookups = new Lookups(api);
        AddTab("Hóa đơn phòng", RoomInvoices());
        AddTab("Hóa đơn dịch vụ", ServiceInvoices());
        AddTab("Phiếu cọc", Deposits());
        AddTab("Đơn cọc QR", DepositOrders());
        AddTab("Phiếu hoàn tiền", Refunds());
        AddTab("Ca thu ngân", Shifts());
        BuildTabs();
    }

    public override string Title => "Hóa đơn & thanh toán";

    private IWin32Window? Owner => FindForm();

    private Task<JsonElement> Page(string path, DataQuery q)
        => Api.GetJsonAsync(ApiClient.Url(path, ("page", q.Page), ("pageSize", q.PageSize), ("search", q.Search), ("status", q.Status)));

    // =====================================================================
    // Hóa đơn phòng
    // =====================================================================
    private DataView RoomInvoices()
    {
        var view = new DataView(Session, "Hóa đơn tiền phòng") { Paged = true, Loader = q => Page("v1/app/invoices", q) };
        view.AddSearch("Mã HĐ, mã booking, tên khách, SĐT…")
            .AddStatusFilter(null, "NHAP", "DA_PHAT_HANH", "TT_MOT_PHAN", "DA_THANH_TOAN", "DA_HUY")
            .Columns(
                Col.Text("Mã hóa đơn", "invoice_code", 95),
                Col.Text("Booking", "booking_code", 80),
                Col.Text("Khách", "customer_name", 110),
                Col.Text("Phòng", "rooms", 55),
                Col.Time("Ngày lập", "issued_at", 85),
                Col.Money("Tổng", "total"),
                Col.Money("Cọc trừ", "deposit_applied", 70),
                Col.Money("Đã thu", "paid", 70),
                Col.Money("Còn nợ", "due", 70),
                Col.Status("Trạng thái", "status", 95))
            .AddAction("Thu tiền", r => PayRoomInvoiceAsync(r!.Value), "BILLING_PAYMENT_WRITE", primary: true,
                when: r => J.Str(r, "status") is "DA_PHAT_HANH" or "TT_MOT_PHAN" && J.Dec(r, "due") > 0)
            .AddAction("Chốt hóa đơn", r => PrepareInvoiceAsync(r!.Value), "BILLING_WRITE", when: r => J.Str(r, "status") == "NHAP")
            .AddAction("Xem chi tiết", r => ShowRoomInvoiceAsync(r!.Value))
            .AddAction("In", r => PrintAsync(r!.Value), when: r => J.Str(r, "status") != "NHAP")
            .AddAction("Gửi khách", r => SendInvoiceAsync(r!.Value), "BILLING_WRITE",
                when: r => J.Bool(r, "has_account") && J.Str(r, "status") is not ("NHAP" or "DA_HUY"))
            .AddAction("Hủy hóa đơn", r => VoidAsync(r!.Value, $"v1/invoices/{J.Long(r.Value, "id")}:void"), "BILLING_WRITE",
                when: r => J.Str(r, "status") != "DA_HUY");
        view.DefaultAction = r => ShowRoomInvoiceAsync(r!.Value);
        return view;
    }

    private async Task<bool> PayRoomInvoiceAsync(JsonElement row)
    {
        var id = J.Long(row, "id");
        var due = J.Dec(row, "due");
        var methods = await lookups.PaymentMethodsAsync();
        var shiftId = await MyOpenShiftAsync();
        var dlg = new FormDialog($"Thu tiền hóa đơn {J.Str(row, "invoice_code")}", 520,
            $"{J.Str(row, "customer_name")} · booking {J.Str(row, "booking_code")}", okText: "Xác nhận đã thu");
        dlg.Note($"Tổng {J.MoneyVnd(J.Dec(row, "total"))} · cọc trừ {J.MoneyVnd(J.Dec(row, "deposit_applied"))} · đã thu {J.MoneyVnd(J.Dec(row, "paid"))} · còn phải thu {J.MoneyVnd(due)}"
            + (shiftId is null ? "\nChưa mở ca thu ngân: khoản thu sẽ không tính vào đối soát ca." : ""));
        dlg.Combo("payment_method_id", "Phương thức", methods, methods.FirstOrDefault().Value);
        dlg.Money("amount", "Số tiền thu", due, due);
        dlg.TextField("transaction_id", "Mã giao dịch / số biên lai", ApiClient.NewReference("HD"), required: true, span: 2);
        dlg.Fixed("payment_gateway", "MANUAL");
        dlg.Fixed("shift_id", shiftId);
        dlg.Validator = v => (decimal)v["amount"]! <= 0 ? "Số tiền phải lớn hơn 0." : null;
        dlg.OnSubmit = v => Api.PostJsonAsync($"v1/invoices/{id}:pay", v);
        return dlg.ShowFor(Owner);
    }

    private Task<bool> PrepareInvoiceAsync(JsonElement row)
    {
        var dlg = new FormDialog($"Chốt hóa đơn {J.Str(row, "invoice_code")}", 480,
            "Tính lại tiền phòng, phụ thu theo chính sách và bồi thường sự cố, rồi phát hành hóa đơn.", okText: "Chốt & phát hành");
        dlg.Money("early_or_late_surcharge", "Phụ thu thêm (ngoài chính sách)", 0);
        dlg.Number("vat_percent", "VAT (%)", 10, 0, 100, 1, 1);
        dlg.OnSubmit = v => Api.PostJsonAsync($"v1/bookings/{J.Long(row, "booking_id")}/invoice:prepare", new
        {
            employee_id = 0,
            early_or_late_surcharge = (decimal)v["early_or_late_surcharge"]!,
            vat_rate = (decimal)v["vat_percent"]! / 100m
        });
        return Task.FromResult(dlg.ShowFor(Owner));
    }

    private async Task<bool> ShowRoomInvoiceAsync(JsonElement row)
    {
        var id = J.Long(row, "id");
        var detail = await Api.GetJsonAsync($"v1/invoices/{id}");
        var methods = await lookups.NamesAsync(Lookups.PaymentMethodsUrl, "ten_phuong_thuc");
        var status = J.Str(row, "status");
        using var dlg = new DetailDialog($"Hóa đơn {J.Str(row, "invoice_code")}", $"{J.Str(row, "customer_name")} · booking {J.Str(row, "booking_code")} · phòng {J.Str(row, "rooms")}");
        dlg.Info(
            ("Trạng thái", Labels.Of(status)), ("Ngày lập", J.TimeText(J.Time(row, "issued_at"))),
            ("Tiền phòng", J.MoneyVnd(J.Dec(row, "room_amount"))), ("Phụ thu", J.MoneyVnd(J.Dec(row, "surcharge"))),
            ("Bồi thường", J.MoneyVnd(J.Dec(row, "compensation"))), ("VAT", J.MoneyVnd(J.Dec(row, "vat"))),
            ("Tổng cộng", J.MoneyVnd(J.Dec(row, "total"))), ("Cọc đã trừ", J.MoneyVnd(J.Dec(row, "deposit_applied"))),
            ("Đã thu", J.MoneyVnd(J.Dec(row, "paid"))), ("Còn phải thu", J.MoneyVnd(J.Dec(row, "due"))));
        if (J.StrOrNull(row, "void_reason") is { } reason) dlg.Info(("Lý do hủy", reason));
        dlg.Table("Dòng hóa đơn", J.Items(detail, "lines"), 170,
            Col.Label("Loại", "loai_dong", 60), Col.Text("Nội dung", "noi_dung", 160), Col.Day("Ngày", "ngay_ap_dung", 60),
            Col.Qty("SL", "so_luong", 40), Col.Money("Đơn giá", "don_gia", 70), Col.Money("Thành tiền", "thanh_tien", 70));
        var payments = J.Items(detail, "payments");
        dlg.Table("Các lần thu tiền", payments, 130,
            Col.Time("Thời gian", "thoi_gian_thanh_toan", 80), Col.Custom("Phương thức", e => methods.GetValueOrDefault(J.Long(e, "phuong_thuc_id")), 70),
            Col.Money("Số tiền", "so_tien", 70), Col.Text("Mã giao dịch", "ma_giao_dich", 110), Col.Status("Trạng thái", "trang_thai", 70));
        if (status == "DA_HUY" && Session.HasPermission("REFUND_WRITE") && payments.Count > 0)
            dlg.Action("Lập phiếu hoàn cho khoản thu đã chọn", async () =>
            {
                if (dlg.SelectedOf(1) is not { } payment) { Ui.Error(dlg, "Chọn một lần thu tiền trong bảng."); return false; }
                return await CreateRefundAsync(dlg, "room_payment_id", J.Long(payment, "id"), J.Dec(payment, "so_tien"),
                    $"Hoàn khoản thu {J.Str(payment, "ma_giao_dich")} của hóa đơn {J.Str(row, "invoice_code")}");
            });
        return dlg.ShowDialog(Owner) == DialogResult.OK;
    }

    private async Task<bool> PrintAsync(JsonElement row)
    {
        var html = await Api.GetTextAsync($"v1/invoices/{J.Long(row, "id")}/print");
        var path = Path.Combine(Path.GetTempPath(), $"hoa-don-{J.Str(row, "invoice_code")}.html");
        await File.WriteAllTextAsync(path, html);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return false;
    }

    private async Task<bool> SendInvoiceAsync(JsonElement row)
    {
        await Api.PostJsonAsync($"v1/invoices/{J.Long(row, "id")}:send");
        Ui.Info(Owner, "Đã gửi đường dẫn hóa đơn vào hộp thư tài khoản của khách.");
        return false;
    }

    private async Task<bool> VoidAsync(JsonElement row, string url)
    {
        var reason = Ui.Prompt(Owner, "Hủy hóa đơn",
            "Lý do hủy (hóa đơn đã hủy không khôi phục được; khoản đã thu cần lập phiếu hoàn):");
        if (reason is null) return false;
        await Api.PostJsonAsync(url, new { reason });
        return true;
    }

    // =====================================================================
    // Hóa đơn dịch vụ
    // =====================================================================
    private DataView ServiceInvoices()
    {
        var view = new DataView(Session, "Hóa đơn dịch vụ") { Paged = true, Loader = q => Page("v1/app/service-invoices", q) };
        view.AddSearch("Mã HĐ, mã booking, tên khách…")
            .AddStatusFilter(null, "DA_PHAT_HANH", "TT_MOT_PHAN", "DA_THANH_TOAN", "DA_HUY")
            .Columns(
                Col.Custom("Mã hóa đơn", e => Short(J.Str(e, "invoice_code")), 85),
                Col.Text("Booking", "booking_code", 80),
                Col.Text("Khách", "customer_name", 110),
                Col.Text("Dịch vụ", "services", 130),
                Col.Time("Ngày lập", "issued_at", 85),
                Col.Money("Tổng", "total"),
                Col.Money("Đã thu", "paid", 70),
                Col.Money("Còn nợ", "due", 70),
                Col.Status("Trạng thái", "status", 95))
            .AddAction("Thu tiền", r => PayServiceInvoiceAsync(r!.Value), "BILLING_PAYMENT_WRITE", primary: true,
                when: r => J.Str(r, "status") is "DA_PHAT_HANH" or "TT_MOT_PHAN" && J.Dec(r, "due") > 0)
            .AddAction("Xem chi tiết", r => ShowServiceInvoiceAsync(r!.Value))
            .AddAction("Hủy hóa đơn", r => VoidAsync(r!.Value, $"v1/service-invoices/{J.Long(r.Value, "id")}:void"), "BILLING_WRITE",
                when: r => J.Str(r, "status") != "DA_HUY");
        view.DefaultAction = r => ShowServiceInvoiceAsync(r!.Value);
        return view;
    }

    private async Task<bool> PayServiceInvoiceAsync(JsonElement row)
    {
        var id = J.Long(row, "id");
        var due = J.Dec(row, "due");
        var methods = await lookups.PaymentMethodsAsync();
        var shiftId = await MyOpenShiftAsync();
        var dlg = new FormDialog("Thu tiền dịch vụ", 520, $"{J.Str(row, "customer_name")} · {J.Str(row, "services")}", okText: "Xác nhận đã thu");
        dlg.Note($"Tổng {J.MoneyVnd(J.Dec(row, "total"))} · đã thu {J.MoneyVnd(J.Dec(row, "paid"))} · còn phải thu {J.MoneyVnd(due)}");
        dlg.Combo("payment_method_id", "Phương thức", methods, methods.FirstOrDefault().Value);
        dlg.Money("amount", "Số tiền thu", due, due);
        dlg.TextField("reference", "Mã giao dịch / số biên lai", ApiClient.NewReference("DV"), required: true, span: 2);
        dlg.Fixed("shift_id", shiftId);
        dlg.Validator = v => (decimal)v["amount"]! <= 0 ? "Số tiền phải lớn hơn 0." : null;
        dlg.OnSubmit = v => Api.PostJsonAsync($"v1/service-invoices/{id}:pay", v);
        return dlg.ShowFor(Owner);
    }

    private async Task<bool> ShowServiceInvoiceAsync(JsonElement row)
    {
        var id = J.Long(row, "id");
        var detail = await Api.GetJsonAsync($"v1/service-invoices/{id}");
        var methods = await lookups.NamesAsync(Lookups.PaymentMethodsUrl, "ten_phuong_thuc");
        using var dlg = new DetailDialog($"Hóa đơn dịch vụ {Short(J.Str(row, "invoice_code"))}", $"{J.Str(row, "customer_name")} · booking {J.Str(row, "booking_code")}");
        dlg.Info(("Dịch vụ", J.Str(row, "services")), ("Trạng thái", Labels.Of(J.Str(row, "status"))),
            ("Tổng cộng", J.MoneyVnd(J.Dec(row, "total"))), ("Còn phải thu", J.MoneyVnd(J.Dec(detail, "due"))));
        var payments = J.Items(detail, "payments");
        dlg.Table("Các lần thu tiền", payments, 160,
            Col.Time("Thời gian", "thoi_gian_thanh_toan", 80), Col.Custom("Phương thức", e => methods.GetValueOrDefault(J.Long(e, "phuong_thuc_id")), 70),
            Col.Money("Số tiền", "so_tien", 70), Col.Text("Mã giao dịch", "ma_giao_dich", 110), Col.Status("Trạng thái", "trang_thai", 70));
        if (J.Str(row, "status") == "DA_HUY" && Session.HasPermission("REFUND_WRITE") && payments.Count > 0)
            dlg.Action("Lập phiếu hoàn cho khoản thu đã chọn", async () =>
            {
                if (dlg.SelectedOf(0) is not { } payment) { Ui.Error(dlg, "Chọn một lần thu tiền trong bảng."); return false; }
                return await CreateRefundAsync(dlg, "service_payment_id", J.Long(payment, "id"), J.Dec(payment, "so_tien"),
                    $"Hoàn khoản thu dịch vụ {J.Str(payment, "ma_giao_dich")}");
            });
        return dlg.ShowDialog(Owner) == DialogResult.OK;
    }

    // =====================================================================
    // Phiếu cọc & hoàn tiền
    // =====================================================================
    private DataView Deposits()
    {
        var view = new DataView(Session, "Phiếu cọc") { Paged = true, Loader = q => Page("v1/app/deposits", q) };
        view.AddSearch("Mã phiếu, mã booking, tên khách…")
            .Columns(
                Col.Custom("Mã phiếu", e => Short(J.Str(e, "deposit_code")), 85),
                Col.Text("Booking", "booking_code", 80),
                Col.Status("TT booking", "booking_status", 85),
                Col.Text("Khách", "customer_name", 110),
                Col.Money("Số tiền", "amount", 75),
                Col.Text("Phương thức", "method", 75),
                Col.Time("Thời gian", "paid_at", 85),
                Col.Money("Đã trừ HĐ", "applied", 70),
                Col.Money("Đã/đang hoàn", "refunded", 70),
                Col.Money("Có thể hoàn", "refundable", 70),
                Col.Status("Trạng thái", "status", 85))
            .AddAction("Lập phiếu hoàn", r => CreateRefundAsync(Owner, "deposit_id", J.Long(r!.Value, "id"), J.Dec(r.Value, "refundable"),
                    $"Hoàn cọc booking {J.Str(r.Value, "booking_code")}"), "REFUND_WRITE",
                when: r => J.Dec(r, "refundable") > 0 && J.Str(r, "booking_status") is "DA_HUY" or "DA_TRA")
            .AddDetails(r => $"Phiếu cọc booking {J.Str(r, "booking_code")}",
                r => [("Mã phiếu đầy đủ", J.StrOrNull(r, "deposit_code")), ("Cổng thanh toán", J.StrOrNull(r, "gateway")),
                    ("Mã giao dịch", J.StrOrNull(r, "reference"))]);
        return view;
    }

    /// <summary>
    /// Đơn cọc VietQR do khách tạo trên website. Hệ thống tự kiểm tra mỗi phút (khi dùng VietQR thật);
    /// nhân viên có thể bấm "Kiểm tra ngay" khi khách báo đã chuyển khoản.
    /// </summary>
    private DataView DepositOrders()
    {
        var view = new DataView(Session, "Đơn cọc QR (khách đặt trên web)") { Paged = true, Loader = q => Page("v1/app/deposit-orders", q) };
        view.AddSearch("Mã booking, tên khách, nội dung CK, mã giao dịch…")
            .AddStatusFilter(null, "PENDING", "PAID", "FAILED", "REFUND_REQUIRED", "REFUNDED")
            .Columns(
                Col.Text("Booking", "booking_code", 80),
                Col.Status("TT booking", "booking_status", 85),
                Col.Text("Khách", "customer_name", 110),
                Col.Text("SĐT", "customer_phone", 75),
                Col.Money("Số tiền", "amount", 75),
                Col.Text("Nội dung CK", "description", 95),
                Col.Time("Tạo QR lúc", "created_at", 85),
                Col.Time("Giữ phòng đến", "hold_expires_at", 85),
                Col.Time("Nhận tiền lúc", "paid_at", 85),
                Col.Text("Mã giao dịch", "transaction_id", 90),
                Col.Status("Trạng thái", "status", 85))
            .AddAction("Kiểm tra ngay", r => InquireDepositOrderAsync(r!.Value), "BILLING_PAYMENT_WRITE", primary: true,
                when: r => J.Str(r, "status") is "PENDING" or "FAILED")
            .AddAction("Giả lập đã thanh toán (demo)", r => SimulateDepositOrderAsync(r!.Value), "BILLING_PAYMENT_WRITE",
                when: r => J.Str(r, "status") == "PENDING");
        return view;
    }

    private async Task<bool> InquireDepositOrderAsync(JsonElement row)
    {
        var result = await Api.PostJsonAsync($"v1/deposit-payments/{Uri.EscapeDataString(J.Str(row, "order_id"))}/inquiry");
        Ui.Info(Owner, DepositOrderMessage(J.Str(result, "status"), J.Str(row, "booking_code")), "Kết quả kiểm tra VietQR");
        return true;
    }

    private async Task<bool> SimulateDepositOrderAsync(JsonElement row)
    {
        if (!Ui.Confirm(Owner, $"Giả lập ngân hàng báo đã nhận {J.MoneyVnd(J.Dec(row, "amount"))} cho booking {J.Str(row, "booking_code")}?\n\n" +
                "Chỉ dùng khi demo (API chạy Development, chưa cấu hình VietQR thật). Không có tiền thật được chuyển."))
            return false;
        var result = await Api.PostJsonAsync($"v1/deposit-payments/{Uri.EscapeDataString(J.Str(row, "order_id"))}:simulate-paid");
        Ui.Info(Owner, DepositOrderMessage(J.Str(result, "status"), J.Str(row, "booking_code")), "Giả lập thanh toán");
        return true;
    }

    private static string DepositOrderMessage(string status, string bookingCode) => status switch
    {
        "PAID" => $"Đã nhận tiền cọc. Booking {bookingCode} đã được xác nhận.",
        "REFUND_REQUIRED" => "Đã nhận tiền nhưng phòng không còn giữ hoặc số tiền không khớp.\nHệ thống đã lập phiếu hoàn chờ xử lý – xem tab Phiếu hoàn tiền.",
        "FAILED" => "Mã QR đã hết hạn hoặc bị hủy phía ngân hàng. Khách có thể tạo QR mới nếu phòng còn giữ.",
        _ => "Chưa nhận được tiền chuyển khoản."
    };

    private DataView Refunds()
    {
        var view = new DataView(Session, "Phiếu hoàn tiền") { Paged = true, Loader = q => Page("v1/app/refunds", q) };
        view.AddAction("Lập phiếu hoàn cọc", _ => ChooseDepositRefundAsync(), "REFUND_WRITE", needsRow: false);
        view.AddStatusFilter("CHO_XU_LY", "CHO_XU_LY", "THANH_CONG", "DA_HUY")
            .Columns(
                Col.Custom("Mã phiếu", e => Short(J.Str(e, "refund_code")), 85),
                Col.Text("Booking", "booking_code", 80),
                Col.Text("Nguồn tiền", "source", 100),
                Col.Money("Số tiền", "amount", 75),
                Col.Text("Phương thức", "method", 75),
                Col.Text("Lý do", "reason", 150),
                Col.Time("Thời gian", "time", 85),
                Col.Text("Mã tham chiếu", "reference", 90),
                Col.Status("Trạng thái", "status", 85))
            .AddAction("Xác nhận đã hoàn", r => ConfirmRefundAsync(r!.Value), "REFUND_CONFIRM", primary: true,
                when: r => J.Str(r, "status") == "CHO_XU_LY");
        return view;
    }

    private async Task<bool> CreateRefundAsync(IWin32Window? owner, string sourceKey, long sourceId, decimal max, string reason)
    {
        var methods = await lookups.PaymentMethodsAsync();
        var dlg = new FormDialog("Lập phiếu hoàn tiền", 520, "Phiếu ở trạng thái chờ xử lý; xác nhận sau khi đã thực sự trả tiền cho khách.");
        dlg.Fixed(sourceKey, sourceId);
        dlg.Combo("payment_method_id", "Hoàn bằng", methods, methods.FirstOrDefault().Value);
        dlg.Money("amount", "Số tiền hoàn", max, Math.Max(max, 0));
        dlg.TextField("reason", "Lý do", reason, required: true, span: 2, multiline: true);
        dlg.Validator = v => (decimal)v["amount"]! <= 0 ? "Số tiền hoàn phải lớn hơn 0." : null;
        dlg.OnSubmit = v => Api.PostJsonAsync("v1/refunds", v);
        if (!dlg.ShowFor(owner)) return false;
        Ui.Info(owner, "Đã lập phiếu hoàn. Xem ở tab Phiếu hoàn tiền để xác nhận sau khi chi tiền.");
        return true;
    }

    private async Task<bool> ChooseDepositRefundAsync()
    {
        var rows = J.Items(await Api.GetJsonAsync("v1/app/deposits?pageSize=100"))
            .Where(x => J.Dec(x, "refundable") > 0 && J.Str(x, "booking_status") is "DA_HUY" or "DA_TRA").ToList();
        if (rows.Count == 0) { Ui.Info(Owner, "Chưa có cọc còn dư của booking đã hủy/trả phòng. Có thể lập phiếu từ tab Phiếu cọc; yêu cầu hủy được duyệt cũng tự tạo phiếu hoàn."); return false; }
        var dlg = new FormDialog("Chọn khoản cọc cần hoàn", 560, columns: 1);
        dlg.Combo("id", "Booking / khoản cọc", rows.Select(x => ($"{J.Str(x, "booking_code")} · {J.MoneyVnd(J.Dec(x, "refundable"))}", J.Long(x, "id"))), J.Long(rows[0], "id"), required: true);
        if (!dlg.ShowFor(Owner)) return false;
        var selected = rows.Single(x => J.Long(x, "id") == (long)dlg.Values["id"]!);
        return await CreateRefundAsync(Owner, "deposit_id", J.Long(selected, "id"), J.Dec(selected, "refundable"), $"Hoàn cọc booking {J.Str(selected, "booking_code")}");
    }

    private async Task<bool> ConfirmRefundAsync(JsonElement row)
    {
        var methods = await lookups.PaymentMethodsAsync();
        var dlg = new FormDialog("Xác nhận đã hoàn tiền", 500, $"{J.MoneyVnd(J.Dec(row, "amount"))} · {J.Str(row, "source")} · booking {J.Str(row, "booking_code")}", okText: "Xác nhận");
        dlg.TextField("reference", "Mã giao dịch hoàn / số phiếu chi", null, required: true, span: 2);
        dlg.Combo("payment_method_id", "Phương thức thực tế", methods, null, allowEmpty: true, emptyText: $"Giữ nguyên ({J.Str(row, "method")})", span: 2);
        dlg.OnSubmit = v => Api.PostJsonAsync($"v1/refunds/{J.Long(row, "id")}:confirm", v);
        return dlg.ShowFor(Owner);
    }

    // =====================================================================
    // Ca thu ngân
    // =====================================================================
    private DataView Shifts()
    {
        var view = new DataView(Session, "Ca thu ngân") { Paged = true, Loader = q => Page("v1/app/shifts", q) };
        view.AddStatusFilter(null, "DANG_MO", "DA_DONG")
            .Columns(
                Col.Text("Thu ngân", "employee_name", 120),
                Col.Time("Bắt đầu", "started_at", 90),
                Col.Time("Kết thúc", "ended_at", 90),
                Col.Money("Tiền đầu ca", "opening_cash", 80),
                Col.Custom("Ca của tôi", e => J.Bool(e, "mine") ? "✓" : "", 50),
                Col.Status("Trạng thái", "status", 80))
            .AddAction("Đối soát & đóng ca", r => CloseShiftAsync(r!.Value), "BILLING_PAYMENT_WRITE", primary: true,
                when: r => J.Bool(r, "mine") && J.Str(r, "status") == "DANG_MO")
            .AddAction("Mở ca", _ => OpenShiftAsync(), "BILLING_PAYMENT_WRITE", needsRow: false)
            .AddAction("Xem đối soát", r => ShowReconciliationAsync(r!.Value));
        view.DefaultAction = r => ShowReconciliationAsync(r!.Value);
        return view;
    }

    private Task<bool> OpenShiftAsync()
    {
        var dlg = new FormDialog("Mở ca thu ngân", 420, "Các khoản thu trong ca sẽ được đối soát khi đóng ca.", okText: "Mở ca");
        dlg.Money("opening_cash", "Tiền mặt đầu ca", 0);
        dlg.OnSubmit = v => Api.PostJsonAsync("v1/shifts", v);
        return Task.FromResult(dlg.ShowFor(Owner));
    }

    private async Task<bool> CloseShiftAsync(JsonElement row)
    {
        var id = J.Long(row, "id");
        var data = await Api.GetJsonAsync($"v1/shifts/{id}/reconciliation");
        var totals = ShiftTotals(data);
        var methods = await lookups.PaymentMethodsAsync();
        var allNames = await lookups.NamesAsync(Lookups.PaymentMethodsUrl, "ten_phuong_thuc");
        var ids = methods.Select(m => m.Value).Union(totals.Keys).ToList();

        var editor = new LinesEditor(220, false,
            new LineCol("method", "Phương thức", LineKind.ReadOnly, 90),
            new LineCol("system", "Hệ thống ghi nhận", LineKind.ReadOnly, 80),
            new LineCol("actual_total", "Thực tế đếm được", LineKind.Number, 80),
            new LineCol("note", "Ghi chú chênh lệch", LineKind.Text, 120)) { SkipEmptyRows = false };
        foreach (var methodId in ids)
        {
            var system = totals.GetValueOrDefault(methodId);
            editor.AddRow(new Dictionary<string, object?>
            {
                ["method"] = allNames.GetValueOrDefault(methodId, $"#{methodId}"), ["system"] = J.Money(system), ["actual_total"] = system
            });
        }
        var dlg = new FormDialog("Đối soát & đóng ca", 680,
            $"Ca của {J.Str(row, "employee_name")} từ {J.TimeText(J.Time(row, "started_at"))} · tiền đầu ca {J.MoneyVnd(J.Dec(row, "opening_cash"))}",
            columns: 1, okText: "Đóng ca");
        dlg.Note("Nhập số tiền thực tế theo từng phương thức. Số thực tế không gồm tiền đầu ca.");
        dlg.Custom("lines", "", editor, () =>
        {
            var lines = editor.Lines();
            return lines.Select((l, i) => new Dictionary<string, object?>
            {
                ["payment_method_id"] = ids[i], ["actual_total"] = l["actual_total"] ?? 0m, ["note"] = l["note"]
            }).ToList();
        });
        dlg.OnSubmit = v => Api.PostJsonAsync($"v1/shifts/{id}:close", new { lines = v["lines"] });
        try
        {
            return dlg.ShowFor(Owner);
        }
        catch (FormatException ex)
        {
            Ui.Error(Owner, ex.Message);
            return false;
        }
    }

    private async Task<bool> ShowReconciliationAsync(JsonElement row)
    {
        var data = await Api.GetJsonAsync($"v1/shifts/{J.Long(row, "id")}/reconciliation");
        var names = await lookups.NamesAsync(Lookups.PaymentMethodsUrl, "ten_phuong_thuc");
        var totals = ShiftTotals(data);
        using var dlg = new DetailDialog("Đối soát ca", $"{J.Str(row, "employee_name")} · {J.TimeText(J.Time(row, "started_at"))} → {J.TimeText(J.Time(row, "ended_at"))}", 720, 520);
        dlg.Info(("Tiền đầu ca", J.MoneyVnd(J.Dec(row, "opening_cash"))), ("Trạng thái", Labels.Of(J.Str(row, "status"))),
            ("Tổng thu hệ thống", J.MoneyVnd(totals.Values.Sum())), ("Số phương thức", totals.Count.ToString()));
        foreach (var (methodId, amount) in totals)
            dlg.Info((names.GetValueOrDefault(methodId, $"#{methodId}"), J.MoneyVnd(amount)), ("", ""));
        dlg.Table("Kết quả đối soát khi đóng ca", J.Items(data, "reconciliations"), 160,
            Col.Custom("Phương thức", e => names.GetValueOrDefault(J.Long(e, "phuong_thuc_id")), 80),
            Col.Money("Hệ thống", "tong_he_thong", 70), Col.Money("Thực tế", "tong_thuc_te", 70),
            Col.Money("Chênh lệch", "chenh_lech", 70), Col.Text("Ghi chú", "ghi_chu", 120));
        dlg.ShowDialog(Owner);
        return false;
    }

    private static Dictionary<long, decimal> ShiftTotals(JsonElement data)
    {
        var result = new Dictionary<long, decimal>();
        if (J.Get(data, "totals") is { ValueKind: JsonValueKind.Object } totals)
            foreach (var p in totals.EnumerateObject())
                if (long.TryParse(p.Name, out var key) && p.Value.TryGetDecimal(out var amount)) result[key] = amount;
        return result;
    }

    /// <summary>Ca đang mở của nhân viên đăng nhập (để gắn khoản thu vào ca); null nếu chưa mở.</summary>
    private async Task<long?> MyOpenShiftAsync()
    {
        if (!Session.HasPermission("BILLING_READ")) return null;
        try
        {
            var data = await Api.GetJsonAsync("v1/app/shifts?status=DANG_MO&pageSize=50");
            return J.Items(data).Where(e => J.Bool(e, "mine")).Select(e => (long?)J.Long(e, "id")).FirstOrDefault();
        }
        catch (ApiException)
        {
            return null;
        }
    }

    /// <summary>Mã sinh tự động dài (HDV + GUID) → rút gọn để hiển thị.</summary>
    private static string Short(string code) => code.Length > 14 ? code[..3] + "…" + code[^6..] : code;
}
