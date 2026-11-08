using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Module Buồng phòng (mục 14): công việc dọn/kiểm tra phòng (tạo, phân công, bắt đầu, hoàn thành)
/// và sự cố/hư hỏng trong phòng (ghi nhận, xử lý, chi phí bồi thường).
/// Hoàn thành việc dọn sẽ đưa phòng về Sẵn sàng (do backend xử lý).
/// </summary>
public sealed class HousekeepingPage : ModulePage
{
    private readonly Lookups lookups;
    private readonly CheckBox onlyMine = new() { Text = "Chỉ việc của tôi", AutoSize = true, Font = AppTheme.Body };

    public HousekeepingPage(ApiClient api, UserSession session) : base(api, session)
    {
        lookups = new Lookups(api);
        AddTab("Công việc buồng phòng", Tasks());
        AddTab("Sự cố & hư hỏng", Conditions());
        BuildTabs();
    }

    public override string Title => "Buồng phòng";

    private IWin32Window? Owner => FindForm();

    // =====================================================================
    // Công việc
    // =====================================================================
    private DataView Tasks()
    {
        var view = new DataView(Session, "Công việc dọn / kiểm tra phòng")
        {
            Paged = true,
            Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/app/housekeeping-tasks", ("page", q.Page), ("pageSize", q.PageSize),
                ("search", q.Search), ("status", q.Status), ("mine", onlyMine.Checked ? (object?)true : null)))
        };
        // Nhân viên buồng phòng mặc định chỉ xem việc của mình.
        onlyMine.Checked = Session.HasRole(Roles.Housekeeping) && !Session.HasPermission("HOUSEKEEPING_ASSIGN");
        onlyMine.CheckedChanged += async (_, _) => { view.ResetPage(); await view.ReloadAsync(); };
        view.AddSearch("Số phòng…", 140)
            .AddStatusFilter(null, "CHO_XU_LY", "DANG_THUC_HIEN", "HOAN_THANH");
        view.AddFilter(onlyMine);
        view.Columns(
                Col.Text("Phòng", "room_number", 50),
                Col.Status("TT phòng", "room_status", 75),
                Col.Label("Loại việc", "type", 95),
                Col.Status("Ưu tiên", "priority", 70),
                Col.Text("Người làm", "assignee_name", 100),
                Col.Text("Booking", "booking_code", 75),
                Col.Time("Dự kiến", "planned_start", 80),
                Col.Time("Bắt đầu", "started_at", 80),
                Col.Time("Xong lúc", "completed_at", 80),
                Col.Text("Ghi chú", "note", 110),
                Col.Status("Trạng thái", "status", 80))
            .AddAction("Hoàn thành", r => SetTaskStatusAsync(r!.Value, "HOAN_THANH"), "HOUSEKEEPING_WRITE", primary: true,
                when: r => J.Str(r, "status") == "DANG_THUC_HIEN")
            .AddAction("Bắt đầu", r => SetTaskStatusAsync(r!.Value, "DANG_THUC_HIEN"), "HOUSEKEEPING_WRITE",
                when: r => J.Str(r, "status") == "CHO_XU_LY")
            .AddAction("Phân công", r => AssignAsync(r!.Value), "HOUSEKEEPING_ASSIGN", when: r => J.Str(r, "status") != "HOAN_THANH")
            .AddAction("Tạo công việc", _ => CreateTaskAsync(), "HOUSEKEEPING_WRITE", needsRow: false)
            .AddDetails(r => $"Công việc phòng {J.Str(r, "room_number")}");
        return view;
    }

    private async Task<bool> CreateTaskAsync()
    {
        var rooms = await lookups.RoomsAsync();
        var canAssign = Session.HasPermission("HOUSEKEEPING_ASSIGN");
        var employees = canAssign ? await lookups.EmployeesAsync() : new List<(string Text, long Value)>();
        var dlg = new FormDialog("Tạo công việc buồng phòng", 560);
        dlg.Combo("room_id", "Phòng", rooms, rooms.FirstOrDefault().Value, required: true);
        dlg.Codes("type", "Loại công việc", "DON_HANG_NGAY", "DON_HANG_NGAY", "DON_CHECKOUT", "KIEM_TRA");
        dlg.Codes("priority", "Mức ưu tiên", "BINH_THUONG", "THAP", "BINH_THUONG", "CAO", "KHAN_CAP");
        if (canAssign) dlg.Combo("assignee_id", "Giao cho", employees, null, allowEmpty: true, emptyText: "(Chưa giao)");
        else dlg.Fixed("assignee_id", null);
        dlg.DateTimeField("planned_start", "Bắt đầu dự kiến", null, nullable: true);
        dlg.TextField("note", "Ghi chú", null, span: 2, multiline: true);
        dlg.Fixed("booking_id", null);
        dlg.OnSubmit = v => Api.PostJsonAsync("v1/housekeeping-tasks", v);
        return dlg.ShowFor(Owner);
    }

    private async Task<bool> AssignAsync(JsonElement row)
    {
        var employees = await lookups.EmployeesAsync();
        var dlg = new FormDialog($"Phân công dọn phòng {J.Str(row, "room_number")}", 460, Labels.Of(J.Str(row, "type")), columns: 1);
        dlg.Combo("employee_id", "Nhân viên thực hiện", employees, J.LongOrNull(row, "assignee_id") ?? employees.FirstOrDefault().Value, required: true);
        dlg.OnSubmit = v => Api.PutJsonAsync($"v1/housekeeping-tasks/{J.Long(row, "id")}/assignee", v);
        return dlg.ShowFor(Owner);
    }

    private async Task<bool> SetTaskStatusAsync(JsonElement row, string status)
    {
        if (status == "HOAN_THANH" && !Ui.Confirm(Owner, $"Xác nhận đã hoàn thành công việc ở phòng {J.Str(row, "room_number")}?\n" +
                "Nếu là việc dọn phòng và không còn việc dọn nào khác, phòng sẽ chuyển sang Sẵn sàng."))
            return false;
        await Api.PatchJsonAsync($"v1/housekeeping-tasks/{J.Long(row, "id")}/status", new { status });
        return true;
    }

    // =====================================================================
    // Sự cố phòng
    // =====================================================================
    private DataView Conditions()
    {
        var view = new DataView(Session, "Sự cố, hư hỏng trong phòng")
        {
            Paged = true,
            Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/app/room-conditions", ("page", q.Page), ("pageSize", q.PageSize), ("status", q.Status)))
        };
        view.AddStatusFilter(null, "MOI_GHI_NHAN", "DANG_XU_LY", "DA_XU_LY")
            .Columns(
                Col.Text("Phòng", "room_number", 50),
                Col.Status("TT phòng", "room_status", 75),
                Col.Text("Booking", "booking_code", 75),
                Col.Label("Loại", "type", 85),
                Col.Status("Mức độ", "severity", 80),
                Col.Text("Mô tả", "description", 150),
                Col.Money("CP dự kiến", "estimated_cost", 70),
                Col.Money("CP thực tế", "actual_cost", 70),
                Col.Bool("Khách gây ra", "caused_by_guest", 55),
                Col.Text("Người ghi nhận", "reported_by", 90),
                Col.Time("Ghi nhận lúc", "reported_at", 80),
                Col.Status("Xử lý", "status", 80))
            .AddAction("Cập nhật xử lý", r => ResolveAsync(r!.Value), "HOUSEKEEPING_WRITE", primary: true,
                when: r => J.Str(r, "status") != "DA_XU_LY")
            .AddAction("Sửa", r => EditConditionAsync(r!.Value), "HOUSEKEEPING_WRITE", when: r => J.Str(r, "status") != "DA_XU_LY")
            .AddAction("Ghi nhận sự cố", _ => ReportAsync(), "HOUSEKEEPING_WRITE", needsRow: false)
            .AddDetails(r => $"Sự cố phòng {J.Str(r, "room_number")}",
                r => [("Xử lý xong lúc", J.TimeText(J.Time(r, "resolved_at"))), ("Ảnh minh chứng", J.StrOrNull(r, "evidence"))],
                (r, dlg) =>
                {
                    if (Session.HasPermission("HOUSEKEEPING_WRITE") && J.Str(r, "status") != "DA_XU_LY")
                        dlg.Action("Sửa", () => EditConditionAsync(r));
                });
        return view;
    }

    private async Task<bool> ReportAsync()
    {
        var board = await lookups.ListAsync(Lookups.RoomBoardUrl);
        var rooms = board.Select(e => ($"{J.Str(e, "room_number")} · {J.Str(e, "room_type_name")}", J.Long(e, "id"))).ToList();
        var stays = board.Where(e => J.LongOrNull(e, "current_booking_id") is not null)
            .Select(e => ($"{J.Str(e, "current_booking_code")} · phòng {J.Str(e, "room_number")} · {J.Str(e, "current_guest")}", J.Long(e, "current_booking_id"))).ToList();
        var dlg = new FormDialog("Ghi nhận sự cố / hư hỏng", 600,
            "Mức Nghiêm trọng sẽ chuyển phòng trống sang Bảo trì. Chi phí do khách gây ra được tính vào hóa đơn khi chốt.");
        dlg.Combo("room_id", "Phòng", rooms, rooms.FirstOrDefault().Item2, required: true);
        dlg.Combo("booking_id", "Booking liên quan", stays, null, allowEmpty: true, emptyText: "(Không gắn booking)");
        dlg.Codes("type", "Loại sự cố", "HONG_THIET_BI", "HONG_THIET_BI", "HU_HONG_NOI_THAT", "MAT_DO", "VET_BAN", "KHAC");
        dlg.Codes("severity", "Mức độ", "NHE", "NHE", "TRUNG_BINH", "NGHIEM_TRONG");
        dlg.TextField("description", "Mô tả", null, required: true, span: 2, multiline: true);
        dlg.Money("estimated_cost", "Chi phí dự kiến", 0);
        dlg.Money("actual_cost", "Chi phí thực tế", 0);
        dlg.Check("caused_by_guest", "Do khách gây ra (tính bồi thường)", false);
        dlg.TextField("evidence", "Ảnh minh chứng (đường dẫn)", null);
        dlg.Fixed("asset_id", null);
        dlg.Validator = v => (bool)v["caused_by_guest"]! && v["booking_id"] is null ? "Sự cố do khách gây ra cần chọn booking." : null;
        dlg.OnSubmit = v => Api.PostJsonAsync("v1/room-conditions", v);
        if (!dlg.ShowFor(Owner)) return false;
        lookups.Invalidate(Lookups.RoomBoardUrl);
        return true;
    }

    /// <summary>Sửa thông tin sự cố chưa xử lý xong (PUT v1/room-conditions/{id}). Chi phí thực tế nhập ở "Cập nhật xử lý".</summary>
    private async Task<bool> EditConditionAsync(JsonElement row)
    {
        var roomId = J.Long(row, "room_id");
        var board = await lookups.ListAsync(Lookups.RoomBoardUrl);
        var stays = board.Where(e => J.Long(e, "id") == roomId && J.LongOrNull(e, "current_booking_id") is not null)
            .Select(e => ($"{J.Str(e, "current_booking_code")} · {J.Str(e, "current_guest")}", J.Long(e, "current_booking_id"))).ToList();
        if (J.LongOrNull(row, "booking_id") is { } currentBooking && stays.All(s => s.Item2 != currentBooking))
            stays.Insert(0, ($"{J.Str(row, "booking_code")} (đang gắn)", currentBooking));
        var dlg = new FormDialog($"Sửa sự cố phòng {J.Str(row, "room_number")}", 600,
            "Chuyển sang mức Nghiêm trọng sẽ đưa phòng trống vào Bảo trì. Chi phí thực tế nhập khi cập nhật xử lý.");
        dlg.Combo("booking_id", "Booking liên quan", stays, J.LongOrNull(row, "booking_id"), allowEmpty: true, emptyText: "(Không gắn booking)", span: 2);
        dlg.Codes("type", "Loại sự cố", J.StrOrNull(row, "type") ?? "KHAC", "HONG_THIET_BI", "HU_HONG_NOI_THAT", "MAT_DO", "VET_BAN", "KHAC");
        dlg.Codes("severity", "Mức độ", J.StrOrNull(row, "severity") ?? "NHE", "NHE", "TRUNG_BINH", "NGHIEM_TRONG");
        dlg.TextField("description", "Mô tả", J.StrOrNull(row, "description"), required: true, span: 2, multiline: true);
        dlg.Money("estimated_cost", "Chi phí dự kiến", J.Dec(row, "estimated_cost"));
        dlg.Check("caused_by_guest", "Do khách gây ra (tính bồi thường)", J.Bool(row, "caused_by_guest"));
        dlg.TextField("evidence", "Ảnh minh chứng (đường dẫn)", J.StrOrNull(row, "evidence"), span: 2);
        dlg.Validator = v => (bool)v["caused_by_guest"]! && v["booking_id"] is null ? "Sự cố do khách gây ra cần chọn booking." : null;
        dlg.OnSubmit = v => Api.PutJsonAsync($"v1/room-conditions/{J.Long(row, "id")}", v);
        if (!dlg.ShowFor(Owner)) return false;
        lookups.Invalidate(Lookups.RoomBoardUrl);
        return true;
    }

    private Task<bool> ResolveAsync(JsonElement row)
    {
        var dlg = new FormDialog($"Xử lý sự cố phòng {J.Str(row, "room_number")}", 520, J.Str(row, "description"));
        dlg.Codes("status", "Trạng thái", "DA_XU_LY", "DANG_XU_LY", "DA_XU_LY");
        dlg.Money("actual_cost", "Chi phí thực tế", Math.Max(J.Dec(row, "actual_cost"), J.Dec(row, "estimated_cost")));
        var release = dlg.Check("release_maintenance", "Mở lại phòng sau bảo trì (chuyển sang Cần dọn)", J.Str(row, "room_status") == "BAO_TRI", span: 2);
        release.Enabled = J.Str(row, "room_status") == "BAO_TRI";
        dlg.Validator = v => (bool)v["release_maintenance"]! && (string?)v["status"] != "DA_XU_LY" ? "Chỉ mở lại phòng khi sự cố đã xử lý xong." : null;
        dlg.OnSubmit = v => Api.PatchJsonAsync($"v1/room-conditions/{J.Long(row, "id")}", v);
        return Task.FromResult(dlg.ShowFor(Owner));
    }
}
