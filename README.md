# Hệ thống quản lý khách sạn

Nhánh `codex/integrate-lmt-api` tích hợp website của Trung, WinForms/API mới nhất của LMT và bộ database tương thích. Solution chung: **`HeThongQuanLyKhachSan.sln`**, dùng **.NET 10 SDK**. WinForms cần Windows.

| Project | Chức năng | Địa chỉ mặc định |
|---|---|---|
| `Quanlychuoikhachsan` | REST API, nghiệp vụ, MySQL/MariaDB | `http://localhost:5005`, Swagger `/swagger` |
| `QuanLyKhachSan.Web` | Website khách hàng: phòng, đặt phòng, hồ sơ, đánh giá, VietQR | `http://localhost:5100` |
| `HotelAdmin.WinForms` | Phòng, booking, lễ tân, hóa đơn, buồng phòng, dịch vụ, khách hàng, báo cáo, giá, nhân viên, kho | Gọi API cổng 5005 |
| `Quanlychuoikhachsan.Api.Tests` | Kiểm thử nghiệp vụ, HTTP, client và database thật | xUnit |

## Chuẩn bị database

1. Cài MySQL >= 8.0.16 hoặc MariaDB >= 10.4.
2. Import **một file** [hotel_management_compatible.sql](Quanlychuoikhachsan/Database/Install/hotel_management_compatible.sql): 67 bảng, 5 view, 6 vai trò, 42 quyền. Không chạy lại các script nguồn sau bundle.
3. Nếu cần dữ liệu/tài khoản demo, chạy riêng `Database/Scripts/seed_demo.sql` với database đã chọn. Seed không tự đổi database; chỉ dùng cho demo. Các tài khoản trong [danh sách demo](<tai khoan test demo.txt>) chỉ có sau bước này.

Ví dụ PowerShell 7 tại thư mục repo (có thể thay `mysql` bằng `mariadb`):

```powershell
Get-Content 'Quanlychuoikhachsan/Database/Install/hotel_management_compatible.sql' -Raw -Encoding utf8 |
    mysql --host=127.0.0.1 --user=root --password --default-character-set=utf8mb4
if ($LASTEXITCODE -ne 0) { throw 'Import database thất bại.' }

# Tùy chọn: chỉ chạy trên database demo.
Get-Content 'Quanlychuoikhachsan/Database/Scripts/seed_demo.sql' -Raw -Encoding utf8 |
    mysql --host=127.0.0.1 --user=root --password --database=hotel_management_compatible --default-character-set=utf8mb4
if ($LASTEXITCODE -ne 0) { throw 'Seed demo thất bại.' }
```

Không dùng `--force`: tên database đã tồn tại phải làm import dừng. Database đang có dữ liệu cần kế hoạch nâng cấp riêng. Xem [hướng dẫn database](docs/DATABASE_COMPATIBILITY.md). File SQL có sẵn trong Git; không cần RAR, Python hay thư mục `review` để import.

## Cấu hình và chạy

Cấu hình backend bằng User Secrets hoặc biến môi trường (`__` thay `:`):

| Khóa | Nội dung |
|---|---|
| `ConnectionStrings:HotelManagement` | Kết nối đến database đã import |
| `Database:ServerVersion` | Đúng phiên bản server, ví dụ `11.4.13-mariadb` hoặc `8.0.36-mysql` |
| `Jwt:SigningKey` | Khóa riêng tối thiểu 32 ký tự |
| `Jwt:Issuer`, `Jwt:Audience` | Issuer/audience xác thực |
| `VietQR:Username`, `VietQR:Password`, `VietQR:BankAccount`, `VietQR:BankCode`, `VietQR:UserBankName` | Cấu hình gateway khi cần thử thanh toán |
| `Smtp:Host`, `Smtp:Port`, `Smtp:From`, `Smtp:Username`, `Smtp:Password` | Email khôi phục mật khẩu khi cần |

```powershell
dotnet user-secrets set 'ConnectionStrings:HotelManagement' 'Server=127.0.0.1;Port=3306;Database=hotel_management_compatible;User ID=<app_user>;Password=<password>' --project Quanlychuoikhachsan
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

- [Báo cáo tích hợp và phạm vi kiểm chứng](docs/LMT_INTEGRATION.md).
- [Hướng dẫn backend](docs/HUONG_DAN_BACKEND.md).
- [Các luồng nghiệp vụ](docs/API_WORKFLOWS.md).
- [Danh mục endpoint](docs/API_ENDPOINTS.md) và [OpenAPI](docs/openapi.json).

Gateway VietQR dùng cấu hình sandbox; kiểm thử local không chứng minh giao dịch ngân hàng thật. Hoàn tiền hiện ghi nhận khoản đã hoàn, không tự chuyển tiền. Import schema không tự tạo admin; seed demo cung cấp tài khoản thử, còn môi trường thật cần cấp tài khoản nhân viên và vai trò ADMIN riêng.
