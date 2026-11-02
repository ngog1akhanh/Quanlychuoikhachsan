using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Module Nhân viên & phân quyền (mục 16): hồ sơ nhân viên + tài khoản đăng nhập, gán nhiều vai trò, khóa/mở khóa,
/// đặt lại mật khẩu; vai trò và quyền; chức vụ; nhật ký hệ thống.
/// </summary>
public sealed class StaffPage : ModulePage
{
    private readonly Lookups lookups;

    public StaffPage(ApiClient api, UserSession session) : base(api, session)
    {
        lookups = new Lookups(api);
        AddTab("Nhân viên", Staff());
        if (session.HasPermission("ROLE_READ")) AddTab("Vai trò & quyền", RolesView());
        AddTab("Chức vụ", Positions());
        if (session.HasPermission("AUDIT_READ")) AddTab("Nhật ký hệ thống", Audit());
        BuildTabs();
    }

    public override string Title => "Nhân viên & phân quyền";

    private IWin32Window? Owner => FindForm();

    // =====================================================================
    // Nhân viên
    // =====================================================================
    private DataView Staff()
    {
        var view = new DataView(Session, "Danh sách nhân viên")
        {
            Paged = true,
            Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/app/staff", ("page", q.Page), ("pageSize", q.PageSize), ("search", q.Search), ("status", q.Status)))
        };
        view.AddSearch("Tên, mã, email…")
            .AddStatusFilter(null, "DANG_LAM", "TAM_NGHI", "NGHI_VIEC")
            .Columns(
                Col.Text("Mã NV", "staff_code", 55),
                Col.Text("Họ tên", "full_name", 115),
                Col.Text("Chức vụ", "position", 80),
                Col.Text("Email đăng nhập", "email", 120),
                Col.Text("Điện thoại", "phone_number", 75),
                Col.Text("Vai trò", "roles", 120),
                Col.Status("Tài khoản", "account_status", 75),
                Col.Time("Đăng nhập cuối", "last_login", 85),
                Col.Status("Trạng thái", "status", 75))
            .AddAction("Đặt lại mật khẩu", r => ResetPasswordAsync(r!.Value), "ACCOUNT_MANAGE")
            .AddAction("Khóa / mở khóa", r => ToggleAccountAsync(r!.Value), "ACCOUNT_MANAGE")
            .AddAction("Gán vai trò", r => AssignRolesAsync(r!.Value), "ROLE_WRITE")
            .AddAction("Sửa", r => EditStaffAsync(r!.Value), "STAFF_WRITE")
            .AddAction("Thêm nhân viên", _ => CreateStaffAsync(), "STAFF_WRITE", needsRow: false, primary: true);
        if (Session.HasPermission("STAFF_WRITE")) view.DefaultAction = r => EditStaffAsync(r!.Value);
        return view;
    }

    private async Task<bool> CreateStaffAsync()
    {
        var positions = await lookups.PositionsAsync();
        if (positions.Count == 0) { Ui.Error(Owner, "Chưa có chức vụ đang hoạt động. Thêm chức vụ trước."); return false; }
        JsonElement created = default;
        var dlg = new FormDialog("Thêm nhân viên", 600, "Tạo hồ sơ nhân viên kèm tài khoản đăng nhập App quản lý.");
        dlg.TextField("code", "Mã nhân viên", null, required: true);
        dlg.TextField("full_name", "Họ tên", null, required: true);
        dlg.Combo("position_id", "Chức vụ", positions, positions[0].Value);
        dlg.Date("start_date", "Ngày vào làm", DateTime.Today, nullable: true);
        dlg.TextField("email", "Email đăng nhập", null, required: true);
        dlg.TextField("phone", "Số điện thoại", null);
        dlg.Password("password", "Mật khẩu (tối thiểu 8 ký tự)");
        dlg.Validator = v => ((string?)v["password"])?.Length < 8 ? "Mật khẩu tối thiểu 8 ký tự." : null;
        dlg.OnSubmit = async v => created = await Api.PostJsonAsync("v1/staff", v);
        if (!dlg.ShowFor(Owner)) return false;
        if (Session.HasPermission("ROLE_WRITE") && J.LongOrNull(created, "account_id") is { } accountId
            && Ui.Confirm(Owner, "Đã tạo nhân viên. Gán vai trò cho tài khoản này ngay?"))
            await ChooseRolesAsync(accountId, J.Str(created, "ho_ten"), []);
        return true;
    }

    private async Task<bool> EditStaffAsync(JsonElement row)
    {
        var positions = await lookups.PositionsAsync();
        var dlg = new FormDialog($"Sửa nhân viên {J.Str(row, "staff_code")}", 580,
            "Chuyển sang Tạm nghỉ / Nghỉ việc sẽ khóa tài khoản và đăng xuất mọi phiên.");
        dlg.TextField("full_name", "Họ tên", J.StrOrNull(row, "full_name"), required: true);
        dlg.Combo("position_id", "Chức vụ", positions, J.Long(row, "position_id"));
        dlg.Date("birth_date", "Ngày sinh", J.Day(row, "birth_date"), nullable: true);
        dlg.Codes("status", "Trạng thái", J.StrOrNull(row, "status"), "DANG_LAM", "TAM_NGHI", "NGHI_VIEC");
        dlg.TextField("address", "Địa chỉ", J.StrOrNull(row, "address"), span: 2);
        dlg.OnSubmit = v => Api.PutJsonAsync($"v1/staff/{J.Long(row, "id")}", v);
        return dlg.ShowFor(Owner);
    }

