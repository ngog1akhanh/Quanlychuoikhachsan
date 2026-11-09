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
        ClearPromotionCore();
        Changed?.Invoke();
    }

    // ---- Ưu đãi: mỗi booking tối đa 1 ưu đãi; giá sau ưu đãi do API báo cho từng phòng ----
    public long? PromotionId { get; private set; }
    public string? PromotionName { get; private set; }
    private readonly Dictionary<long, decimal> _promoTotals = new();

    /// <summary>Tổng tiền kỳ lưu trú của một phòng sau ưu đãi (null = chưa có / không có ưu đãi).</summary>
    public decimal? PromoTotal(long roomId) => _promoTotals.TryGetValue(roomId, out var v) ? v : null;

    public void SetPromotion(long id, string name, IEnumerable<RoomQuote> quotes)
    {
        if (PromotionId != id) _promoTotals.Clear();
        PromotionId = id;
        PromotionName = name;
        foreach (var q in quotes) _promoTotals[q.RoomId] = q.Total;
        Changed?.Invoke();
    }

    public void ClearPromotion()
    {
        ClearPromotionCore();
        Changed?.Invoke();
    }

    private void ClearPromotionCore()
    {
        PromotionId = null;
        PromotionName = null;
        _promoTotals.Clear();
    }

    /// <summary>Chọn đúng <paramref name="rooms"/> cho nhóm phòng <paramref name="groupKey"/> (bỏ các phòng khác cùng nhóm).</summary>
    public void SelectGroup(string groupKey, IEnumerable<PublicRoom> rooms)
    {
        foreach (var id in _selected.Values.Where(r => r.GroupKey == groupKey).Select(r => r.Id).ToList()) _selected.Remove(id);
        foreach (var r in rooms) _selected[r.Id] = r;
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

    /// <summary>Tổng giá gốc (không ưu đãi).</summary>
    public decimal EstimatedBaseTotal => _selected.Values.Sum(r => r.UnitPrice) * Query.Nights;
    /// <summary>Tổng tạm tính: phòng nào có giá ưu đãi thì dùng giá đó.</summary>
    public decimal EstimatedTotal => _selected.Values.Sum(r => PromoTotal(r.Id) ?? r.UnitPrice * Query.Nights);
    public decimal EstimatedDeposit => Math.Round(EstimatedTotal * HotelInfo.DepositRate, 0);

    public void ClearSelection()
    {
        _selected.Clear();
        CurrentBooking = null;
        ClearPromotionCore();
        Changed?.Invoke();
    }
}
