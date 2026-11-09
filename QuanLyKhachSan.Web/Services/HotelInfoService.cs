using System.Globalization;
using QuanLyKhachSan.Web.Models.Room;

namespace QuanLyKhachSan.Web.Services;

/// <summary>
/// GET /v1/public/hotel-info → thông tin khách sạn, chính sách cọc / hủy / phụ thu, chính sách giá đang giảm
/// và nội dung tự soạn (bảng noi_dung_web). Khi UseMockData = true thì dùng HotelInfo (dữ liệu mẫu trong code).
/// </summary>
public sealed class HotelInfoService
{
    private readonly ApiClient _api;
    private readonly ILogger<HotelInfoService> _logger;

    public HotelInfoService(ApiClient api, ILogger<HotelInfoService> logger)
    {
        _api = api;
        _logger = logger;
    }

    // ---- Dạng JSON API trả về (snake_case) ----
    private sealed record ApiHotel(string Name, string Address, string? Phone, string? Email, string? Website,
        string? CheckInTime, string? CheckOutTime, string? Description);
    private sealed record ApiDeposit(string Name, string Type, decimal Value);
    private sealed record ApiCancelRule(int MinimumHours, decimal RefundPercent, decimal FixedFee);
    private sealed record ApiCancellation(string Name, string? Description, List<ApiCancelRule>? Rules);
    private sealed record ApiSurcharge(string Type, string Name, int FromMinute, int? ToMinute, string Calculation, decimal Value);
    private sealed record ApiPromotionLine(string RoomTypeCode, string RoomTypeName, string AdjustmentType, decimal Value);
    private sealed record ApiPromotion(long Id, string Code, string Name, string? Description, DateOnly From, DateOnly To,
        List<int>? Weekdays, List<ApiPromotionLine>? Lines);
    private sealed record ApiContent(long Id, string Type, string Title, string Body, string? Tag, string? PeriodText, DateOnly? EventDate);
    private sealed record ApiHotelInfo(ApiHotel? Hotel, int HoldMinutes, ApiDeposit? DepositPolicy, ApiCancellation? CancellationPolicy,
        List<ApiSurcharge>? Surcharges, List<ApiPromotion>? Promotions, List<ApiContent>? Contents);

    public async Task<HotelInfoView> GetAsync(CancellationToken ct = default)
    {
        if (_api.UseMock) return Fallback();
        try
        {
            var info = await _api.GetAsync<ApiHotelInfo>("v1/public/hotel-info", ct);
            return info is null ? Fallback() : Build(info);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Không tải được /v1/public/hotel-info: {Message}", ex.Message);
            return Fallback();
        }
    }

    /// <summary>Thông tin mặc định trong code – chỉ dùng khi chạy mock hoặc API lỗi (không hiện ưu đãi / sự kiện mẫu).</summary>
    private static HotelInfoView Fallback() => new(
        HotelInfo.Name, HotelInfo.Address, HotelInfo.Phone, HotelInfo.PhoneRaw, HotelInfo.Email,
        HotelInfo.CheckInTime, HotelInfo.CheckOutTime, HotelInfo.Slogan,
        HotelInfo.Policies.Select(p => new PolicyItem(p.Title, p.Text)).ToList(),
        [], [], FromDatabase: false);