    private Task<bool> AssignRolesAsync(JsonElement row)
    {
        var current = J.Items(row, "role_ids").Select(e => e.TryGetInt64(out var id) ? id : 0).ToList();
        return ChooseRolesAsync(J.Long(row, "account_id"), J.Str(row, "full_name"), current);
    }

    private async Task<bool> ChooseRolesAsync(long accountId, string name, List<long> current)
    {
        var roles = J.Items(await Api.GetJsonAsync(Lookups.RolesUrl)).Where(r => J.Str(r, "status") == "HOAT_DONG")
            .Select(r => (J.Str(r, "role_name"), (string?)J.Str(r, "role_code"), J.Long(r, "id"), (string?)null)).ToList();
        using var dlg = new ChecklistDialog($"Vai trò của {name}", "Một nhân viên có thể có nhiều vai trò; quyền là hợp của các vai trò.", roles, current, 560, 480);
        dlg.OnSubmit = ids => Api.PutJsonAsync($"v1/accounts/{accountId}/roles", new { ids });
        return dlg.ShowDialog(Owner) == DialogResult.OK;
    }

    private async Task<bool> ToggleAccountAsync(JsonElement row)
    {
        var locked = J.Str(row, "account_status") == "KHOA";
        var status = locked ? "HOAT_DONG" : "KHOA";
        if (!Ui.Confirm(Owner, locked ? $"Mở khóa tài khoản của {J.Str(row, "full_name")}?" : $"Khóa tài khoản của {J.Str(row, "full_name")}? Nhân viên sẽ bị đăng xuất ngay."))
            return false;
        await Api.PatchJsonAsync($"v1/accounts/{J.Long(row, "account_id")}/status", new { status });
        return true;
    }

    private Task<bool> ResetPasswordAsync(JsonElement row)
    {
        var dlg = new FormDialog($"Đặt lại mật khẩu – {J.Str(row, "full_name")}", 440, J.Str(row, "email"), columns: 1, okText: "Đặt lại");
        dlg.Password("password", "Mật khẩu mới (tối thiểu 8 ký tự)");
        var confirm = dlg.Password("confirm", "Nhập lại mật khẩu");
        dlg.Validator = v => ((string?)v["password"])?.Length < 8 ? "Mật khẩu tối thiểu 8 ký tự."
            : (string?)v["password"] != (string?)v["confirm"] ? "Hai lần nhập mật khẩu không khớp." : null;
        dlg.OnSubmit = v => Api.PostJsonAsync($"v1/accounts/{J.Long(row, "account_id")}:reset-password", new { password = v["password"] });
        var ok = dlg.ShowFor(Owner);
        if (ok) Ui.Info(Owner, "Đã đặt lại mật khẩu. Hãy báo mật khẩu mới cho nhân viên.");
        return Task.FromResult(false);
    }

    // =====================================================================
    // Vai trò & quyền
    // =====================================================================
    private DataView RolesView()
    {
        var view = new DataView(Session, "Vai trò") { Loader = _ => Api.GetJsonAsync(Lookups.RolesUrl) };
        view.Columns(
                Col.Text("Mã vai trò", "role_code", 80),
                Col.Text("Tên vai trò", "role_name", 110),
                Col.Text("Mô tả", "description", 220),
                Col.Status("Trạng thái", "status", 75))
            .AddAction("Phân quyền", r => EditPermissionsAsync(r!.Value), "ROLE_WRITE", primary: true, when: r => J.Str(r, "role_code") != "ADMIN")
            .AddAction("Sửa", r => EditRoleAsync(r!.Value), "ROLE_WRITE")
            .AddAction("Thêm vai trò", _ => EditRoleAsync(null), "ROLE_WRITE", needsRow: false);
        view.DefaultAction = r => ShowPermissionsAsync(r!.Value);
        return view;
    }

    private Task<bool> EditRoleAsync(JsonElement? row)
    {
        var dlg = new FormDialog(row is null ? "Thêm vai trò" : "Sửa vai trò", 520);
        dlg.TextField("code", "Mã vai trò (VIẾT_HOA)", FormDialog.S(row, "role_code"), required: true);
        dlg.TextField("name", "Tên vai trò", FormDialog.S(row, "role_name"), required: true);
        dlg.Codes("status", "Trạng thái", FormDialog.S(row, "status") ?? "HOAT_DONG", "HOAT_DONG", "NGUNG_HOAT_DONG");
        dlg.TextField("description", "Mô tả", FormDialog.S(row, "description"), span: 2, multiline: true);
        dlg.OnSubmit = v => row is { } r ? Api.PutJsonAsync($"v1/roles/{J.Long(r, "id")}", v) : Api.PostJsonAsync("v1/roles", v);
        return Task.FromResult(dlg.ShowFor(Owner));
    }

