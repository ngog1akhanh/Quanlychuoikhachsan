namespace QuanLyKhachSan.Web.Models.Room;

/// <summary>GET /v1/public/rooms/availability – từng phòng vật lý còn trống (đã ghép chi tiết phòng).</summary>
public sealed record PublicRoom(
    long Id,
    string RoomNumber,
    string RoomTypeCode,
    string RoomTypeName,
    decimal BaseNightlyPrice,
    decimal NightSurcharge,
    string? Floor,
    string? ViewType,
    decimal? AreaM2,
    string? Description,
    int MaxAdults,
    int MaxChildren,
    IReadOnlyList<QuotedNight>? NightPrices = null,
    string? BedDescription = null)
{
    /// <summary>Giá 1 đêm = giá cơ bản + phụ thu riêng của phòng.</summary>
    public string BedLabel => string.IsNullOrWhiteSpace(BedDescription) ? "Đang cập nhật loại giường" : BedDescription;
    public string VariantName => RoomTypeName + (BedDescription switch
    {
        "1 giường đơn" => " Single",
        "2 giường đơn" => " Twin",
        "1 giường đôi" => " Double",
        null or "" => "",
        _ => " · " + BedDescription
    });
    public string GroupKey => RoomTypeCode + "|" + BedDescription;
    public decimal UnitPrice => BaseNightlyPrice + NightSurcharge;
}

public sealed record QuotedNight(DateOnly StayDate, decimal FinalPrice);

/// <summary>Điều kiện tìm phòng (dùng chung Home / RoomList).</summary>
public sealed class RoomSearchQuery
{
    public DateOnly CheckIn { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
    public DateOnly CheckOut { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
    public int Adults { get; set; } = 2;
    public int Children { get; set; } = 0;

    public int Nights => Math.Max(0, CheckOut.DayNumber - CheckIn.DayNumber);
    public bool IsValid => CheckIn >= DateOnly.FromDateTime(DateTime.Today) && CheckOut > CheckIn && Adults >= 1;

    public RoomSearchQuery Clone() => new()
    {
        CheckIn = CheckIn,
        CheckOut = CheckOut,
        Adults = Adults,
        Children = Children
    };

    /// <summary>Chuỗi query dùng cho URL: checkIn=...&amp;checkOut=...&amp;adults=..&amp;children=..</summary>
    public string ToQueryString()
        => $"checkIn={CheckIn:yyyy-MM-dd}&checkOut={CheckOut:yyyy-MM-dd}&adults={Adults}&children={Children}";

    /// <summary>Đọc query từ URL; giá trị thiếu/sai sẽ dùng mặc định.</summary>
    public static RoomSearchQuery FromUrl(string? checkIn, string? checkOut, int? adults, int? children)
    {
        var q = new RoomSearchQuery();
        if (DateOnly.TryParseExact(checkIn, "yyyy-MM-dd", out var ci)) q.CheckIn = ci;
        if (DateOnly.TryParseExact(checkOut, "yyyy-MM-dd", out var co)) q.CheckOut = co;
        if (q.CheckOut <= q.CheckIn) q.CheckOut = q.CheckIn.AddDays(1);
        if (adults is >= 1 and <= 10) q.Adults = adults.Value;
        if (children is >= 0 and <= 10) q.Children = children.Value;
        return q;
    }

    public bool SameAs(RoomSearchQuery o)
        => CheckIn == o.CheckIn && CheckOut == o.CheckOut && Adults == o.Adults && Children == o.Children;
}

/// <summary>Thông tin tĩnh của khách sạn (API chưa có endpoint). Sửa theo thông tin thật.</summary>
public static class HotelInfo
{
    public const string Name = "Grand Saigon Hotel";
    public const string Slogan = "Nghỉ dưỡng đẳng cấp giữa lòng thành phố";
    public const string Address = "123 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh";
    public const string Phone = "1900 1234";
    public const string PhoneRaw = "19001234";
    public const string Email = "booking@grandsaigon.vn";
    public const string CheckInTime = "14:00";
    public const string CheckOutTime = "12:00";
    public const int HoldMinutes = 15;
    /// <summary>
    /// Tỷ lệ cọc để TẠM TÍNH trước khi giữ phòng. Mặc định 30%, được cập nhật theo chính sách cọc trong DB
    /// mỗi khi tải /v1/public/hotel-info (HotelInfoService). Sau khi giữ phòng luôn dùng BookingResponse.RequiredDeposit.
    /// </summary>
    public static decimal DepositRate { get; set; } = 0.3m;

    public static readonly (string Title, string Text)[] Policies =
    {
        ("Nhận / trả phòng", "Nhận phòng từ 14:00, trả phòng trước 12:00. Nhận sớm hoặc trả trễ có thể phát sinh phụ thu theo quy định."),
        ("Giữ phòng & đặt cọc", "Phòng được giữ 15 phút kể từ lúc bắt đầu đặt. Quá thời gian mà chưa cọc, hệ thống tự giải phóng phòng. Tiền cọc 30% tổng tiền phòng."),
        ("Hủy phòng", "Khách gửi yêu cầu hủy kèm lý do, quản lý sẽ duyệt. Tiền cọc hoàn theo chính sách hủy và thời điểm gửi yêu cầu."),
        ("Đổi ngày / đổi phòng", "Khách chỉ được sửa thông tin liên hệ. Việc đổi ngày hoặc đổi phòng cần gửi yêu cầu để nhân viên xử lý."),
    };
    public static readonly (string Tag, string Title, string Text, string Period)[] Promotions =
{
    ("ƯU ĐÃI", "Ở dài ngày – tiết kiệm hơn", "Giảm 10% tổng tiền phòng khi đặt từ 3 đêm liên tiếp.", "Áp dụng quanh năm"),
    ("ƯU ĐÃI", "Đặt sớm nhận quà", "Đặt trước ngày nhận phòng 30 ngày để nhận bữa sáng cho 2 khách.", "Đến hết 31/12/2026"),
    ("THÀNH VIÊN", "Khách hàng thân thiết", "Tích lũy điểm sau mỗi kỳ nghỉ và đổi lấy ưu đãi cho lần đặt tiếp theo.", "Dành cho tài khoản đã đăng ký"),
};

    public static readonly (string Date, string Title, string Text)[] Events =
    {
    ("24/12/2026", "Đêm Giáng sinh tại sảnh chính", "Âm nhạc sống, tiệc nhẹ và không gian trang trí mùa lễ hội dành cho khách lưu trú."),
    ("31/12/2026", "Countdown chào năm mới", "Cùng đón giao thừa trên sân thượng với tầm nhìn toàn cảnh thành phố."),
};
}

/// <summary>Báo giá API trả cho một phòng (tổng kỳ lưu trú + từng đêm), dùng khi áp dụng ưu đãi.</summary>
public sealed record RoomQuote(long RoomId, decimal Total, IReadOnlyList<QuotedNight> Nights);
