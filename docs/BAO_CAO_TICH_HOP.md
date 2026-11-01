# Báo cáo tích hợp và kiểm tra API

Ngày: 31/10/2026. Phương pháp: đọc mã và đối chiếu `docs/openapi.json` (178 route). **Chưa build, chưa chạy** vì môi trường kiểm tra không có .NET SDK và MySQL; cần chạy `dotnet build` và `dotnet test` trên máy của bạn.

## Đã làm
- Gộp API, WinForms (bản mới trong API.rar), Web và Tests vào một solution. Bỏ backend cũ (APP.rar), project test payOS cũ, `.github/copilot-instructions.md`, file `.rar` trùng, `.git`, `bin`, `obj`, `.vs`, `.sln` riêng của web.
- WinForms: 37/37 lệnh gọi endpoint khớp API, không cần sửa.
- Web: đổi sang .NET 9 (cùng phiên bản với API và WinForms, chạy được trên Visual Studio 2022), `UseMockData=false`, và sửa các service gọi đúng API:

| Chức năng | Trước | Nay |
|---|---|---|
| Tìm phòng | `/v1/public/rooms/available` | `/v1/public/rooms/availability` + `/v1/public/rooms/{id}` |
| Booking của tôi | `/v1/public/my-bookings` | `/v1/me/bookings` (đọc `items`) |
| Hồ sơ | `/v1/customers/{id}` | `GET/PUT /v1/me` (email không đổi được) |
| Đổi mật khẩu | `/v1/auth/change-password` | `/v1/me/change-password` |
| Đánh giá | `/v1/bookings/{id}/reviews` | `POST /v1/reviews`, `GET /v1/reviews/mine` |
| Đặt cọc | `:confirm-deposit` (khách bị 403) | VietQR: `/v1/bookings/{id}/deposit-payment` + `/v1/deposit-payments/{orderId}/inquiry`; Checkout hiện mã QR và tự kiểm tra mỗi 5 giây |

## Còn tồn tại / cần bạn kiểm tra
1. Code web mới sửa chưa được biên dịch. Nếu có lỗi build, gửi lại thông báo lỗi để sửa.
2. Thanh toán VietQR cần cấu hình tài khoản VietQR (sandbox) ở backend; nếu chưa cấu hình, API trả lỗi khi tạo mã QR.
3. Chưa có trang đặt lại mật khẩu trên web (`POST /v1/auth/reset-password {token, password}`); quên mật khẩu cần SMTP.
4. Database là MySQL/MariaDB. Không có tài khoản ADMIN và dữ liệu mẫu (phòng, loại phòng…) tạo sẵn.
5. WinForms mới có 4 màn hình hoạt động (Tổng quan, Phòng, Đặt phòng, Lễ tân).
6. Tên trường của `GET /v1/public/rooms/{id}` là tiếng Việt (`tang`, `loai_view`, `mo_ta`, `room_type.so_nguoi_lon_toi_da`…); web đã ánh xạ, nếu thiếu thì chỉ mất tầng/view/diện tích chứ không lỗi.
