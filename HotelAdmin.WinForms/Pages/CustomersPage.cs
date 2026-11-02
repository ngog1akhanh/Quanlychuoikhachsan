using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Module Khách hàng (mục 13): hồ sơ khách (có tài khoản và vãng lai), lịch sử lưu trú, quản lý đánh giá.
/// </summary>
public sealed class CustomersPage : ModulePage
{
    private static readonly (string Text, string Value)[] Genders = [("Nam", "NAM"), ("Nữ", "NU"), ("Khác", "KHAC")];

    public CustomersPage(ApiClient api, UserSession session) : base(api, session)
    {
        AddTab("Hồ sơ khách hàng", Customers());
        if (session.HasPermission("REVIEW_MANAGE")) AddTab("Đánh giá", Reviews());
        BuildTabs();
    }

    public override string Title => "Khách hàng";

    private IWin32Window? Owner => FindForm();

    // =====================================================================
    // Hồ sơ khách
    // =====================================================================
    private DataView Customers()
    {
        var view = new DataView(Session, "Hồ sơ khách hàng")
        {
            Paged = true,
            Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/app/customers", ("page", q.Page), ("pageSize", q.PageSize), ("search", q.Search)))
        };
        view.AddSearch("Tên, SĐT, email, CCCD, mã khách…", 280)
            .Columns(
                Col.Text("Mã khách", "customer_code", 80),
                Col.Text("Họ tên", "full_name", 120),
                Col.Text("Điện thoại", "phone_number", 80),
                Col.Text("Email", "email", 120),
                Col.Text("CCCD/Hộ chiếu", "identity_number", 85),
                Col.Custom("Tài khoản web", e => J.Bool(e, "has_account") ? "Có" : "Vãng lai", 60),
                Col.Custom("Booking", e => $"{J.Int(e, "stays")}/{J.Int(e, "bookings")}", 50),
                Col.Day("Lần ở gần nhất", "last_stay", 75),
                Col.Money("Tổng chi tiêu", "total_spent", 80))
            .AddAction("Lịch sử lưu trú", r => HistoryAsync(r!.Value))
            .AddAction("Sửa hồ sơ", r => EditAsync(r!.Value), "CUSTOMER_WRITE")
            .AddAction("Thêm khách", _ => CreateAsync(), "CUSTOMER_WRITE", needsRow: false, primary: true);
        view.DefaultAction = r => HistoryAsync(r!.Value);
        return view;
    }

    private Task<bool> CreateAsync()
    {
        var dlg = new FormDialog("Thêm khách hàng", 560, "Khách vãng lai (không có tài khoản website).");
        dlg.TextField("full_name", "Họ tên", null, required: true, span: 2);
        dlg.TextField("phone_number", "Số điện thoại", null);
        dlg.TextField("identity_number", "CCCD / Hộ chiếu", null);
        dlg.Date("birth_date", "Ngày sinh", null, nullable: true);
        dlg.Combo("gender", "Giới tính", Genders, null, allowEmpty: true);
        dlg.TextField("address", "Địa chỉ", null, span: 2);
        dlg.OnSubmit = v => Api.PostJsonAsync("v1/customers", v);
        return Task.FromResult(dlg.ShowFor(Owner));
    }

    private Task<bool> EditAsync(JsonElement row)
    {
        var dlg = new FormDialog($"Sửa hồ sơ {J.Str(row, "customer_code")}", 560,
            J.Bool(row, "has_account") ? "Đổi email/SĐT sẽ đổi luôn thông tin đăng nhập website của khách." : null);
        dlg.TextField("full_name", "Họ tên", J.StrOrNull(row, "full_name"), required: true, span: 2);
        dlg.TextField("phone_number", "Số điện thoại", J.StrOrNull(row, "phone_number"));
        dlg.TextField("email", "Email", J.StrOrNull(row, "email"));
        dlg.TextField("identity_number", "CCCD / Hộ chiếu", J.StrOrNull(row, "identity_number"));
        dlg.Date("birth_date", "Ngày sinh", J.Day(row, "birth_date"), nullable: true);
        dlg.Combo("gender", "Giới tính", Genders, J.StrOrNull(row, "gender"), allowEmpty: true);
        dlg.TextField("address", "Địa chỉ", J.StrOrNull(row, "address"));
        dlg.TextField("note", "Ghi chú (sở thích, lưu ý phục vụ)", J.StrOrNull(row, "note"), span: 2, multiline: true);
        dlg.OnSubmit = v => Api.PatchJsonAsync($"v1/customers/{J.Long(row, "id")}", v);
        return Task.FromResult(dlg.ShowFor(Owner));
    }

