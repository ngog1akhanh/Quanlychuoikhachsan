namespace Quanlychuoikhachsan.Application.Common;

public static class HotelTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    public static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Unspecified
        ? TimeZoneInfo.ConvertTimeToUtc(value, Zone) : value.ToUniversalTime();
    public static DateTime Local(DateTime value) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), Zone);
    public static DateTime? Local(DateTime? value) => value.HasValue ? Local(value.Value) : null;
    public static DateOnly StayDate(DateTime utc) => DateOnly.FromDateTime(Local(utc));
    public static DateOnly Today => DateOnly.FromDateTime(Local(DateTime.UtcNow));
}
