# WinForms quản lý khách sạn

Ứng dụng .NET 10 dành cho Windows, lấy từ nhánh LMT và tích hợp với backend mới trong cùng solution.

- Build: `dotnet build ../Quanlychuoikhachsan.slnx` từ thư mục này.
- Cấu hình URL server tại `appsettings.json`, khóa `ApiBaseUrl`.
- Chạy: `dotnet run --project HotelAdmin.WinForms.csproj`.
- Đăng nhập bằng tài khoản nhân viên đang hoạt động đã được gán vai trò/quyền trong database.

Đã có màn hình tổng quan, quản lý phòng, đặt phòng và lễ tân. Các module còn lại giữ màn hình tạm; danh sách API trên màn hình không có nghĩa giao diện nghiệp vụ đã hoàn tất.

Trả phòng thực hiện lần lượt: chốt hóa đơn, xác nhận thu tiền, trả phòng. Không nhập lại tiền phụ thu tự động vào ô phụ thu thêm.

Chi tiết về tương thích, mã vai trò, UTC, script database và kiểm thử xem [hướng dẫn tích hợp](../docs/LMT_INTEGRATION.md).
