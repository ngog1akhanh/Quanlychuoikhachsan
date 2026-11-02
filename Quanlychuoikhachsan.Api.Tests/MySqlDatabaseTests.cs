using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Inventory;
using Quanlychuoikhachsan.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Quanlychuoikhachsan.Api.Tests;

// Chỉ chạy khi chỉ định server kiểm thử; mỗi test tự tạo và xóa database riêng.
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOTEL_TEST_MYSQL")))
            Skip = "Set HOTEL_TEST_MYSQL to run against a real MySQL/MariaDB test server.";
    }
}

public sealed class MySqlDatabaseTests(ITestOutputHelper output)
{
    [MySqlFact]
    public async Task Install_matches_model_columns_keys_precision_and_permissions()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var failures = new List<string>();
        var columns = new Dictionary<string, (string Type, bool Nullable, long? Length, int? Precision, int? Scale)>();
        await using (var command = database.Command("""
            SELECT TABLE_NAME, COLUMN_NAME,
                   IF(COLUMN_TYPE LIKE '%unsigned%', CONCAT(DATA_TYPE, ' unsigned'), DATA_TYPE), IS_NULLABLE,
                   CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE
            FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
            """))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                columns.Add($"{reader.GetString(0)}.{reader.GetString(1)}", (
                    reader.GetString(2), reader.GetString(3) == "YES",
                    reader.IsDBNull(4) ? null : reader.GetInt64(4),
                    reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    reader.IsDBNull(6) ? null : reader.GetInt32(6)));
        }

        var propertyCount = 0;
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, null);
            foreach (var property in entity.GetProperties())
            {
                propertyCount++;
                var name = $"{table}.{property.GetColumnName(store)}";
                if (!columns.TryGetValue(name, out var column)) { failures.Add($"Missing {name}"); continue; }
                if (property.IsNullable != column.Nullable) failures.Add($"{name}: nullability differs");
                if (property.GetMaxLength() is int length && column.Length < length)
                    failures.Add($"{name}: SQL length {column.Length} < model {length}");
                if (property.GetPrecision() is int precision && (precision != column.Precision || property.GetScale() != column.Scale))
                    failures.Add($"{name}: SQL decimal({column.Precision},{column.Scale}) != model ({precision},{property.GetScale()})");
                var expectedType = Regex.Replace(property.GetColumnType()!, @"\(.*\)", "");
                // TEXT vẫn đọc được thành string; int của DTO có thể chứa toàn bộ miền TINYINT.
                if (expectedType != column.Type && !(expectedType == "longtext" && column.Type == "text")
                    && !(expectedType == "int" && column.Type == "tinyint"))
                    failures.Add($"{name}: SQL {column.Type} != model {expectedType}");
            }
            if (entity.FindPrimaryKey() is { } key)
            {
                var expected = string.Join(",", key.Properties.Select(p => p.GetColumnName(store)));
                var actual = await database.ScalarAsync<string>($"""
                    SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY ORDINAL_POSITION SEPARATOR ',')
                    FROM information_schema.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{table}' AND CONSTRAINT_NAME='PRIMARY'
                    """);
                if (actual != expected) failures.Add($"{table}: primary key {actual} != model {expected}");
            }
            // Thực thi SQL do provider sinh ra, bao gồm các view đã ánh xạ.
            await (Task)typeof(MySqlDatabaseTests).GetMethod(nameof(ReadEntity), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(entity.ClrType).Invoke(null, [db])!;
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        Assert.Equal(67, await database.ScalarAsync<int>("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE'"));
        Assert.Equal(5, await database.ScalarAsync<int>("SELECT COUNT(*) FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE()"));
        foreach (var view in new[] { "vw_ton_kho", "vw_ton_kho_theo_lo", "vw_hang_sap_het_han", "vw_cong_no_hoa_don_phong", "vw_cong_no_hoa_don_dich_vu" })
            await database.ExecuteAsync($"SELECT * FROM {view} LIMIT 1");
        var controllers = Path.Combine(TestDatabase.Root, "Quanlychuoikhachsan/Api/Controllers");
        var permissions = Directory.GetFiles(controllers, "*.cs")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "RequirePermission\\(\"([A-Z_]+)\"\\)").Select(m => m.Groups[1].Value)).Distinct();
        foreach (var permission in permissions)
            Assert.Equal(1, await database.ScalarAsync<int>($"SELECT COUNT(*) FROM quyen q JOIN vai_tro_quyen vq ON vq.quyen_id=q.id JOIN vai_tro v ON v.id=vq.vai_tro_id WHERE v.ma_vai_tro='ADMIN' AND q.ma_quyen='{permission}'"));
        var duplicate = await Assert.ThrowsAsync<MySqlException>(() => database.ExecuteAsync($"CREATE DATABASE `{database.Name}`"));
        Assert.Equal(1007, duplicate.Number);
        output.WriteLine($"Verified {db.Model.GetEntityTypes().Count()} entity mappings / {propertyCount} columns, 67 tables, 5 views and ADMIN permission coverage.");
    }

    [MySqlFact]
    public async Task Purchase_issue_and_stock_constraints_work_on_real_transactions()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.ExecuteAsync("""
            INSERT INTO tai_khoan(email,mat_khau_hash,loai_tai_khoan) VALUES ('test@example.invalid','test-only','NHAN_VIEN');
            INSERT INTO chuc_vu(ma_chuc_vu,ten_chuc_vu) VALUES ('TEST','Test');
            INSERT INTO nhan_vien(ma_nhan_vien,tai_khoan_id,chuc_vu_id,ho_ten) VALUES ('TEST',1,1,'Kiểm thử');
            INSERT INTO nha_cung_cap(ma_nha_cung_cap,ten_nha_cung_cap) VALUES ('TEST','Nhà cung cấp');
            INSERT INTO kho(ma_kho,ten_kho,loai_kho) VALUES ('OTHER','Kho khác','KHO_CHINH');
            INSERT INTO danh_muc_hang_hoa(ma_hang,ten_hang,loai_hang,don_vi_tinh_id,theo_doi_lo,theo_doi_han_su_dung)
              VALUES ('TEST','Sữa kiểm thử','DO_UONG',1,1,1);
            """);
        await using var store = new TestStore(database.Context());
        var purchase = await store.Inventory.CreatePurchaseAsync(new(1, 1, 999, 0, 0, [new(1, 1.125m, 100, null, null, null, null)], null), default);
        var detail = await store.Db.ChiTietPhieuNhaps.SingleAsync();
        var confirm = new ConfirmPurchaseRequest([new(detail.Id, "LOT_TEST", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), null)]);
        await store.Inventory.ConfirmPurchaseAsync(purchase.Id, confirm, default);
        await Assert.ThrowsAsync<InvalidArgumentException>(() => store.Inventory.ConfirmPurchaseAsync(purchase.Id, confirm, default));
        var issue = await store.Inventory.CreateIssueAsync(new(1, 999, "BEP", [new(1, .125m)], "Bếp", null), default);
        await store.Inventory.ConfirmIssueAsync(issue.Id, default);
        Assert.Equal(1m, await database.ScalarAsync<decimal>("SELECT so_luong_ton FROM vw_ton_kho WHERE kho_id=1 AND danh_muc_hang_hoa_id=1"));
        Assert.Equal(1m, await database.ScalarAsync<decimal>("SELECT so_luong_con FROM vw_ton_kho_theo_lo WHERE lo_hang_id=1"));
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT COUNT(*) FROM vw_hang_sap_het_han WHERE lo_hang_id=1"));

        var wrongLot = await Assert.ThrowsAsync<MySqlException>(() => database.ExecuteAsync("""
            INSERT INTO giao_dich_kho(ma_giao_dich,kho_id,danh_muc_hang_hoa_id,lo_hang_id,loai_giao_dich,so_luong_thay_doi)
            VALUES ('WRONG_LOT',2,1,1,'DIEU_CHINH',1)
            """));
        Assert.Equal(1452, wrongLot.Number);
        var badQuantity = await Assert.ThrowsAsync<MySqlException>(() => database.ExecuteAsync("""
            INSERT INTO chi_tiet_phieu_nhap(phieu_nhap_id,danh_muc_hang_hoa_id,so_luong,don_gia_nhap,thanh_tien)
            VALUES (1,1,-1,100,100)
            """));
        Assert.Contains(badQuantity.Number, new[] { 3819, 4025 });
        await database.ExecuteAsync("START TRANSACTION; UPDATE kho SET ten_kho='Rollback test' WHERE id=1; ROLLBACK;");
        Assert.Equal("Kho chính", await database.ScalarAsync<string>("SELECT ten_kho FROM kho WHERE id=1"));
        Assert.Equal(2, await store.Db.GiaoDichKhos.CountAsync());
    }

    [MySqlFact]
    public async Task Room_day_and_qr_uniqueness_and_invoice_debt_views_are_enforced()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.ExecuteAsync("""
            INSERT INTO tai_khoan(email,mat_khau_hash,loai_tai_khoan) VALUES ('guest@example.invalid','test-only','KHACH_HANG');
            INSERT INTO khach_hang(ma_khach_hang,tai_khoan_id,ho_ten) VALUES ('GUEST',1,'Khách thử');
            INSERT INTO loai_phong(ma_loai_phong,ten_loai_phong,gia_co_ban_dem) VALUES ('TEST','Phòng thử',300);
            INSERT INTO phong(ma_phong,loai_phong_id,so_phong) VALUES ('TEST',1,'101');
            INSERT INTO dat_phong(ma_dat_phong,khach_hang_id,du_kien_nhan,du_kien_tra) VALUES
              ('BOOKING1',1,'2030-01-01 07:00:00','2030-01-02 05:00:00'),
              ('BOOKING2',1,'2030-01-01 07:00:00','2030-01-02 05:00:00');
            INSERT INTO chi_tiet_dat_phong(dat_phong_id,phong_id) VALUES (1,1),(2,1);
            INSERT INTO khoa_lich_phong(chi_tiet_dat_phong_id,phong_id,ngay_luu_tru) VALUES (1,1,'2030-01-01');
            INSERT INTO hoa_don_phong(ma_hoa_don,dat_phong_id,tong_tien,trang_thai) VALUES
              ('DRAFT',1,100,'NHAP'),('CANCELLED',1,200,'DA_HUY'),('ISSUED',1,300,'DA_PHAT_HANH');
            INSERT INTO hoa_don_dich_vu(ma_hoa_don,dat_phong_id,tong_tien,trang_thai) VALUES
              ('S_DRAFT',1,100,'NHAP'),('S_CANCELLED',1,200,'DA_HUY'),('S_ISSUED',1,300,'DA_PHAT_HANH');
            INSERT INTO thanh_toan_hoa_don_phong(ma_thanh_toan,hoa_don_phong_id,phuong_thuc_id,so_tien) VALUES ('PAY',3,1,50);
            INSERT INTO thanh_toan_dich_vu(ma_thanh_toan,hoa_don_dich_vu_id,phuong_thuc_id,so_tien) VALUES ('PAY',3,1,25);
            INSERT INTO vietqr_deposit_order(order_id,booking_id,account_id,amount,description,qr_code,qr_data_url,created_at)
              VALUES ('DEPOSIT',1,1,100,'Cọc','qr','data',UTC_TIMESTAMP());
            INSERT INTO vietqr_payment_order(order_id,hoa_don_phong_id,so_tien,noi_dung,qr_code,qr_data_url,ma_giao_dich)
              VALUES ('PAYMENT',3,50,'Thanh toán','qr','data','TX001');
            """);
        var duplicateRoom = await Assert.ThrowsAsync<MySqlException>(() => database.ExecuteAsync("INSERT INTO khoa_lich_phong(chi_tiet_dat_phong_id,phong_id,ngay_luu_tru) VALUES (2,1,'2030-01-01')"));
        Assert.Equal(1062, duplicateRoom.Number);
        Assert.Contains("uq_room_day", duplicateRoom.Message);
        var duplicateDeposit = await Assert.ThrowsAsync<MySqlException>(() => database.ExecuteAsync("""
            INSERT INTO vietqr_deposit_order(order_id,booking_id,account_id,amount,description,qr_code,qr_data_url,created_at)
            VALUES ('DEPOSIT2',1,1,100,'Cọc','qr','data',UTC_TIMESTAMP())
            """));
        Assert.Equal(1062, duplicateDeposit.Number);
        Assert.Contains("uq_deposit_booking", duplicateDeposit.Message);
        var duplicatePayment = await Assert.ThrowsAsync<MySqlException>(() => database.ExecuteAsync("""
            INSERT INTO vietqr_payment_order(order_id,hoa_don_phong_id,so_tien,noi_dung,qr_code,qr_data_url,ma_giao_dich)
            VALUES ('PAYMENT2',3,50,'Thanh toán','qr','data','TX001')
            """));
        Assert.Equal(1062, duplicatePayment.Number);
        Assert.Contains("uq_vietqr_transaction_id", duplicatePayment.Message);
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT COUNT(*) FROM vw_cong_no_hoa_don_phong"));
        Assert.Equal(250m, await database.ScalarAsync<decimal>("SELECT con_phai_thu FROM vw_cong_no_hoa_don_phong"));
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT COUNT(*) FROM vw_cong_no_hoa_don_dich_vu"));
        Assert.Equal(275m, await database.ScalarAsync<decimal>("SELECT con_phai_thu FROM vw_cong_no_hoa_don_dich_vu"));
    }

    [MySqlFact]
    public async Task Every_connection_uses_utc_for_database_defaults()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        await db.Database.OpenConnectionAsync();
        Assert.Equal("+00:00", await db.Database.SqlQueryRaw<string>("SELECT @@session.time_zone AS Value").SingleAsync());
        await db.Database.ExecuteSqlRawAsync("SET time_zone = '+07:00'");
        await db.Database.CloseConnectionAsync();
        db.Database.OpenConnection();
        Assert.Equal("+00:00", db.Database.SqlQueryRaw<string>("SELECT @@session.time_zone AS Value").Single());
        var offset = await db.Database.SqlQueryRaw<int>("SELECT TIMESTAMPDIFF(SECOND, UTC_TIMESTAMP(), CURRENT_TIMESTAMP()) AS Value").SingleAsync();
        Assert.Equal(0, offset);
    }

    private static Task ReadEntity<T>(HotelDbContext db) where T : class => db.Set<T>().AsNoTracking().Take(1).ToListAsync();

    private sealed class TestDatabase : IAsyncDisposable
    {
        public static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        private readonly MySqlConnection connection;
        private readonly string connectionString;
        public string Name { get; } = "hotel_compat_test_" + Guid.NewGuid().ToString("N");

        private TestDatabase(string connectionString)
        {
            this.connectionString = connectionString;
            connection = new MySqlConnection(connectionString);
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var settings = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("HOTEL_TEST_MYSQL")!)
            { Database = "", AllowUserVariables = true };
            var database = new TestDatabase(settings.ConnectionString);
            await database.connection.OpenAsync();
            try
            {
                var sql = await File.ReadAllTextAsync(Path.Combine(Root, "Quanlychuoikhachsan/Database/Install/hotel_management_compatible.sql"));
                await database.ExecuteAsync(sql.Replace("`hotel_management_compatible`", $"`{database.Name}`"));
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }

        public HotelDbContext Context()
        {
            var settings = new MySqlConnectionStringBuilder(connectionString) { Database = Name };
            return new HotelDbContext(new DbContextOptionsBuilder<HotelDbContext>()
                .UseMySql(settings.ConnectionString, ServerVersion.Parse(connection.ServerVersion))
                .AddInterceptors(new UtcConnectionInterceptor()).Options);
        }

        public MySqlCommand Command(string sql) => new(sql, connection) { CommandTimeout = 60 };

        public async Task ExecuteAsync(string sql)
        {
            await using var command = Command(sql);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var command = Command(sql);
            return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T), CultureInfo.InvariantCulture);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                // Chỉ xóa database mang tên GUID do chính test này tạo ra.
                await ExecuteAsync($"DROP DATABASE IF EXISTS `{Name}`");
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
