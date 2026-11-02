# Hướng dẫn backend

Backend của hệ thống quản lý khách sạn: REST API phục vụ **App quản lý (WinForms)** và **website đặt phòng (Blazor)**.

**Nội dung**

1. [Công nghệ và cấu trúc](#1-công-nghệ-và-cấu-trúc)
2. [Chuẩn bị database](#2-chuẩn-bị-database)
3. [Cấu hình](#3-cấu-hình)
4. [Chạy và kiểm thử](#4-chạy-và-kiểm-thử)
5. [Quy ước API](#5-quy-ước-api)
6. [Quy tắc nghiệp vụ quan trọng](#6-quy-tắc-nghiệp-vụ-quan-trọng)
7. [Vai trò và quyền](#7-vai-trò-và-quyền)
8. [Thời gian và múi giờ](#8-thời-gian-và-múi-giờ)
9. [Giới hạn hiện tại](#9-giới-hạn-hiện-tại)
10. [Tài liệu liên quan](#10-tài-liệu-liên-quan)

---

## 1. Công nghệ và cấu trúc

| Thành phần | Công nghệ |
|---|---|
| Nền tảng | ASP.NET Core .NET 10 (Web API + Razor Pages) |
| Cơ sở dữ liệu | MySQL 8 hoặc MariaDB, qua Entity Framework Core 9 + Pomelo |
| Xác thực | JWT Bearer; quyền đọc từ bảng tài khoản – vai trò – quyền |
| Tài liệu API | Swagger tại `/swagger` (môi trường Development) |

Thư mục trong `Quanlychuoikhachsan/`:

| Thư mục | Chứa gì |
|---|---|
| `Api/Controllers` | Định nghĩa đường dẫn HTTP |
| `Application` | Nghiệp vụ và DTO |
| `Domain/Entities` | Lớp ánh xạ bảng database |
| `Infrastructure` | Truy cập dữ liệu, xác thực, middleware, VietQR |
| `Database/Scripts` | Script tạo database và dữ liệu mẫu |

---

## 2. Chuẩn bị database

Tạo database **mới**: import một file `Quanlychuoikhachsan/Database/Install/hotel_management_compatible.sql`. Bundle đã gồm schema, các bảng VietQR, quyền và ràng buộc tương thích (67 bảng, 5 view, 6 vai trò, 42 quyền). Không chạy lại các script nguồn sau bundle.

Nếu cần demo, chọn database vừa tạo rồi chạy `Quanlychuoikhachsan/Database/Scripts/seed_demo.sql`. Seed dùng database đang chọn, không tự chuyển sang `hotel_management`. Với CLI dùng `--database=hotel_management_compatible`; xem lệnh đầy đủ trong [README](../README.md).

Tài khoản demo nằm trong `tai khoan test demo.txt`, mật khẩu chung `12345`.

**Database đã có dữ liệu:** không import bundle hoặc seed demo đè lên. Cần migration và đối chiếu dữ liệu riêng. Seed demo đã được kiểm tra chạy lặp trong cùng ngày trên database thử; không dùng để cập nhật database đang vận hành hay chạy lại vào ngày khác.

---

## 3. Cấu hình

Đặt bằng **User Secrets** hoặc biến môi trường (dùng `__` thay cho `:`). Không đưa thông tin bí mật vào Git.

| Khóa | Bắt buộc | Ý nghĩa |
|---|:---:|---|
| `ConnectionStrings:HotelManagement` | ✔ | Chuỗi kết nối MySQL |
| `Jwt:SigningKey` | ✔ | Khóa ký token, tối thiểu 32 ký tự |
| `Jwt:Issuer`, `Jwt:Audience` | ✔ | Tên phát hành / đối tượng của token |
| `Database:ServerVersion` | | Mặc định `8.0.36-mysql`; MariaDB thì ghi đúng phiên bản, vd. `10.4.32-mariadb` |
| `VietQR:Username`, `VietQR:Password` | | Tài khoản VietQR sandbox (cần cho thanh toán QR) |
| `VietQR:BankAccount`, `VietQR:BankCode`, `VietQR:UserBankName` | | Tài khoản nhận tiền |
| `Smtp:Host`, `Smtp:Port`, `Smtp:From`, `Smtp:Username`, `Smtp:Password` | | Gửi mã đặt lại mật khẩu (port mặc định 587) |
| `BookingMaintenance:Enabled` | | Mặc định `true`: mỗi phút hủy booking giữ chỗ hết hạn và gửi nhắc lịch |

---

## 4. Chạy và kiểm thử

**Cách nhanh:** nhấp đúp `chay-he-thong.bat` ở thư mục gốc. File này mở API (cổng 5005), đợi API sẵn sàng rồi mở Web (cổng 5100) và App quản lý.

**Chạy bằng lệnh:**

```powershell
dotnet build HeThongQuanLyKhachSan.sln
dotnet test Quanlychuoikhachsan.Api.Tests/Quanlychuoikhachsan.Api.Tests.csproj
dotnet run --project Quanlychuoikhachsan --launch-profile http
```

> Cấu hình local dùng API profile **http** (cổng 5005). Nếu dùng HTTPS, cập nhật URL của cả web và WinForms cùng chứng chỉ phát triển, tránh phụ thuộc chuyển hướng HTTP khi xác thực.

---

## 5. Quy ước API

- Đường dẫn bắt đầu bằng `/v1`.
- JSON dùng `snake_case` (vd. `booking_id`); tham số query dùng `camelCase` (vd. `pageSize`).
- API cần đăng nhập nhận header `Authorization: Bearer <token>`.
- Phân trang có hai kiểu:
  - Danh sách mới: `?page=1&pageSize=20&search=...&status=...` → trả `items, total, page, page_size`.
  - Danh sách cũ (`/bookings`, `/invoices`, `/transactions`): `?pageSize=20&pageToken=...` → trả `items, next_page_token`.
- Mã nhân viên thực hiện (thu tiền, xác nhận...) lấy từ tài khoản đăng nhập; trường `employee_id` gửi lên chỉ để tương thích.
- `/v1/app/...`: các danh sách chỉ đọc đã ghép sẵn tên hiển thị cho App quản lý (xem `update_11_1.md`).

---

## 6. Quy tắc nghiệp vụ quan trọng

| Nghiệp vụ | Quy tắc |
|---|---|
| Giữ phòng | Booking chưa đủ cọc ở trạng thái `HOLD` trong **15 phút**, quá hạn tự hủy |
| Nhận phòng | Chỉ khi đã đến ngày lưu trú và mọi phòng ở trạng thái Sẵn sàng |
| Trả phòng | 3 bước: **chốt hóa đơn → thu tiền → trả phòng**. API từ chối trả phòng khi hóa đơn chưa phát hành hoặc còn nợ |
| Hóa đơn | Đã phát hành thì không sửa; sai thì hủy và lập lại. Gửi lại cùng `transaction_id` không bị thu hai lần |
| Thu cọc tại quầy | Có trường `reference` để chống thu trùng khi bấm lại |
| Hủy booking | Khách gửi yêu cầu → quản lý duyệt. Duyệt tạo phiếu hoàn `CHO_XU_LY`; đây chỉ là ghi nhận, không tự chuyển tiền |
| Dịch vụ | Hoàn thành yêu cầu dịch vụ tạo hóa đơn dịch vụ riêng, không cộng vào hóa đơn phòng |
| Kho | Tồn kho tính từ sổ kho; phiếu đã xác nhận không sửa; xuất kho theo lô hết hạn trước (FEFO) |

---

## 7. Vai trò và quyền

Backend luôn kiểm tra quyền ở từng API; App chỉ ẩn nút cho gọn.

**Vai trò mặc định**

| Mã | Vai trò | Mã cũ tương đương |
|---|---|---|
| `ADMIN` | Quản trị hệ thống | |
| `MANAGER` | Quản lý | |
| `RECEPTIONIST` | Lễ tân | `LE_TAN` |
| `HOUSEKEEPING` | Buồng phòng | `BUONG_PHONG` |
| `SERVICE` | Dịch vụ | `DICH_VU` |
| `WAREHOUSE` | Kho | `KHO` |

App nhận cả mã cũ lẫn mã mới. `seed_api_permissions.sql` cấp quyền cho cả hai bộ mã nếu database cũ còn dùng mã cũ.

**Quyền cần cho các thao tác hay gặp**

| Thao tác | Quyền |
|---|---|
| Tạo, sửa booking; nhận phòng | `BOOKING_WRITE` |
| Thu cọc, thu tiền hóa đơn | `BILLING_PAYMENT_WRITE` |
| Trả phòng (cả 3 bước) | `BOOKING_WRITE`, `BILLING_READ`, `BILLING_WRITE`, `BILLING_PAYMENT_WRITE` |
| Duyệt / từ chối hủy | `CANCELLATION_APPROVE` |
| Đổi trạng thái phòng | `ROOM_WRITE` |
| Tạo khách vãng lai | `CUSTOMER_WRITE` |

Danh sách đầy đủ quyền của từng API: `API_ENDPOINTS.md`.

---

## 8. Thời gian và múi giờ

- Database lưu thời điểm theo **UTC**. Khách sạn ở UTC+7: nhận phòng 14:00 giờ VN được lưu là 07:00 UTC.
- Khi gọi API, gửi thời gian kèm `Z` hoặc offset, vd. `2026-11-01T07:00:00Z`.
- Ngày lưu trú (bảng `gia_dem_dat_phong`, `khoa_lich_phong`) là ngày theo giờ VN, không phải thời điểm.
- Các route `/v1/front-desk/...` của App nhận và trả giờ VN để tương thích màn hình cũ.
- **Database cũ:** backend trước đây có chỗ lưu giờ VN, có chỗ lưu UTC. Không tự trừ 7 giờ hàng loạt; tạo lại database từ script là cách đơn giản nhất.

---

## 9. Giới hạn hiện tại

- Đã chạy 60 test gồm EF InMemory và MariaDB 11.4.13, cả hai yêu cầu đặt cùng phòng đồng thời. Chưa kiểm thử tải nhiều người dùng, deadlock diện rộng hoặc chạy riêng trên MySQL.
- Kiểm thử VietQR thay gateway bên ngoài bằng fake; chưa gọi ngân hàng hay SMTP thật.
- Phiếu hoàn tiền chỉ ghi nhận việc đã hoàn, không ra lệnh chuyển tiền.
- Ảnh phòng lưu bằng đường dẫn URL, chưa có chức năng tải ảnh lên.
- Thông báo chỉ gửi vào hộp thư trong ứng dụng, chưa có push/SMS/email (trừ email đặt lại mật khẩu).

---

## 10. Tài liệu liên quan

| File | Nội dung |
|---|---|
| [API_ENDPOINTS.md](API_ENDPOINTS.md) | Danh mục toàn bộ API và quyền |
| [API_WORKFLOWS.md](API_WORKFLOWS.md) | Ví dụ gọi API theo luồng: nhập hàng, đặt phòng, thanh toán, hoàn tiền |
| [API_FUNCTIONAL_COVERAGE.md](API_FUNCTIONAL_COVERAGE.md) | Đối chiếu chức năng trong tài liệu đồ án với API |
| [openapi.json](openapi.json) | Import vào Postman |
| [update_11_1.md](update_11_1.md) | Cập nhật 01/11: 8 module App quản lý, API `/v1/app` |
