using QuanLyKhachSan.Web.Models.Booking;
using QuanLyKhachSan.Web.Models.Customer;
using QuanLyKhachSan.Web.Models.Payment;
using QuanLyKhachSan.Web.Models.Review;
using QuanLyKhachSan.Web.Models.Room;

namespace QuanLyKhachSan.Web.Services;

/// <summary>
/// Dữ liệu mẫu để chạy giao diện khi chưa có backend (ApiOptions.UseMockData = true).
/// Dữ liệu nằm trong bộ nhớ, mất khi khởi động lại ứng dụng.
/// </summary>
internal static class MockData
{
    private static readonly object Lock = new();
    private static long _nextBookingId = 1000;

    public static readonly List<PublicRoom> Rooms = BuildRooms();
    public static readonly List<BookingResponse> Bookings = new();
    public static readonly List<ReviewResponse> Reviews = new();
    public static readonly List<PaymentMethodItem> PaymentMethods = new()
    {
        new(1, "VIETQR", "Chuyển khoản QR (VietQR)"),
        new(2, "VNPAY", "VNPay"),
        new(3, "MOMO", "Ví MoMo"),
    };

    public static CustomerResponse Customer = new(
        "customers/1", 1, "KH0001", "Nguyễn Văn An", "an.nguyen@example.com", "0901234567",
        new DateOnly(1995, 5, 20), "NAM", "079095001234", "12 Lê Lợi, Quận 1, TP.HCM",
        DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddDays(-3));

    static MockData()
    {
        // 2 booking cũ để trang Lịch sử có dữ liệu
        var past = MakeBooking(new[] { Rooms[3] }, DateTime.Today.AddDays(-20).AddHours(14), DateTime.Today.AddDays(-18).AddHours(12), BookingStatus.CheckedOut);
        var future = MakeBooking(new[] { Rooms[7] }, DateTime.Today.AddDays(12).AddHours(14), DateTime.Today.AddDays(14).AddHours(12), BookingStatus.Booked);
        Bookings.Add(past);
        Bookings.Add(future);
    }

    private static List<PublicRoom> BuildRooms()
    {
        var list = new List<PublicRoom>();
        long id = 1;
        void Add(string no, string code, string name, decimal price, decimal sur, string floor, string view, decimal area, string desc, int adults, int kids)
            => list.Add(new PublicRoom(id++, no, code, name, price, sur, floor, view, area, desc, adults, kids));

        const string std = "Phòng tiêu chuẩn ấm cúng, đầy đủ tiện nghi cho chuyến công tác hoặc nghỉ ngắn ngày. Giường đôi, bàn làm việc, Wi-Fi miễn phí.";
        const string sup = "Không gian rộng rãi hơn với cửa sổ lớn đón nắng, khu vực tiếp khách nhỏ, phòng tắm vòi sen kính.";
        const string dlx = "Thiết kế sang trọng, nội thất gỗ ấm áp, bồn tắm nằm và ban công riêng nhìn ra thành phố.";
        const string fam = "Hai phòng ngủ thông nhau, phù hợp gia đình 4 người, có khu bếp nhỏ và phòng khách riêng.";
        const string exe = "Suite cao cấp với phòng khách tách biệt, bàn ăn, quyền sử dụng lounge và dịch vụ quản gia.";

        Add("101", "STD", "Phòng Standard", 850_000, 0, "1", "Nội khu", 24, std, 2, 1);
        Add("102", "STD", "Phòng Standard", 850_000, 0, "1", "Nội khu", 24, std, 2, 1);
        Add("201", "STD", "Phòng Standard", 850_000, 50_000, "2", "Thành phố", 24, std, 2, 1);
        Add("202", "SUP", "Phòng Superior", 1_150_000, 0, "2", "Thành phố", 28, sup, 2, 1);
        Add("203", "SUP", "Phòng Superior", 1_150_000, 0, "2", "Nội khu", 28, sup, 2, 1);
        Add("301", "SUP", "Phòng Superior", 1_150_000, 80_000, "3", "Thành phố", 28, sup, 2, 1);
        Add("302", "DLX", "Phòng Deluxe", 1_650_000, 0, "3", "Thành phố", 36, dlx, 2, 2);
        Add("303", "DLX", "Phòng Deluxe", 1_650_000, 100_000, "3", "Sông", 36, dlx, 2, 2);
        Add("401", "DLX", "Phòng Deluxe", 1_650_000, 150_000, "4", "Sông", 36, dlx, 2, 2);
        Add("402", "DLX", "Phòng Deluxe", 1_650_000, 150_000, "4", "Sông", 36, dlx, 3, 1);
        Add("501", "FAM", "Phòng Family", 2_400_000, 0, "5", "Thành phố", 55, fam, 4, 2);
        Add("502", "FAM", "Phòng Family", 2_400_000, 100_000, "5", "Sông", 55, fam, 4, 2);
        Add("601", "EXE", "Executive Suite", 3_900_000, 0, "6", "Sông", 72, exe, 3, 2);
        Add("602", "EXE", "Executive Suite", 3_900_000, 300_000, "6", "Toàn cảnh", 72, exe, 3, 2);
        return list;
    }

    public static List<PublicRoom> Search(RoomSearchQuery q)
    {
        return Rooms
            .Where(r => r.MaxAdults >= q.Adults && r.MaxChildren >= q.Children)
            .OrderBy(r => r.UnitPrice).ThenBy(r => r.RoomNumber)
            .ToList();
    }

    public static BookingResponse MakeBooking(IEnumerable<PublicRoom> rooms, DateTime checkIn, DateTime checkOut, string status)
    {
        var roomList = rooms.ToList();
        var nights = new List<NightPrice>();
        for (var d = DateOnly.FromDateTime(checkIn); d < DateOnly.FromDateTime(checkOut); d = d.AddDays(1))
            foreach (var r in roomList)
                nights.Add(new NightPrice(d, r.BaseNightlyPrice, r.NightSurcharge, null, 0, r.UnitPrice));

        long id;
        lock (Lock) id = _nextBookingId++;
        return new BookingResponse($"bookings/{id}", id, $"BK{DateTime.Now:yyMMdd}{id}", status, checkIn, checkOut,
            nights.Sum(n => n.FinalPrice),
            status == BookingStatus.Hold ? DateTime.UtcNow.AddMinutes(HotelInfo.HoldMinutes) : null,
            nights);
    }

    public static BookingListItem ToListItem(BookingResponse b)
        => new(b.Id, b.Name, b.BookingCode, b.Status, b.CheckIn, b.CheckOut, b.TotalRoomAmount);
}