    private static HotelInfoView Build(ApiHotelInfo info)
    {
        // Tạm tính tiền cọc trên web theo đúng chính sách cọc trong DB (không cố định 30%).
        if (info.DepositPolicy is { Type: "PHAN_TRAM", Value: > 0 and <= 100 } dp) HotelInfo.DepositRate = dp.Value / 100m;
        var hotel = info.Hotel;
        var checkIn = hotel?.CheckInTime ?? HotelInfo.CheckInTime;
        var checkOut = hotel?.CheckOutTime ?? HotelInfo.CheckOutTime;
        var phone = string.IsNullOrWhiteSpace(hotel?.Phone) ? HotelInfo.Phone : hotel!.Phone!;
        var contents = info.Contents ?? [];

        var policies = new List<PolicyItem>
        {
            new("Nhận / trả phòng", $"Nhận phòng từ {checkIn}, trả phòng trước {checkOut}."
                + ((info.Surcharges?.Count ?? 0) > 0 ? " Nhận sớm hoặc trả trễ có phụ thu theo mục bên dưới." : "")),
            new("Giữ phòng & đặt cọc", $"Phòng được giữ {info.HoldMinutes} phút kể từ lúc bắt đầu đặt. Quá thời gian mà chưa cọc, "
                + "hệ thống tự giải phóng phòng. " + DepositText(info.DepositPolicy)),
            new("Hủy phòng", CancellationText(info.CancellationPolicy)),
        };
        var early = SurchargeText(info.Surcharges, "NHAN_SOM");
        var late = SurchargeText(info.Surcharges, "TRA_TRE");
        if (early is not null || late is not null)
            policies.Add(new("Phụ thu nhận sớm / trả trễ", string.Join(" ", new[] { early, late }.Where(x => x is not null))));
        policies.AddRange(contents.Where(c => c.Type == "CHINH_SACH").Select(c => new PolicyItem(c.Title, c.Body)));

        var promotions = (info.Promotions ?? []).Select(PromotionFromPricing).ToList();
        promotions.AddRange(contents.Where(c => c.Type == "UU_DAI")
            .Select(c => new PromotionItem(c.Tag ?? "ƯU ĐÃI", c.Title, c.Body, c.PeriodText ?? "")));

        var events = contents.Where(c => c.Type == "SU_KIEN")
            .Select(c => new EventItem(c.EventDate?.ToString("dd/MM/yyyy") ?? c.PeriodText ?? "Sắp diễn ra", c.Title, c.Body, c.Tag))
            .ToList();

        return new HotelInfoView(
            hotel?.Name ?? HotelInfo.Name, hotel?.Address ?? HotelInfo.Address, phone,
            new string(phone.Where(char.IsDigit).ToArray()), hotel?.Email ?? HotelInfo.Email,
            checkIn, checkOut, hotel?.Description, policies, promotions, events, FromDatabase: true);
    }

