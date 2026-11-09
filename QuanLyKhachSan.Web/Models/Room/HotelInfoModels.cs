namespace QuanLyKhachSan.Web.Models.Room;

/// <summary>Dữ liệu đã dựng sẵn để hiển thị trang "Thông tin &amp; chính sách".</summary>
public sealed record HotelInfoView(
    string Name,
    string Address,
    string Phone,
    string PhoneRaw,
    string Email,
    string CheckInTime,
    string CheckOutTime,
    string? Description,
    IReadOnlyList<PolicyItem> Policies,
    IReadOnlyList<PromotionItem> Promotions,
    IReadOnlyList<EventItem> Events,
    // true = lấy từ database qua API; false = thông tin mặc định trong code (mock hoặc API lỗi).
    bool FromDatabase);

public sealed record PolicyItem(string Title, string Text);
public sealed record PromotionItem(string Tag, string Title, string Text, string Period);
public sealed record EventItem(string Date, string Title, string Text, string? Tag = null);

/// <summary>Ưu đãi (chính sách giá giảm) khách chọn ở trang chi tiết phòng. Mỗi booking dùng tối đa 1 ưu đãi.</summary>
public sealed record RoomPromotion(long Id, string Name, string Summary, string Period);
