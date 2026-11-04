using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Forms.Dialogs;

/// <summary>
/// Hộp thư thông báo của người đang đăng nhập:
///  • lọc theo nhóm (đặt phòng / thanh toán / vận hành) và "chỉ chưa đọc";
///  • xem chi tiết, tự đánh dấu đã đọc, mở booking liên quan;
///  • đánh dấu tất cả đã đọc; cài đặt loại thông báo muốn nhận;
///  • gửi thông báo nội bộ (người có quyền NOTIFICATION_WRITE).
/// </summary>
public sealed class NotificationsDialog : DialogBase
{
    private readonly ApiClient api;
    private readonly UserSession session;
    private readonly ComboBox group = Inputs.Combo([
        new Choice<string?>("Tất cả loại", null),
        new Choice<string?>("Đặt phòng", "BOOKING"),
        new Choice<string?>("Thanh toán", "PAYMENT"),
        new Choice<string?>("Vận hành & nội bộ", "OPERATIONS")]);
    private readonly CheckBox unreadOnly = new() { Text = "Chỉ chưa đọc", AutoSize = true, Font = AppTheme.Body };

    public NotificationsDialog(ApiClient api, UserSession session) : base("Thông báo", 1000, 640)
    {
        this.api = api;
        this.session = session;
        var view = new DataView(session, "Hộp thư thông báo")
        {
            Dock = DockStyle.Fill, Paged = true, IdPath = "notification.id",
            Loader = q => api.GetJsonAsync(ApiClient.Url("v1/notifications", ("page", q.Page), ("pageSize", q.PageSize),
                ("unreadOnly", unreadOnly.Checked ? (object?)true : null), ("group", (group.SelectedItem as Choice<string?>)?.Value)))
        };
        group.Width = 170;
        group.SelectedIndex = 0;
        group.SelectionChangeCommitted += async (_, _) => { view.ResetPage(); await view.ReloadAsync(); };
        unreadOnly.CheckedChanged += async (_, _) => { view.ResetPage(); await view.ReloadAsync(); };
        view.AddFilter(group);
        view.AddFilter(unreadOnly);

        view.Columns(
            Col.Custom("", e => J.Bool(e, "read") ? "" : "● Mới", 40),
            Col.Time("Thời gian", "notification.created_at", 75),
            Col.Status("Loại", "notification.loai_thong_bao", 80),
            Col.Custom("Tiêu đề", e => (J.Str(e, "notification.muc_do") == "QUAN_TRONG" ? "❗ " : "") + J.Str(e, "notification.tieu_de"), 170),
            Col.Text("Nội dung", "notification.noi_dung", 260));

        view.AddAction("Gửi thông báo", _ => ComposeAsync(), "NOTIFICATION_WRITE", needsRow: false)
            .AddAction("Cài đặt", _ => SettingsAsync(), needsRow: false)
            .AddAction("Đánh dấu tất cả đã đọc", async _ => { await api.PostJsonAsync("v1/notifications/read-all"); return true; }, needsRow: false)
            .AddAction("Xem", ReadAsync, primary: true);
        view.DefaultAction = ReadAsync;

        Body.Controls.Add(view);
        AddCancelButton("Đóng");
        Shown += async (_, _) => await view.ReloadAsync();
    }

    /// <summary>Xem chi tiết, đánh dấu đã đọc; có booking liên quan thì hỏi mở chi tiết booking.</summary>
    private async Task<bool> ReadAsync(JsonElement? row)
    {
        if (row is not { } item) return false;
        var bookingId = J.LongOrNull(item, "notification.dat_phong_id");
        using (var detail = new DetailDialog(J.Str(item, "notification.tieu_de"), Labels.Of(J.StrOrNull(item, "notification.loai_thong_bao")), 720, 420))
        {
            detail.Info(
                ("Thời gian", J.TimeText(J.Time(item, "notification.created_at"))),
                ("Mức độ", Labels.Of(J.StrOrNull(item, "notification.muc_do"))),
                ("Nội dung", J.Str(item, "notification.noi_dung")));
            detail.ShowDialog(this);
        }
        if (!J.Bool(item, "read")) await api.PostJsonAsync($"v1/notifications/{J.Long(item, "notification.id")}:read");
        if (bookingId is { } id && session.HasPermission("BOOKING_READ") && Ui.Confirm(this, "Thông báo này gắn với một booking. Mở chi tiết booking?"))
        {
            using var booking = new DlgBookingDetail(api, session, id);
            booking.ShowDialog(this);
        }
        return true;
    }

