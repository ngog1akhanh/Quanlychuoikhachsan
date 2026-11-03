# Thanh toán cọc bằng QR (VietQR)

Khách đặt phòng trên website, quét QR để chuyển tiền cọc. Nhận đủ tiền khi phòng còn giữ thì booking tự chuyển **Đã đặt (BOOKED)**.

## 1. Luồng hoạt động

```
Khách chọn phòng → booking HOLD (giữ 15 phút)
        │
        ▼
Bấm "Tạo mã QR" → giữ phòng còn ít nhất 10 phút → hiện QR + số tiền + nội dung CK
        │
        ▼
Hệ thống kiểm tra tiền về bằng 3 cách:
  • Web hỏi mỗi 5 giây (khi khách còn mở trang)
  • Job nền hỏi mỗi phút (kể cả khi khách đã đóng trang) – chỉ khi dùng VietQR thật
  • Nhân viên bấm "Kiểm tra ngay" trong App quản lý
        │
        ▼
Kết quả:
  • Đủ tiền, phòng còn giữ      → phiếu cọc + booking BOOKED          (đơn: Đã nhận tiền)
  • Thiếu tiền / hết giữ phòng  → phiếu cọc + phiếu hoàn chờ xử lý    (đơn: Cần hoàn tiền)
  • QR hết hạn / bị hủy         → khách bấm "Tạo mã QR mới"           (đơn: Thất bại)
```

## 2. Hai chế độ

| | Demo | VietQR thật |
|---|---|---|
| Khi nào | Chưa điền tài khoản VietQR (mặc định của repo) | Đã điền đủ `VietQR:Username, Password, BankAccount, BankCode, UserBankName` |
| Mã QR | Hệ thống tự dựng theo chuẩn VietQR; app ngân hàng đọc được số tiền, nội dung | Do VietQR sandbox tạo |
| Xác nhận tiền về | Bấm **Giả lập đã thanh toán (demo)** trên web hoặc App | Tự động qua tra cứu VietQR |
| Job nền mỗi phút | Không chạy | Chạy |

> Nút giả lập chỉ hoạt động khi API chạy môi trường **Development** (profile http mặc định) **và** đang ở chế độ demo. Không có tiền thật được chuyển.

## 3. Cấu hình

Mục `VietQR` trong `appsettings.json` hoặc User Secrets:

| Khóa | Mặc định | Ý nghĩa |
|---|---|---|
| `Mode` | `Auto` | `Auto`: đủ tài khoản thì dùng thật, thiếu thì demo · `Demo`: luôn demo · `Live`: luôn dùng thật |
| `BankBin` | `970436` | Mã BIN ngân hàng (6 số) để dựng QR demo |
| `BankAccount` | `0123456789` (demo) | Số tài khoản nhận tiền |
| `HoldExtensionMinutes` | `10` | Khi tạo QR, giữ phòng còn ít nhất bấy nhiêu phút |

## 4. Demo nhanh

1. Chạy `chay-he-thong.bat`.
2. Web: đăng nhập tài khoản khách → chọn phòng → **Tạo mã QR thanh toán cọc**.
3. Bấm **Giả lập đã thanh toán (demo)** → chuyển sang trang thành công.
4. App quản lý (`letan@hotel.com`): **Hóa đơn & thanh toán → Đơn cọc QR** thấy đơn *Đã nhận tiền*; tab **Phiếu cọc** có phiếu VietQR mới.

Thử trường hợp hết giờ: tạo QR, đợi quá thời gian giữ phòng mà không thanh toán → booking tự hủy, đơn vẫn *Chờ thanh toán*. Nếu sau đó bấm giả lập trong App → đơn chuyển *Cần hoàn tiền* và có phiếu hoàn ở tab **Phiếu hoàn tiền**.

## 5. API

| Phương thức | Đường dẫn | Ai dùng | Ghi chú |
|---|---|---|---|
| POST | `/v1/bookings/{id}/deposit-payment` | Khách chủ booking | Tạo QR hoặc trả lại QR đang chờ; QR thất bại thì tạo QR mới |
| POST | `/v1/deposit-payments/{orderId}/inquiry` | Khách chủ đơn, nhân viên `BILLING_PAYMENT_WRITE` | Tra cứu ngay |
| POST | `/v1/deposit-payments/{orderId}:simulate-paid` | Như trên | Chỉ Development + demo |
| GET | `/v1/app/deposit-orders` | `BILLING_READ` | Danh sách cho App: mã booking, khách, SĐT, hạn giữ phòng |

Kết quả của 3 API POST có thêm `booking_code`, `hold_expires_at` (hạn giữ phòng mới nhất) và `demo`.

## 6. File thay đổi

| File | Nội dung |
|---|---|
| `Application/Payments/VietQrSettings.cs` | Thêm `Mode`, `BankBin`, `HoldExtensionMinutes`, `IsDemo` |
| `Infrastructure/Payments/DemoVietQrGateway.cs` | **Mới** – cổng giả lập, dựng chuỗi VietQR (EMVCo + CRC16) |
| `Infrastructure/Payments/VietQrGateway.cs` | Dùng lại token VietQR đến gần hết hạn thay vì xin mới mỗi lần gọi |
| `Program.cs` | Chọn cổng demo hay thật theo cấu hình |
| `Application/Payments/DepositPaymentService.cs` | Tạo lại QR khi thất bại; nội dung CK ngắn `COC<id> <mã>`; nới giờ giữ phòng; xử lý tiền thiếu; giả lập; nhân viên được tra cứu |
| `Application/Bookings/BookingMaintenance.cs` | Job nền tra cứu tối đa 20 đơn đang chờ mỗi phút, trước bước hủy booking hết hạn |
| `Api/Controllers/DepositPaymentsController.cs` | Route giả lập; trả kèm hạn giữ phòng |
| `Api/Controllers/AppViewsController.cs` | `GET /v1/app/deposit-orders` |
| `Quanlychuoikhachsan.Api.Tests/DepositAndMaintenanceTests.cs` | Cập nhật test tiền thiếu; thêm test tạo lại QR, giả lập, chuỗi QR |
| `QuanLyKhachSan.Web/Pages/Booking/Checkout.razor` | Vẽ QR khi API không trả ảnh; cập nhật đồng hồ theo hạn mới; dừng hỏi khi hết giờ; nút tạo QR mới, nút giả lập |
| `QuanLyKhachSan.Web/wwwroot/js/qrcode.js` | **Mới** – thư viện vẽ QR (qrcode-generator 1.4.4, MIT) |
| `HotelAdmin.WinForms/Pages/BillingPage.cs` | Tab **Đơn cọc QR**: lọc trạng thái, Kiểm tra ngay, Giả lập |

Không đổi database.

## 7. Chưa làm

- **Webhook** VietQR (ngân hàng gọi về ngay khi có tiền) – cần địa chỉ public.
- **QR tại quầy** cho khách đặt qua điện thoại / vãng lai.
- Khách chuyển **thừa** tiền (không vượt tổng tiền booking): hệ thống vẫn xác nhận, phiếu cọc ghi đúng số tiền nhận; chưa có cảnh báo riêng cho trường hợp này.
