# Hệ thống quản lý khách sạn

Nhánh `codex/integrate-lmt-api` tích hợp website của Trung, WinForms/API mới nhất của LMT và bộ database tương thích. Solution chung: **`HeThongQuanLyKhachSan.sln`**, dùng **.NET 9 SDK** (máy có .NET 10 SDK cũng build/chạy được). WinForms cần Windows.

| Project | Chức năng | Địa chỉ mặc định |
|---|---|---|
| `Quanlychuoikhachsan` | REST API, nghiệp vụ, MySQL/MariaDB | `http://localhost:5005`, Swagger `/swagger` |
| `QuanLyKhachSan.Web` | Website khách hàng: phòng, đặt phòng, hồ sơ, đánh giá, VietQR | `http://localhost:5100` |
| `HotelAdmin.WinForms` | Phòng, booking, lễ tân, hóa đơn, buồng phòng, dịch vụ, khách hàng, báo cáo, giá, nhân viên, kho | Gọi API cổng 5005 |
| `Quanlychuoikhachsan.Api.Tests` | Kiểm thử nghiệp vụ, HTTP, client và database thật | xUnit |

## Chuẩn bị database

> **Máy mới, chưa có database?** Làm theo hướng dẫn từng bước: [docs/HUONG_DAN_CAI_DAT_TU_DAU.md](docs/HUONG_DAN_CAI_DAT_TU_DAU.md) (cài phần mềm → tạo database → khai báo kết nối → chạy → đăng nhập → xử lý lỗi).

**Thành viên mới pull về chỉ cần chạy MỘT file:** [`Quanlychuoikhachsan/Database/Install/hotel_management_full.sql`](Quanlychuoikhachsan/Database/Install/hotel_management_full.sql). File tạo database `hotel_management` gồm toàn bộ bảng, quyền, ràng buộc **và dữ liệu demo** (tài khoản, 24 phòng, booking, hóa đơn lịch sử cho báo cáo, kho, việc buồng phòng). **Không chạy thêm** script nào trong `Database/Scripts` sau file này.

1. Cài MySQL >= 8.0.16 (hoặc MariaDB >= 10.4) và MySQL Workbench.
2. Workbench: **File → Open SQL Script…** → chọn `hotel_management_full.sql` → bấm ⚡ (**Execute all**). Kết quả cuối phải là `READY_WITH_DEMO_DATA`.
   - Máy đã có database `hotel_management` thì script dừng ngay dòng đầu. Muốn dựng lại: `DROP DATABASE hotel_management;` rồi chạy lại (mất dữ liệu cũ).
   - Dòng lệnh (PowerShell 7): `Get-Content 'Quanlychuoikhachsan/Database/Install/hotel_management_full.sql' -Raw -Encoding utf8 | mysql --host=127.0.0.1 --user=root --password --default-character-set=utf8mb4`
3. Tạo tài khoản MySQL mà `appsettings.json` đang dùng (chạy trong Workbench): `CREATE USER IF NOT EXISTS 'hotel_app'@'localhost' IDENTIFIED BY 'change-me'; GRANT ALL PRIVILEGES ON hotel_management.* TO 'hotel_app'@'localhost';` – xong là API kết nối được, không cần User Secrets.
4. Tài khoản demo trong [`tai khoan test demo.txt`](<tai khoan test demo.txt>), mật khẩu chung `12345` (vd. `admin.com`, `letan@hotel.com`, `kho@hotel.com`).
5. Dữ liệu demo tính theo ngày chạy script (khách đến hôm nay, lô sắp hết hạn…): dựng lại vào ngày khác sẽ có dữ liệu mới theo ngày đó.

**Các file database trong repo:**

| File | Dùng khi nào |
|---|---|
| `Database/Install/hotel_management_full.sql` | **Dựng database để chạy/demo** (file duy nhất cần chạy) |
| `Database/Scripts/*.sql` | Script nguồn (schema, quyền, từng bộ seed). Chỉ dùng khi muốn nạp thêm một phần seed vào database đang có, hoặc khi sửa dữ liệu gốc |

