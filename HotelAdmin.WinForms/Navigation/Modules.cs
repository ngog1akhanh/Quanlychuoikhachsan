using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Navigation;

/// <summary>Trạng thái hỗ trợ của một API cho module.</summary>
public sealed record ApiEndpoint(string Route, bool Available);

/// <summary>
/// Định nghĩa một module của App quản lý (mục B trong tài liệu chức năng).
/// Roles rỗng = mọi nhân viên đều thấy.
/// </summary>
public sealed record ModuleDef(
    string Key,
    string Glyph,
    string Text,
    string Group,
    string[] Roles,
    string Description,
    string[] Features,
    ApiEndpoint[] Apis);

public static class Modules
{
    public const string Dashboard = "dashboard";

    private static ApiEndpoint Has(string route) => new(route, true);

    public static readonly IReadOnlyList<ModuleDef> All =
    [
        new(Dashboard, Glyph.Home, "Tổng quan", "VẬN HÀNH", [],
            "Dashboard tổng quan: khách đến/đi hôm nay, tình trạng phòng, doanh thu, công suất phòng (mục 18.1).",
            [], [Has("GET /v1/reports/dashboard"), Has("GET /v1/reports/revenue"), Has("GET /v1/reports/occupancy"), Has("GET /v1/rooms"), Has("GET /v1/bookings")]),

        new("bookings", Glyph.Calendar, "Đặt phòng", "VẬN HÀNH", [Roles.Manager, Roles.Receptionist],
            "Quản lý toàn bộ vòng đời booking từ lúc tạo đến khi hoàn tất hoặc hủy (mục 7).",
            ["7.1 Xem, tra cứu booking theo mã/tên/SĐT; lọc theo trạng thái và ngày lưu trú",
             "7.2 Tạo booking tại quầy (khách gọi điện, khách vãng lai), ghi nguồn đặt và nhân viên tạo",
             "7.3 Xác nhận booking khi thu cọc tại quầy",
             "7.4 Đổi ngày lưu trú / gia hạn, tính lại giá và chênh lệch cọc",
             "7.5 Đổi phòng cho các đêm còn lại",
             "7.6 Duyệt / từ chối yêu cầu hủy, tạo phiếu hoàn cọc",
             "7.7 Đánh dấu khách không đến (no-show)",
             "7.8 Lịch sử trạng thái booking",
             "7.9 Ràng buộc chống đặt trùng phòng"],
            [Has("GET /v1/front-desk/bookings?q&status&from&to"), Has("GET /v1/front-desk/bookings/{id}"),
             Has("POST /v1/front-desk/bookings"), Has("POST /v1/front-desk/bookings/{id}:confirm-deposit"),
             Has("POST /v1/front-desk/bookings/{id}:change-dates"), Has("POST /v1/front-desk/bookings/{id}:change-room"),
             Has("POST /v1/front-desk/bookings/{id}:cancel"), Has("POST /v1/front-desk/bookings/{id}:mark-no-show"),
             Has("GET /v1/front-desk/cancellation-requests"), Has("POST /v1/front-desk/cancellation-requests/{id}:approve|:reject")]),

        new("frontdesk", Glyph.Switch, "Nhận / trả phòng", "VẬN HÀNH", [Roles.Manager, Roles.Receptionist, Roles.Housekeeping],
            "Nghiệp vụ tại quầy khi khách đến và rời khách sạn (mục 8).",
            ["8.1 Danh sách khách đến / đi trong ngày",
             "8.2 Check-in: kiểm tra phòng Sẵn sàng, ghi giờ nhận thực tế",
             "8.3 Khai báo khách lưu trú (CCCD/hộ chiếu)",
             "8.4 Check-out: buồng phòng kiểm tra, lập hóa đơn, thu tiền; phòng chuyển Cần dọn",
             "8.5 Phụ thu nhận sớm / trả muộn"],
            [Has("GET /v1/front-desk/day?date="), Has("PUT /v1/front-desk/bookings/{id}/guests"),
             Has("POST /v1/front-desk/bookings/{id}:check-in"), Has("GET /v1/front-desk/bookings/{id}/checkout-preview"),
             Has("POST /v1/bookings/{id}/invoice:prepare"), Has("POST /v1/invoices/{id}:pay"),
             Has("POST /v1/front-desk/bookings/{id}:check-out")]),

        new("rooms", Glyph.Rooms, "Phòng", "VẬN HÀNH", [Roles.Manager, Roles.Receptionist, Roles.Housekeeping],
            "Quản lý loại phòng, phòng vật lý, trạng thái vận hành, sơ đồ và lịch phòng (mục 9).",
            ["9.1 Quản lý loại phòng (sức chứa, diện tích, giá cơ bản, tiện nghi, hình ảnh)",
             "9.2 Quản lý phòng cụ thể (số phòng, tầng, hướng, view, phụ thu)",
             "9.3 Trạng thái vận hành: Sẵn sàng, Đang ở, Cần dọn, Đang dọn, Bảo trì, Ngừng sử dụng",
             "9.4 Sơ đồ phòng theo tầng, tô màu theo trạng thái",
             "9.5 Lịch phòng theo ngày"],
            [Has("GET /v1/room-board"), Has("GET /v1/room-board/types"), Has("GET /v1/room-calendar?from&to"),
             Has("POST /v1/rooms"), Has("PATCH /v1/rooms/{id}"), Has("POST /v1/rooms/{id}:set-status"),
             Has("POST /v1/room-types"), Has("PATCH /v1/room-types/{id}"), Has("GET /v1/rooms/{id}/price"),
             Has("GET /v1/amenities")]),

        new("housekeeping", Glyph.Check, "Buồng phòng", "VẬN HÀNH", [Roles.Manager, Roles.Receptionist, Roles.Housekeeping],
            "Đảm bảo phòng sạch, sẵn sàng phục vụ và phát hiện kịp thời hư hỏng (mục 14).",
            ["14.1 Công việc dọn phòng tự tạo khi check-out; tạo thêm việc dọn/kiểm tra",
             "14.2 Cập nhật: Chờ xử lý → Đang thực hiện → Hoàn thành (phòng về Sẵn sàng)",
             "14.3 Kiểm tra phòng khi trả phòng, gửi kết quả cho lễ tân",
             "14.4 Báo sự cố, chuyển phòng sang Bảo trì"],
            [Has("GET /v1/housekeeping-tasks"), Has("POST /v1/housekeeping-tasks/checkout"), Has("POST /v1/room-conditions"),
             Has("POST /v1/housekeeping-tasks"), Has("PATCH /v1/housekeeping-tasks/{id}/status"),
             Has("POST /v1/room-conditions")]),

        new("services", Glyph.Shop, "Dịch vụ", "VẬN HÀNH", [Roles.Manager, Roles.Receptionist, Roles.Service],
            "Dịch vụ bổ sung trong thời gian lưu trú: ăn uống, giặt ủi, spa, đưa đón... (mục 11).",
            ["11.1 Danh mục dịch vụ", "11.2 Tiếp nhận yêu cầu dịch vụ theo booking/phòng",
             "11.3 Cập nhật trạng thái yêu cầu", "11.4 Ghi nhận sử dụng (lưu giá tại thời điểm dùng)",
             "11.5 Hóa đơn dịch vụ riêng"],
            [Has("GET/POST /v1/services"), Has("GET/POST /v1/service-requests"),
             Has("PATCH /v1/service-requests/{id}/status"), Has("GET /v1/service-invoices")]),

        new("billing", Glyph.Card, "Hóa đơn & thanh toán", "TÀI CHÍNH", [Roles.Manager, Roles.Receptionist],
            "Quản lý tiền cọc, hóa đơn, thu tiền, hoàn tiền có thể truy vết (mục 12).",
            ["12.1 Phiếu cọc", "12.2–12.3 Hóa đơn tiền phòng và chi tiết",
             "12.4 Thanh toán nhiều lần, nhiều phương thức", "12.5 Trạng thái hóa đơn",
             "12.6 In và gửi hóa đơn", "12.7 Hoàn tiền", "12.8 Không xóa vật lý, hủy và lập hóa đơn thay thế",
             "12.9 Đối soát cuối ca"],
            [Has("GET /v1/invoices"), Has("POST /v1/invoices/{id}:pay"), Has("GET /v1/transactions"),
             Has("POST /v1/invoices/{id}/vietqr-payment"), Has("GET /v1/payments/vietqr"),
             Has("GET /v1/invoices/{id}"), Has("POST /v1/invoices/{id}:void"),
             Has("GET /v1/deposits"), Has("POST /v1/refunds"), Has("GET /v1/shifts/{id}/reconciliation")]),

        new("pricing", Glyph.Calculator, "Giá phòng", "TÀI CHÍNH", [Roles.Manager],
            "Thiết lập giá linh hoạt theo thời gian, cuối tuần, ngày lễ, mùa (mục 10).",
            ["10.1 Chính sách giá theo khoảng thời gian / thứ trong tuần / ngày lễ",
             "10.2 Giá từng đêm được chốt vào booking tại thời điểm đặt"],
            [Has("GET /v1/rooms/{id}/price (xem trước giá)"), Has("GET/POST /v1/pricing-policies")]),

        new("customers", Glyph.Contact, "Khách hàng", "KHÁCH HÀNG", [Roles.Manager, Roles.Receptionist],
            "Hồ sơ khách hàng, lịch sử lưu trú và đánh giá (mục 13).",
            ["13.1 Thêm, sửa, tìm kiếm hồ sơ (cả khách vãng lai)", "13.2 Thông tin cá nhân, CCCD/hộ chiếu",
             "13.3 Lịch sử đặt phòng, lưu trú, dịch vụ, thanh toán", "13.4 Xem, phản hồi, ẩn đánh giá"],
            [Has("GET /v1/customers"), Has("GET /v1/customers/{id}"), Has("PATCH /v1/customers/{id}"),
             Has("POST /v1/customers"), Has("GET /v1/customers/{id}/history"),
             Has("GET /v1/reviews"), Has("PUT /v1/reviews/{id}/reply"), Has("PATCH /v1/reviews/{id}/status")]),

        new("inventory", Glyph.Package, "Kho & tài sản", "KHO", [Roles.Manager, Roles.Warehouse],
            "Cơ sở vật chất, vật tư tiêu hao, hàng minibar (mục 15).",
            ["15.1 Nhà cung cấp", "15.2 Danh mục vật tư, tài sản", "15.3 Nhập kho", "15.4 Xuất kho",
             "15.5 Tồn kho và cảnh báo dưới mức tối thiểu", "15.6 Hư hỏng, mất tài sản (liên kết bồi thường)"],
            [Has("GET /v1/inventory-items"), Has("POST /v1/inventory-items"), Has("POST /v1/purchase-orders"),
             Has("POST /v1/purchase-orders/{id}:confirm"), Has("POST /v1/stock-issues"), Has("POST /v1/stock-issues/{id}:confirm"),
             Has("GET /v1/warehouses/{w}/inventory-items/{i}/balance"),
             Has("GET/POST /v1/suppliers"), Has("GET /v1/purchase-orders"), Has("GET /v1/stock-issues"),
             Has("GET /v1/inventory-alerts")]),

        new("staff", Glyph.People, "Nhân viên & phân quyền", "HỆ THỐNG", [Roles.Manager],
            "Quản lý nhân sự, tài khoản, vai trò và nhật ký thao tác (mục 16).",
            ["16.1 Thông tin nhân viên", "16.2 Tài khoản: tạo, đặt lại mật khẩu, khóa/mở",
             "16.3 Vai trò và quyền", "16.4 Gán nhiều vai trò cho một tài khoản",
             "16.5 Kiểm tra quyền tại backend", "16.6 Nhật ký thao tác"],
            [Has("GET /v1/staff"), Has("GET /v1/roles"), Has("GET /v1/me"),
             Has("POST /v1/staff"), Has("POST /v1/accounts/{id}:reset-password"), Has("PATCH /v1/accounts/{id}/status"),
             Has("PUT /v1/accounts/{id}/roles"), Has("GET /v1/audit-logs")]),

        new("reports", Glyph.Chart, "Báo cáo", "HỆ THỐNG", [Roles.Manager],
            "Số liệu phục vụ theo dõi và ra quyết định kinh doanh (mục 18).",
            ["18.2 Doanh thu tiền phòng / dịch vụ theo kỳ, loại phòng, phương thức",
             "18.3 Công suất phòng, ADR, RevPAR", "18.4 Đặt phòng và hủy phòng",
             "18.5 Dịch vụ, khách mới / quay lại, điểm đánh giá", "18.6 Nhập – xuất – tồn kho"],
            [Has("GET /v1/reports/dashboard"), Has("GET /v1/reports/revenue"), Has("GET /v1/reports/occupancy"),
             Has("GET /v1/reports/revenue-breakdown"), Has("GET /v1/reports/bookings"),
             Has("GET /v1/reports/customers"), Has("GET /v1/reports/inventory")]),
    ];
}