    /// <summary>Chọn loại thông báo muốn nhận (lưu theo tài khoản).</summary>
    private async Task<bool> SettingsAsync()
    {
        var s = await api.GetJsonAsync("v1/notifications/settings");
        using var dlg = new FormDialog("Cài đặt thông báo", 520, "Bỏ chọn nhóm nào thì thông báo mới thuộc nhóm đó không vào hộp thư của bạn.", columns: 1);
        dlg.Check("booking", "Đặt phòng: đặt mới, xác nhận cọc, hủy, nhắc lịch đến/đi, quá giờ trả phòng", J.Bool(s, "nhan_booking"));
        dlg.Check("payment", "Thanh toán: đơn QR, hóa đơn", J.Bool(s, "nhan_thanh_toan"));
        dlg.Check("operations", "Vận hành & nội bộ: buồng phòng, sự cố, kho, dịch vụ, thông báo quản lý gửi", J.Bool(s, "nhan_van_hanh"));
        dlg.Fixed("email", J.Bool(s, "cho_phep_email"));
        dlg.Fixed("push", J.Bool(s, "cho_phep_push"));
        dlg.OnSubmit = v => api.PutJsonAsync("v1/notifications/settings", v);
        if (dlg.ShowFor(this)) Ui.Info(this, "Đã lưu cài đặt thông báo.");
        return false;
    }

    /// <summary>Soạn và gửi thông báo nội bộ tới tất cả nhân viên, một vai trò hoặc một nhân viên.</summary>
    private async Task<bool> ComposeAsync()
    {
        var data = await api.GetJsonAsync("v1/notifications/recipients");
        var roles = J.Items(data, "roles");
        var staff = J.Items(data, "staff");
        var targets = new List<(string Text, string Value)> { ("Tất cả nhân viên (mọi vai trò)", "all") };
        targets.AddRange(roles.Select(r => ($"Vai trò: {J.Str(r, "name")} ({J.Long(r, "members")} người)", $"role:{J.Long(r, "id")}")));
        targets.AddRange(staff.Select(p => ($"Nhân viên: {J.Str(p, "name")} · {J.Str(p, "position")}", $"acc:{J.Long(p, "account_id")}")));

        using var dlg = new FormDialog("Gửi thông báo nội bộ", 620, "Người nhận thấy thông báo trong hộp thư và popup trên app.", columns: 1);
        dlg.Combo("to", "Gửi tới", targets, "all", required: true);
        dlg.TextField("title", "Tiêu đề", null, required: true);
        dlg.TextField("content", "Nội dung", null, required: true, multiline: true);
        dlg.Check("important", "Đánh dấu quan trọng (hiện màu đỏ)", false);
        dlg.Validator = v => ((string?)v["title"])?.Length > 255 ? "Tiêu đề tối đa 255 ký tự." : ((string?)v["content"])?.Length > 3000 ? "Nội dung tối đa 3000 ký tự." : null;
        long sent = 0;
        dlg.OnSubmit = async v =>
        {
            var to = (string)v["to"]!;
            var roleIds = to == "all" ? roles.Select(r => J.Long(r, "id")).ToArray()
                : to.StartsWith("role:", StringComparison.Ordinal) ? new[] { long.Parse(to[5..]) } : Array.Empty<long>();
            var accountIds = to.StartsWith("acc:", StringComparison.Ordinal) ? new[] { long.Parse(to[4..]) } : Array.Empty<long>();
            var result = await api.PostJsonAsync("v1/notifications", new
            {
                title = v["title"], content = v["content"], role_ids = roleIds, account_ids = accountIds, important = v["important"]
            });
            sent = J.Long(result, "recipients");
        };
        if (!dlg.ShowFor(this)) return false;
        Ui.Info(this, $"Đã gửi thông báo tới {sent} người.");
        return true;
    }
}
