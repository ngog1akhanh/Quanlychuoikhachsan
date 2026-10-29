using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;
public sealed class SchemaMappingTests
{
    [Fact]
    public void Every_mapped_table_column_exists_in_sql_schema()
    {
        using var db = new HotelDbContext(new DbContextOptionsBuilder<HotelDbContext>().UseMySql("Server=unused;Database=test;User=test", new MySqlServerVersion(new Version(8, 0, 36))).Options);
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Quanlychuoikhachsan/Database/Scripts"));
        var sql = string.Join("\n", Directory.GetFiles(folder, "*.sql").Select(File.ReadAllText));
        var tables = Regex.Matches(sql, @"CREATE TABLE (?:IF NOT EXISTS )?(\w+)\s*\((.*?)\) ENGINE=InnoDB;", RegexOptions.Singleline)
            .ToDictionary(m => m.Groups[1].Value, m => Regex.Matches(m.Groups[2].Value, @"^\s*(\w+)\s+(?:BIGINT|VARCHAR|DECIMAL|INT|TINYINT|DATETIME|DATE|TIME|TEXT|LONGTEXT)\b", RegexOptions.Multiline).Select(c => c.Groups[1].Value).ToHashSet());
        foreach (var entity in db.Model.GetEntityTypes().Where(e => e.FindPrimaryKey() is not null))
        {
            var table = entity.GetTableName()!; Assert.True(tables.ContainsKey(table), table);
            var identifier = StoreObjectIdentifier.Table(table, null);
            foreach (var property in entity.GetProperties()) Assert.True(tables[table].Contains(property.GetColumnName(identifier)!), $"{entity.ClrType.Name}.{property.Name} -> {table}.{property.GetColumnName(identifier)}");
        }
    }
    [Fact]
    public void MySql_translates_stock_and_customer_ownership_queries()
    {
        using var db = new HotelDbContext(new DbContextOptionsBuilder<HotelDbContext>().UseMySql("Server=unused;Database=test;User=test", new MySqlServerVersion(new Version(8, 0, 36))).Options);
        var ids = new List<long> { 1, 2 };
        var stock = db.LoHangs.Where(x => ids.Contains(x.DanhMucHangHoaId)).Select(x => new { x.Id, Balance = db.GiaoDichKhos.Where(g => g.LoHangId == x.Id).Sum(g => (decimal?)g.SoLuongThayDoi) ?? 0 }).ToQueryString();
        Assert.Contains("giao_dich_kho", stock);
        Assert.Contains("khach_hang", db.DatPhongs.Where(x => x.KhachHang.TaiKhoanId == 2).ToQueryString());
        Assert.DoesNotContain("`id`", db.Set<CaiDatThongBao>().ToQueryString());
    }
}