**Sửa script nguồn:** sửa file trong `Database/Scripts`, rồi chạy `python scripts/build_database.py` để sinh lại `Database/Install/hotel_management_full.sql` và commit cả hai. Không sửa tay file trong `Install`. Kiểm tra file đã cập nhật: `python scripts/build_database.py --check`.

## Cấu hình và chạy

Cấu hình backend bằng User Secrets hoặc biến môi trường (`__` thay `:`):

| Khóa | Nội dung |
|---|---|
| `ConnectionStrings:HotelManagement` | Kết nối đến database đã import |
| `Database:ServerVersion` | Đúng phiên bản server, ví dụ `11.4.13-mariadb` hoặc `8.0.36-mysql` |
| `Jwt:SigningKey` | Khóa riêng tối thiểu 32 ký tự |
| `Jwt:Issuer`, `Jwt:Audience` | Issuer/audience xác thực |
| `VietQR:Username`, `VietQR:Password`, `VietQR:BankAccount`, `VietQR:BankCode`, `VietQR:UserBankName` | Cấu hình gateway khi cần thử thanh toán |
| `VietQR:Mode` | `Auto` mặc định: thiếu cấu hình thì demo; `Demo` giả lập; `Live` gọi gateway sandbox |
| `Smtp:Host`, `Smtp:Port`, `Smtp:From`, `Smtp:Username`, `Smtp:Password` | Email khôi phục mật khẩu khi cần |

```powershell
dotnet user-secrets set 'ConnectionStrings:HotelManagement' 'Server=127.0.0.1;Port=3306;Database=hotel_management;User ID=<app_user>;Password=<password>' --project Quanlychuoikhachsan
dotnet user-secrets set 'Database:ServerVersion' '11.4.13-mariadb' --project Quanlychuoikhachsan
dotnet restore HeThongQuanLyKhachSan.sln
dotnet build HeThongQuanLyKhachSan.sln
dotnet run --project Quanlychuoikhachsan --launch-profile http
```

Trong hai terminal khác:

```powershell
dotnet run --project QuanLyKhachSan.Web --launch-profile http
dotnet run --project HotelAdmin.WinForms
```

Đặt `Api:BaseUrl` của web và `ApiBaseUrl` của WinForms đúng URL API. Web dùng `Api:UseMockData=false` để gọi API thật; web không cần cấu hình DB hay khóa ký JWT. Dùng profile API `http` cho cấu hình local trên; nếu dùng HTTPS, cập nhật URL cả hai client và chứng chỉ phát triển.

Có thể chọn cấu hình chạy nhiều project từ `HeThongQuanLyKhachSan.slnLaunch`, hoặc dùng `chay-he-thong.bat`. Không commit User Secrets, dữ liệu database hay cấu hình riêng của máy.

## Kiểm thử và tài liệu

```powershell
dotnet test Quanlychuoikhachsan.Api.Tests/Quanlychuoikhachsan.Api.Tests.csproj
python scripts/export_api_docs.py
python scripts/build_database.py --check
```

Đặt `HOTEL_TEST_MYSQL` thành connection string của server kiểm thử để chạy test database thật. Test tạo database có tên GUID và chỉ xóa database do chính test tạo. Thiếu biến này thì test server bị bỏ qua.

- [Hướng dẫn cài đặt từ đầu](docs/HUONG_DAN_CAI_DAT_TU_DAU.md).
- [Hướng dẫn backend](docs/HUONG_DAN_BACKEND.md).
- [Các luồng nghiệp vụ](docs/API_WORKFLOWS.md).
- [Danh mục endpoint](docs/API_ENDPOINTS.md) và [OpenAPI](docs/openapi.json).

Gateway VietQR dùng cấu hình sandbox; kiểm thử local không chứng minh giao dịch ngân hàng thật. Hoàn tiền hiện ghi nhận khoản đã hoàn, không tự chuyển tiền. Import schema không tự tạo admin; seed demo cung cấp tài khoản thử, còn môi trường thật cần cấp tài khoản nhân viên và vai trò ADMIN riêng.

Nút giả lập cọc chỉ hoạt động khi API ở `Development` và gateway ở chế độ demo. Lần tích hợp 03/11/2026: 66 test đạt (9 test MariaDB thật); WinForms tải 12 trang/43 tab, web hoàn tất luồng QR demo.
