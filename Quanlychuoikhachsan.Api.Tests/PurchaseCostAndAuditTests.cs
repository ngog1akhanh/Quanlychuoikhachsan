using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Quanlychuoikhachsan.Api.Controllers;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Auditing;
using Quanlychuoikhachsan.Infrastructure.Persistence;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed class PurchaseCostAndAuditTests
{
    [Fact]
    public async Task Purchase_cost_counts_only_confirmed_orders_in_range_and_matches_supplier_totals()
    {
        await using var s = new TestStore(); await s.SeedAsync();
        var date = new DateOnly(2030, 3, 10);
        var inRange = HotelTime.StartOfDayUtc(date).AddHours(3);
        s.Db.Add(new NhaCungCap { Id = 1, MaNhaCungCap = "NCC1", TenNhaCungCap = "Thực phẩm A" });
        s.Db.Add(new NhaCungCap { Id = 2, MaNhaCungCap = "NCC2", TenNhaCungCap = "Vật tư B" });
        PhieuNhap Order(long id, long supplier, string status, DateTime? confirmedAt, decimal goods, decimal discount, decimal tax, long warehouse = 1) => new()
        {
            Id = id, MaPhieuNhap = $"PN{id}", NhaCungCapId = supplier, KhoId = warehouse, NhanVienTaoId = 1, NgayNhap = inRange, XacNhanLuc = confirmedAt,
            TongTienHang = goods, ChietKhau = discount, TienThue = tax, TongThanhToan = goods - discount + tax, TrangThai = status,
            ChiTiet = [new ChiTietPhieuNhap { DanhMucHangHoaId = 1, SoLuong = 1, DonGiaNhap = goods, ThanhTien = goods }]
        };
        s.Db.AddRange(
            Order(1, 1, "DA_XAC_NHAN", inRange, 288, 10, 28),          // 306 – tính
            Order(2, 2, "DA_XAC_NHAN", inRange, 100, 0, 0),            // 100 – tính
            Order(3, 1, "NHAP", null, 500, 0, 0),                      // nháp – bỏ
            Order(4, 1, "DA_HUY", null, 700, 0, 0),                    // đã hủy – bỏ
            Order(5, 1, "DA_XAC_NHAN", inRange.AddDays(-5), 900, 0, 0), // ngoài kỳ – bỏ
            Order(6, 2, "DA_XAC_NHAN", inRange, 50, 0, 0, warehouse: 2)); // kho khác – bỏ khi lọc kho 1
        await s.Db.SaveChangesAsync();
        var reports = new OperationalReportsController(s.Db);

        var purchases = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await reports.Purchases(date, date, 1, default)).Value);
        Assert.Equal(406m, purchases.GetProperty("Total").GetDecimal());
        Assert.Equal(2, purchases.GetProperty("Orders").GetInt32());
        var suppliers = purchases.GetProperty("Suppliers").EnumerateArray().ToList();
        Assert.Equal(purchases.GetProperty("Total").GetDecimal(), suppliers.Sum(x => x.GetProperty("Total").GetDecimal()));
        Assert.Equal("Thực phẩm A", suppliers[0].GetProperty("SupplierName").GetString());
        Assert.Equal(10m, suppliers[0].GetProperty("Discount").GetDecimal());

        var allWarehouses = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await reports.Purchases(date, date, null, default)).Value);
        Assert.Equal(456m, allWarehouses.GetProperty("Total").GetDecimal());

        var inventory = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await reports.Inventory(date, date, 1, default)).Value);
        var milk = inventory.EnumerateArray().Single(x => x.GetProperty("ItemId").GetInt64() == 1);
        Assert.Equal(388m, milk.GetProperty("PurchaseValue").GetDecimal()); // 288 + 100, chưa trừ chiết khấu / cộng thuế
    }

    [Fact]
    public async Task Audit_log_records_real_ids_skips_technical_tables_and_reads_in_vietnamese()
    {
        var http = new TestStore.TestHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "test")) }
        };
        await using var db = new HotelDbContext(new DbContextOptionsBuilder<HotelDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new AuditSaveChangesInterceptor(http)).Options);

        var warehouse = new Kho { MaKho = "KHO_A", TenKho = "Kho cũ" };
        db.Add(warehouse);
        db.Add(new KhoaLichPhong { ChiTietDatPhongId = 1, PhongId = 1, NgayLuuTru = new DateOnly(2030, 1, 1), TrangThai = "HOLD", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var created = Assert.Single(await db.NhatKyHeThongs.ToListAsync()); // khóa lịch phòng không được ghi
        Assert.Equal("CREATE", created.HanhDong);
        Assert.Equal("kho", created.DoiTuong);
        Assert.True(warehouse.Id > 0);
        Assert.Equal(warehouse.Id.ToString(), created.DoiTuongId); // ID thật, không phải khóa tạm âm

        warehouse.TenKho = "Kho mới";
        await db.SaveChangesAsync();
        var updated = await db.NhatKyHeThongs.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal("UPDATE", updated.HanhDong);

        var view = AuditPresenter.Describe(updated, "Staff");
        Assert.Equal("Cập nhật", view.ActionLabel);
        Assert.Equal("Kho", view.ObjectLabel);
        var change = Assert.Single(view.Changes);
        Assert.Equal("Tên kho", change.Label);
        Assert.Equal("Kho cũ", change.Old);
        Assert.Equal("Kho mới", change.New);
        Assert.Contains("Kho cũ → Kho mới", view.Summary);

        var createdView = AuditPresenter.Describe(created, "Staff");
        Assert.Equal("KHO_A", createdView.RecordCode);
        Assert.Contains("Mã kho: KHO_A", createdView.Summary);

        // Bản ghi cũ còn khóa tạm âm → không hiển thị ID rác.
        Assert.Null(AuditPresenter.Describe(new NhatKyHeThong { HanhDong = "CREATE", DoiTuong = "kho", DoiTuongId = "-9223372036854774807" }, "Staff").DoiTuongId);
    }
}
