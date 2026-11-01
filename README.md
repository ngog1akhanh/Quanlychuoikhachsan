# Hệ thống quản lý khách sạn (Đồ án Công nghệ phần mềm)

Một solution duy nhất: `HeThongQuanLyKhachSan.sln` (mở được trên mọi bản Visual Studio 2022 trở lên, cần .NET 9 SDK).

| Thư mục | Vai trò | Công nghệ | Địa chỉ mặc định |
|---|---|---|---|
| `Quanlychuoikhachsan/` | REST API + cơ sở dữ liệu | ASP.NET Core .NET 9, EF Core, MySQL | http://localhost:5005 (Swagger: `/swagger`) |
| `HotelAdmin.WinForms/` | App quản lý cho admin / nhân viên | WinForms (Windows) | gọi API ở 5005 |
| `QuanLyKhachSan.Web/` | Website đặt phòng cho khách | Blazor Web App | http://localhost:5100 |
| `Quanlychuoikhachsan.Api.Tests/` | Test của API và tương thích WinForms | xUnit | |
| `docs/` | Mô tả chức năng, tài liệu API, hướng dẫn backend, báo cáo tích hợp | | |
| `scripts/` | Script sinh tài liệu API / seed quyền | Python | |

## Chạy lần đầu
1. Cài .NET 9 SDK và MySQL 8. Tạo DB, chạy lần lượt 5 script (4 script schema/quyền + `seed_demo.sql`) trong `Quanlychuoikhachsan/Database/Scripts/` (thứ tự ghi trong `docs/HUONG_DAN_BACKEND.md`).
2. Đặt chuỗi kết nối và khóa JWT bằng User Secrets (xem `docs/HUONG_DAN_BACKEND.md`). Tài khoản demo nằm trong `tai khoan test demo.txt` (admin: `admin.com` / `12345`).
3. Chạy cả API + Web + App quản lý cùng lúc, chọn một trong hai cách:
   - **Visual Studio:** mở `HeThongQuanLyKhachSan.sln`, ở thanh công cụ chọn mục chạy **"Chay ca API + Web + App"** (lấy từ file `HeThongQuanLyKhachSan.slnLaunch`) rồi nhấn F5. Nếu VS không hiện mục này: chuột phải Solution → *Configure Startup Projects…* → *Multiple startup projects*, đặt 3 project `Quanlychuoikhachsan`, `QuanLyKhachSan.Web`, `HotelAdmin.WinForms` thành **Start** (để API ở trên cùng).
   - **Không cần Visual Studio:** nhấp đúp `chay-he-thong.bat`. File này mở API, đợi API sẵn sàng rồi mới mở Web (cổng 5100) và App quản lý.

Lưu ý: chạy API bằng profile **http** (cổng 5005). Profile https sẽ chuyển hướng 5005 → 7147 và làm mất header đăng nhập của web.

Web đọc cấu hình ở `QuanLyKhachSan.Web/appsettings.json` (`Api:BaseUrl`, `Api:UseMockData`). Đặt `UseMockData=true` để demo giao diện khi chưa chạy API.
