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
| Nền tảng | ASP.NET Core .NET 9 (Web API + Razor Pages) |
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

Tạo database **mới**: mở MySQL Workbench, chạy lần lượt 5 script trong `Quanlychuoikhachsan/Database/Scripts/`.

| Thứ tự | Script | Tác dụng |
|---|---|---|
| 1 | `hotel_management_final_food_inventory.sql` | Tạo database `hotel_management` và toàn bộ bảng |
| 2 | `add_vietqr_payment_order.sql` | Bảng thanh toán VietQR cho hóa đơn |
| 3 | `add_operational_api_support.sql` | Bảng đơn cọc VietQR |
| 4 | `seed_api_permissions.sql` | Vai trò, quyền và phân quyền mặc định |
| 5 | `seed_demo.sql` | Dữ liệu mẫu: 10 tài khoản, 24 phòng, ~160 booking, dịch vụ, hàng kho |
| 6 (tùy chọn) | `seed_thong_ke.sql` | Hóa đơn lịch sử 2024–03/2026, nhiều nhân viên lập, có hóa đơn dịch vụ — cho các tab Thống kê hóa đơn |

> Từ script 2 trở đi, trước khi chạy hãy **bấm đúp vào `hotel_management`** ở panel Schemas để chọn database (tên in đậm).

Tài khoản demo nằm trong `tai khoan test demo.txt`, mật khẩu chung `12345`.

**Database đã có sẵn:** chỉ chạy các script bổ sung chưa chạy, không chạy lại script 1. Script 4 và 5 chạy lại nhiều lần được.

---

## 3. Cấu hình

Đặt bằng **User Secrets** hoặc biến môi trường (dùng `__` thay cho `:`). Không đưa thông tin bí mật vào Git.

| Khóa | Bắt buộc | Ý nghĩa |
|---|:---:|---|
| `ConnectionStrings:HotelManagement` | ✔ | Chuỗi kết nối MySQL |
| `Jwt:SigningKey` | ✔ | Khóa ký token, tối thiểu 32 ký tự |
| `Jwt:Issuer`, `Jwt:Audience` | ✔ | Tên phát hành / đối tượng của token |
| `Database:ServerVersion` | | Mặc định `8.0.36-mysql`; MariaDB thì ghi đúng phiên bản, vd. `10.4.32-mariadb` |
| `VietQR:Username`, `VietQR:Password` | | Tài khoản VietQR sandbox. Để trống → thanh toán QR chạy **chế độ demo** |
| `VietQR:BankAccount`, `VietQR:BankCode`, `VietQR:UserBankName` | | Tài khoản nhận tiền |
| `VietQR:Mode` | | `Auto` (mặc định) / `Demo` / `Live` – xem [THANH_TOAN_QR_COC.md](THANH_TOAN_QR_COC.md) |
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

> Luôn chạy API bằng profile **http** (cổng 5005). Profile https chuyển hướng sang cổng 7147 và làm mất header đăng nhập của website.

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
| Giữ phòng | Booking chưa đủ cọc ở trạng thái `HOLD` trong **15 phút**, quá hạn tự hủy. Khách tạo QR cọc thì giữ thêm để còn ít nhất **10 phút** |
| Cọc QR | Đủ tiền + phòng còn giữ → `BOOKED`. Thiếu tiền hoặc hết giữ phòng → phiếu hoàn chờ xử lý. Chi tiết: [THANH_TOAN_QR_COC.md](THANH_TOAN_QR_COC.md) |
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

- Kiểm thử tự động chạy trên database giả lập (EF InMemory); chưa kiểm thử khóa dòng, giao dịch đồng thời trên MySQL thật.
- VietQR dùng môi trường sandbox (hoặc chế độ demo khi chưa có tài khoản); chưa có webhook, chưa gọi ngân hàng hay SMTP thật.
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
| [THANH_TOAN_QR_COC.md](THANH_TOAN_QR_COC.md) | Thanh toán cọc bằng QR: luồng, chế độ demo, cấu hình, cách demo |