    private async Task<bool> EditPermissionsAsync(JsonElement row)
    {
        var all = J.Items(await Api.GetJsonAsync("v1/permissions"));
        var current = J.Items(await Api.GetJsonAsync($"v1/roles/{J.Long(row, "id")}/permissions")).Select(e => J.Long(e, "quyen_id")).ToList();
        var items = all.OrderBy(p => J.Str(p, "nhom_quyen")).ThenBy(p => J.Str(p, "ma_quyen"))
            .Select(p => (J.Str(p, "ten_quyen"), (string?)J.Str(p, "ma_quyen"), J.Long(p, "id"), (string?)(J.StrOrNull(p, "nhom_quyen") ?? "KHÁC"))).ToList();
        using var dlg = new ChecklistDialog($"Quyền của vai trò {J.Str(row, "role_name")}",
            "Thay đổi có hiệu lực ở lần gọi API tiếp theo; nhân viên đăng nhập lại để menu cập nhật.", items, current, 680, 640);
        dlg.OnSubmit = ids => Api.PutJsonAsync($"v1/roles/{J.Long(row, "id")}/permissions", new { ids });
        return dlg.ShowDialog(Owner) == DialogResult.OK;
    }

    private async Task<bool> ShowPermissionsAsync(JsonElement? row)
    {
        if (row is not { } r) return false;
        var current = J.Items(await Api.GetJsonAsync($"v1/roles/{J.Long(r, "id")}/permissions"));
        using var dlg = new DetailDialog($"Vai trò {J.Str(r, "role_name")}", J.Str(r, "description"), 560, 520);
        dlg.Table($"{current.Count} quyền", current, 340, Col.Text("Mã quyền", "ma_quyen", 100));
        dlg.ShowDialog(Owner);
        return false;
    }

    // =====================================================================
    // Chức vụ
    // =====================================================================
    private DataView Positions()
    {
        var view = new DataView(Session, "Chức vụ") { Loader = _ => Api.GetJsonAsync(Lookups.PositionsUrl) };
        view.Columns(
                Col.Text("Mã", "code", 80),
                Col.Text("Tên chức vụ", "name", 160),
                Col.Status("Trạng thái", "trang_thai", 80))
            .AddAction("Thêm chức vụ", _ => AddPositionAsync(), "STAFF_WRITE", needsRow: false, primary: true);
        return view;
    }

    private Task<bool> AddPositionAsync()
    {
        var dlg = new FormDialog("Thêm chức vụ", 500);
        dlg.TextField("code", "Mã chức vụ", null, required: true);
        dlg.TextField("name", "Tên chức vụ", null, required: true);
        dlg.TextField("description", "Mô tả", null, span: 2);
        dlg.Fixed("status", "HOAT_DONG");
        dlg.OnSubmit = v => Api.PostJsonAsync("v1/positions", v);
        var ok = dlg.ShowFor(Owner);
        if (ok) lookups.Invalidate(Lookups.PositionsUrl);
        return Task.FromResult(ok);
    }

    // =====================================================================
    // Nhật ký hệ thống
    // =====================================================================
    private DataView Audit()
    {
        var view = new DataView(Session, "Nhật ký thao tác")
        {
            Paged = true,
            PageSize = 50,
            Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/audit-logs", ("page", q.Page), ("pageSize", q.PageSize), ("search", q.Search)))
        };
        view.AddSearch("Tên bảng (vd: dat_phong)…", 200)
            .Columns(
                Col.Time("Thời gian", "created_at", 80),
                Col.Text("Tài khoản", "tai_khoan_id", 50),
                Col.Text("Hành động", "hanh_dong", 60),
                Col.Text("Đối tượng", "doi_tuong", 90),
                Col.Text("ID", "doi_tuong_id", 45),
                Col.Custom("Dữ liệu mới", e => Clip(J.Str(e, "du_lieu_moi")), 260));
        view.DefaultAction = r =>
        {
            if (r is not { } e) return Task.FromResult(false);
            using var dlg = new DetailDialog("Chi tiết nhật ký", $"{J.Str(e, "hanh_dong")} {J.Str(e, "doi_tuong")} #{J.Str(e, "doi_tuong_id")}", 760, 520);
            dlg.Info(("Thời gian", J.TimeText(J.Time(e, "created_at"))), ("Tài khoản", J.Str(e, "tai_khoan_id")),
                ("Địa chỉ IP", J.StrOrNull(e, "dia_chi_ip")), ("Trình duyệt / app", J.StrOrNull(e, "user_agent")),
                ("Dữ liệu cũ", Clip(J.Str(e, "du_lieu_cu"), 600)), ("Dữ liệu mới", Clip(J.Str(e, "du_lieu_moi"), 600)));
            dlg.ShowDialog(Owner);
            return Task.FromResult(false);
        };
        return view;
    }

    private static string Clip(string s, int max = 120) => s.Length > max ? s[..max] + "…" : s;
}