    private static string Percent(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string DepositText(ApiDeposit? p) => p?.Type switch
    {
        "PHAN_TRAM" => $"Tiền cọc {Percent(p.Value)} tổng tiền phòng.",
        "DEM_DAU" => "Tiền cọc bằng tiền phòng đêm đầu tiên.",
        "SO_TIEN_CO_DINH" => $"Tiền cọc {Fmt.Money(p.Value)} cho mỗi booking.",
        _ => "Tiền cọc theo chính sách tại thời điểm đặt phòng."
    };

    private static string CancellationText(ApiCancellation? p)
    {
        const string intro = "Khách gửi yêu cầu hủy kèm lý do, quản lý sẽ duyệt. ";
        var rules = p?.Rules?.OrderByDescending(r => r.MinimumHours).ToList();
        if (p is null || rules is null || rules.Count == 0)
            return intro + "Tiền cọc hoàn theo chính sách hủy và thời điểm gửi yêu cầu.";

        var parts = new List<string>();
        for (var i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            var when = r.MinimumHours > 0
                ? $"hủy trước từ {r.MinimumHours} giờ"
                : i > 0 ? $"hủy dưới {rules[i - 1].MinimumHours} giờ" : "hủy bất kỳ lúc nào";
            var refund = r.RefundPercent > 0 ? $"hoàn {Percent(r.RefundPercent)} tiền cọc" : "không hoàn cọc";
            if (r.FixedFee > 0) refund += $" (trừ phí {Fmt.Money(r.FixedFee)})";
            parts.Add($"{when}: {refund}");
        }
        var text = string.Join("; ", parts);
        return intro + $"Chính sách “{p.Name}” – " + char.ToLower(text[0]) + text[1..] + ". Tính theo giờ nhận phòng dự kiến.";
    }

    private static string? SurchargeText(List<ApiSurcharge>? list, string type)
    {
        var items = list?.Where(s => s.Type == type).OrderBy(s => s.FromMinute).ToList();
        if (items is null || items.Count == 0) return null;
        var label = type == "NHAN_SOM" ? "Nhận sớm" : "Trả trễ";
        var parts = items.Select(s =>
        {
            var range = s.ToMinute is null ? (s.FromMinute <= 1 ? "bất kỳ thời gian nào" : $"trên {Hours(s.FromMinute - 1)}") : $"{(s.FromMinute <= 1 ? "đến" : Hours(s.FromMinute - 1) + " –")} {Hours(s.ToMinute.Value)}";
            var value = s.Calculation == "PHAN_TRAM" ? $"{Percent(s.Value)} giá một đêm" : Fmt.Money(s.Value);
            return $"{range}: {value}";
        });
        return $"{label} {string.Join("; ", parts)}.";
    }

    private static string Hours(int minutes) => minutes % 60 == 0 ? $"{minutes / 60} giờ" : $"{minutes / 60} giờ {minutes % 60} phút";

    private static readonly string[] DayNames = ["", "T2", "T3", "T4", "T5", "T6", "T7", "CN"];

    /// <summary>
    /// Ưu đãi khách chọn được cho một loại phòng trong kỳ lưu trú: chính sách giá đang hoạt động có dòng giảm giá
    /// cho loại phòng đó và có ít nhất một đêm (theo ngày + thứ) nằm trong khoảng nhận – trả phòng.
    /// </summary>
    public async Task<IReadOnlyList<RoomPromotion>> GetRoomPromotionsAsync(string roomTypeCode, DateOnly checkIn, DateOnly checkOut, CancellationToken ct = default)
    {
        if (_api.UseMock) return [];
        ApiHotelInfo? info;
        try { info = await _api.GetAsync<ApiHotelInfo>("v1/public/hotel-info", ct); }
        catch (ApiException ex)
        {
            _logger.LogWarning("Không tải được ưu đãi: {Message}", ex.Message);
            return [];
        }
        var result = new List<RoomPromotion>();
        foreach (var p in info?.Promotions ?? [])
        {
            var line = p.Lines?.FirstOrDefault(l => string.Equals(l.RoomTypeCode, roomTypeCode, StringComparison.OrdinalIgnoreCase));
            var days = p.Weekdays ?? [];
            if (line is null) continue;
            var nights = 0;
            for (var d = checkIn; d < checkOut; d = d.AddDays(1))
            {
                var weekday = d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;
                if (d >= p.From && d <= p.To && days.Contains(weekday)) nights++;
            }
            if (nights == 0) continue;
            var amount = line.AdjustmentType == "GIAM_%" ? Percent(line.Value) : Fmt.Money(line.Value) + "/đêm";
            var dayText = days.Count is 0 or 7 ? "mọi đêm" : "đêm " + string.Join(", ", days.Where(x => x is >= 1 and <= 7).Select(x => DayNames[x]));
            var total = checkOut.DayNumber - checkIn.DayNumber;
            var summary = $"Giảm {amount} · {dayText}" + (nights < total ? $" · áp dụng {nights}/{total} đêm của bạn" : "");
            result.Add(new RoomPromotion(p.Id, p.Name, summary, $"Đến hết {p.To:dd/MM/yyyy}"));
        }
        return result;
    }

    private static PromotionItem PromotionFromPricing(ApiPromotion p)
    {
        var lines = p.Lines ?? [];
        var discounts = lines.GroupBy(l => (l.AdjustmentType, l.Value)).Select(g =>
        {
            var amount = g.Key.AdjustmentType == "GIAM_%" ? Percent(g.Key.Value) : Fmt.Money(g.Key.Value) + "/đêm";
            return $"Giảm {amount} cho {string.Join(", ", g.Select(l => l.RoomTypeName))}";
        });
        var days = p.Weekdays ?? [];
        var dayText = days.Count is 0 or 7 ? "mọi đêm" : "các đêm " + string.Join(", ", days.Where(d => d is >= 1 and <= 7).Select(d => DayNames[d]));
        var roomTypes = string.Join(", ", lines.Select(l => l.RoomTypeName).Distinct());
        var text = string.IsNullOrWhiteSpace(p.Description)
            ? string.Join(". ", discounts) + $" – áp dụng {dayText}."
            : $"{p.Description!.Trim().TrimEnd('.')}. Áp dụng cho {roomTypes}.";
        text += " Chọn ưu đãi ở trang chi tiết phòng khi đặt.";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var period = p.From <= today ? $"Đến hết {p.To:dd/MM/yyyy}" : $"{p.From:dd/MM/yyyy} – {p.To:dd/MM/yyyy}";
        return new PromotionItem("ƯU ĐÃI", p.Name, text, period);
    }
}
