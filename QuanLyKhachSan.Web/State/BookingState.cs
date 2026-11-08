using QuanLyKhachSan.Web.Models.Booking;
using QuanLyKhachSan.Web.Models.Room;

namespace QuanLyKhachSan.Web.State;

/// <summary>
/// Lưu "giỏ" đặt phòng giữa các trang: điều kiện tìm, kết quả tìm, phòng đã chọn, booking đang giữ chỗ.
/// </summary>
public sealed class BookingState
{
    public void ResetGuest()
    {
        Guest = new();
        CurrentBooking = null;
    }

    public RoomSearchQuery Query { get; set; } = new();
    public List<PublicRoom> Results { get; private set; } = new();
    public bool HasSearched { get; set; }

    private readonly Dictionary<long, PublicRoom> _selected = new();
    public IReadOnlyCollection<PublicRoom> SelectedRooms => _selected.Values;
    public int SelectedCount => _selected.Count;

    /// <summary>Booking vừa tạo (HOLD) – dùng cho Checkout / BookingDetail (API chưa có GET booking theo id).</summary>
    public BookingResponse? CurrentBooking { get; set; }
    public GuestInformation Guest { get; set; } = new();

    public event Action? Changed;

    public void SetResults(RoomSearchQuery query, IEnumerable<PublicRoom> rooms)
    {
        Query = query.Clone();
        Results = rooms.ToList();
        HasSearched = true;
        _selected.Clear();
        Changed?.Invoke();
    }

    public bool IsSelected(long roomId) => _selected.ContainsKey(roomId);

    public void Toggle(PublicRoom room)
    {
        if (!_selected.Remove(room.Id)) _selected[room.Id] = room;
        Changed?.Invoke();
    }

    public void Remove(long roomId)
    {
        if (_selected.Remove(roomId)) Changed?.Invoke();
    }

    public decimal EstimatedTotal => _selected.Values.Sum(r => r.UnitPrice) * Query.Nights;
    public decimal EstimatedDeposit => Math.Round(EstimatedTotal * HotelInfo.DepositRate, 0);

    public void ClearSelection()
    {
        _selected.Clear();
        CurrentBooking = null;
        Changed?.Invoke();
    }
}
