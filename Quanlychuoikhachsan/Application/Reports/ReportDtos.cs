namespace Quanlychuoikhachsan.Application.Reports;

public sealed record ReportRange(DateOnly From, DateOnly To);
public sealed record DashboardResponse(DateOnly From, DateOnly To, decimal RoomRevenue, decimal ServiceRevenue, decimal Compensation, decimal OutstandingInvoices, int Bookings, int OccupiedRoomNights, int AvailableRoomNights, decimal Occupancy, decimal Adr, decimal RevPar);
public sealed record RevenueReportResponse(DateOnly From, DateOnly To, decimal RoomRevenue, decimal ServiceRevenue, decimal Compensation, decimal Vat, decimal TotalRevenue);
public sealed record OccupancyReportResponse(DateOnly From, DateOnly To, int AvailableRoomNights, int OccupiedRoomNights, decimal Occupancy, decimal Adr, decimal RevPar);