    private async Task<bool> HistoryAsync(JsonElement row)
    {
        var history = await Api.GetJsonAsync($"v1/customers/{J.Long(row, "id")}/history?pageSize=100");
        using var dlg = new DetailDialog($"Khách hàng {J.Str(row, "full_name")}", $"{J.Str(row, "customer_code")} · {J.Str(row, "phone_number")}", 780, 560);
        dlg.Info(("Email", J.StrOrNull(row, "email")), ("CCCD/Hộ chiếu", J.StrOrNull(row, "identity_number")),
            ("Ngày sinh", J.DateText(J.Day(row, "birth_date"))), ("Giới tính", Genders.FirstOrDefault(g => g.Value == J.Str(row, "gender")).Text),
            ("Địa chỉ", J.StrOrNull(row, "address")), ("Tài khoản web", J.Bool(row, "has_account") ? "Có" : "Khách vãng lai"),
            ("Số lần lưu trú", J.Int(row, "stays").ToString()), ("Tổng chi tiêu", J.MoneyVnd(J.Dec(row, "total_spent"))),
            ("Ghi chú", J.StrOrNull(row, "note")), ("Tạo hồ sơ", J.TimeText(J.Time(row, "created_at"))));
        dlg.Table("Lịch sử đặt phòng", J.Items(history), 220,
            Col.Text("Mã booking", "ma_dat_phong", 90), Col.Time("Nhận phòng", "du_kien_nhan", 90), Col.Time("Trả phòng", "du_kien_tra", 90),
            Col.Money("Tổng tiền", "tong_tien_du_kien", 80), Col.Status("Trạng thái", "trang_thai", 90));
        dlg.ShowDialog(Owner);
        return false;
    }

    // =====================================================================
    // Đánh giá
    // =====================================================================
    private DataView Reviews()
    {
        var view = new DataView(Session, "Đánh giá của khách")
        {
            Paged = true,
            Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/app/reviews", ("page", q.Page), ("pageSize", q.PageSize), ("status", q.Status)))
        };
        view.AddStatusFilter(null, "HIEN_THI", "AN")
            .Columns(
                Col.Time("Ngày", "created_at", 75),
                Col.Text("Booking", "booking_code", 75),
                Col.Text("Khách", "customer_name", 100),
                Col.Custom("Điểm", e => Stars(J.Int(e, "rating")), 65),
                Col.Custom("Phòng/DV", e => $"{Score(e, "room_rating")} / {Score(e, "service_rating")}", 55),
                Col.Text("Nội dung", "content", 200),
                Col.Text("Phản hồi", "reply", 150),
                Col.Status("Trạng thái", "status", 80))
            .AddAction("Ẩn / hiện", r => ToggleReviewAsync(r!.Value), "REVIEW_MANAGE")
            .AddAction("Phản hồi", r => ReplyAsync(r!.Value), "REVIEW_MANAGE", primary: true);
        view.DefaultAction = r => ReplyAsync(r!.Value);
        return view;
    }

    private static string Stars(int n) => n <= 0 ? "" : new string('★', Math.Min(5, n)) + new string('☆', Math.Max(0, 5 - n));

    private static string Score(JsonElement e, string path) => J.LongOrNull(e, path) is { } v ? v.ToString() : "–";

    private async Task<bool> ReplyAsync(JsonElement row)
    {
        var dlg = new FormDialog("Phản hồi đánh giá", 560, $"{J.Str(row, "customer_name")} · {Stars(J.Int(row, "rating"))} · booking {J.Str(row, "booking_code")}", columns: 1);
        dlg.TextField("content", "Khách viết", J.StrOrNull(row, "content") ?? "(không có nội dung)", multiline: true, readOnly: true);
        dlg.TextField("reply", "Phản hồi của khách sạn", J.StrOrNull(row, "reply"), required: true, multiline: true);
        dlg.OnSubmit = v => Api.PutJsonAsync($"v1/reviews/{J.Long(row, "id")}/reply", new { reply = v["reply"] });
        return await Task.FromResult(dlg.ShowFor(Owner));
    }

    private async Task<bool> ToggleReviewAsync(JsonElement row)
    {
        var status = J.Str(row, "status") == "AN" ? "HIEN_THI" : "AN";
        if (!Ui.Confirm(Owner, status == "AN" ? "Ẩn đánh giá này khỏi website?" : "Hiển thị lại đánh giá này trên website?")) return false;
        await Api.PatchJsonAsync($"v1/reviews/{J.Long(row, "id")}/status", new { status });
        return true;
    }
}
