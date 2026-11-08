# Hướng dẫn tích hợp API

Danh sách đầy đủ: [API_ENDPOINTS.md](API_ENDPOINTS.md). Schema request: [openapi.json](openapi.json), có thể import vào Postman. Mỗi ví dụ dưới đây là JSON body trực tiếp; không bọc request trong `data`.

## Xác thực, định dạng và phân trang

Đăng nhập tại `POST /v1/auth/login`, lấy JWT và gửi `Authorization: Bearer <token>`. Vai trò/quyền được tra từ database; tên vai trò không thay thế permission. Sau nâng cấp, đăng nhập lại để có các claim kiểm tra phiên mới.

JSON dùng snake_case, nhưng nhiều entity trả tên tiếng Việt như `chi_tiet`, `danh_muc_hang_hoa_id`; DTO có thể dùng tiếng Anh như `item_id`. Không suy ra tên field từ nhãn UI. Các API cũ tạo tài nguyên thường trả `{ "data": {...}, "request_id": "..." }`; GET/PUT mới thường trả trực tiếp.

Danh sách mới dùng `?page=1&pageSize=20&search=...&status=...`, trả `items,total,page,page_size`. Các danh sách cũ như GET `/bookings`, `/invoices`, `/transactions` dùng `pageSize/pageToken`, trả `items,next_page_token`. Dùng `/bookings/search` nếu cần lọc nghiệp vụ.

Thời điểm lưu theo UTC; gửi ISO 8601 với `Z`, ví dụ `2026-11-01T07:00:00Z`. Ngày của lịch phòng/giá là `yyyy-MM-dd`; đêm cuối là ngày trước checkout. Frontend chuyển múi giờ khi hiển thị. Khoảng báo cáo bao gồm cả `from` và `to`, tối đa 366 ngày.

Lỗi thông thường: 400 dữ liệu/trạng thái không hợp lệ, 401 chưa đăng nhập/phiên hết hiệu lực, 403 thiếu quyền, 404 không có tài nguyên hoặc không thuộc khách đang đăng nhập, 409 trùng dữ liệu/xung đột, 503 tích hợp chưa sẵn sàng. Các endpoint quên/đặt lại mật khẩu giới hạn 10 request/phút/IP.

## Nhập hàng và kho

1. Tạo/tra cứu `/suppliers`, `/warehouses`, `/inventory-units`, `/inventory-groups`, `/inventory-items`. Hàng có hạn dùng bắt buộc theo dõi lô; hàng theo cá thể phải là `TAI_SAN`.
2. Lập `POST /v1/purchase-orders` với quyền `PURCHASE_WRITE`:

```json
{
  "supplier_id": 1,
  "warehouse_id": 1,
  "discount": 0,
  "tax": 10000,
  "note": "Nhập sữa cho minibar",
  "lines": [{ "item_id": 1, "quantity": 24, "unit_price": 12000 }]
}
```

Số lượng kho tối đa 3 chữ số thập phân, tiền tối đa 2. Tài sản cá thể phải có số lượng nguyên. Đơn giá được chốt ở dòng nhập; server tính tổng. Không gửi lô/serial ở bước lập nháp.

3. GET `/v1/purchase-orders/{id}` lấy `chi_tiet[].id`. PUT cùng URL sửa nháp; có thêm `supplier_invoice_number`, `supplier_invoice_date` để lưu hóa đơn NCC. Dùng `:cancel` với `{ "reason": "..." }` để hủy nháp.
4. Nhận hàng bằng `POST /v1/purchase-orders/{id}:confirm`, quyền `PURCHASE_CONFIRM`:

```json
{
  "lines": [{
    "detail_id": 10,
    "lot_code": "MILK-20261101",
    "manufacture_date": "2026-11-01",
    "expiry_date": "2026-12-01"
  }]
}
```

Phải gửi đúng một phần tử cho mỗi dòng phiếu. Hàng không theo lô chỉ cần `detail_id`. Với tài sản cá thể, gửi mảng `serials` có số phần tử đúng số lượng hoặc để null nếu không có serial. Không nhận lô hết hạn. Xác nhận lần hai trả lỗi, không tăng tồn lần hai.

5. GET `/warehouses/{warehouseId}/inventory-items/{itemId}/balance`, `/inventory-lots`, `/stock-ledger`, `/inventory-alerts?warehouseId=1` để kiểm tra tồn và truy vết.
6. Tạo `POST /stock-issues`, sửa nháp bằng PUT `/stock-issues/{id}`, xác nhận bằng POST `/stock-issues/{id}:confirm`:

```json
{
  "warehouse_id": 1,
  "purpose": "MINIBAR",
  "recipient": "Buồng phòng tầng 2",
  "reason": "Bổ sung minibar",
  "lines": [{ "item_id": 1, "quantity": 4 }]
}
```

Server chọn lô theo FEFO khi xác nhận. Muốn điều chỉnh dùng `/stock-adjustments` với `warehouse_id,item_id,lot_id,quantity_change,type,reason`. `HONG_MAT`/`THANH_LY` bắt buộc âm; `DIEU_CHINH` có thể dương/âm, không bằng 0.

Tài sản cá thể dùng `/assets/{id}:transfer` với đúng một đích `room_id`, `warehouse_id` hoặc `other_location`; dùng `:condition` để ghi tình trạng. Không xuất cá thể qua phiếu xuất số lượng chung.

## Đặt phòng → nhận phòng → trả phòng

1. GET `/public/rooms/availability?checkInDate=2026-11-01&checkOutDate=2026-11-03&adults=2&children=0`. Có thể thêm `roomTypeId`, `view`, `minPrice`, `maxPrice`; giá lọc là tổng kỳ ở.
2. Khách tạo POST `/bookings` với `room_ids`, `check_in`, `check_out`, `adults`, `children`, `special_request`. Nhân viên tạo `/bookings/counter` với `{ "customer_id": 1, "channel": "QUAY", "booking": {...} }`; `DIEN_THOAI` cũng hợp lệ.
3. Booking HOLD 15 phút. Khách gọi POST `/bookings/{id}/deposit-payment`, hiển thị QR trả về; gọi POST `/deposit-payments/{orderId}/inquiry` để xác minh. Số tiền lấy từ chính sách server, mặc định 30% nếu chưa cấu hình.
4. Thu cọc tại quầy bằng `/bookings/{id}:confirm-deposit`:

```json
{
  "payment_method_id": 1,
  "amount": 300000,
  "payment_gateway": "MANUAL",
  "transaction_id": "COUNTER-RECEIPT-0001",
  "shift_id": 1
}
```

Mã giao dịch phải ổn định khi retry. Cùng mã nhưng khác số tiền/phương thức bị từ chối. ID phương thức phải tra từ `/payment-methods`; không giả định ID trong ví dụ khớp môi trường của bạn. Không xác nhận QR thủ công qua route này.

5. Khách gửi `/bookings/{id}/change-requests`. Nhân viên dùng `:reschedule` hoặc `/bookings/{id}/rooms/{detailId}:move` rồi đánh dấu yêu cầu hoàn thành. GET booking trả `required_deposit` và `deposit_shortfall`; có thể thu bổ sung tại quầy khi booking đã BOOKED.
6. POST `/bookings/{id}:check-in` với `{}`: phòng phải `SAN_SANG`; hóa đơn phòng được tạo nháp. Khai báo người ở qua `/bookings/{id}/rooms/{detailId}/guests`.
7. Tạo việc `KIEM_TRA` buồng phòng; ghi nhận và chốt chi phí sự cố trước khi phát hành hóa đơn. POST `/bookings/{id}/invoice:prepare` với `early_or_late_surcharge` bổ sung và `vat_rate` (ví dụ 0.1). Phụ thu theo chính sách được tính thêm tự động.
8. Thu phần `due` qua POST `/invoices/{id}:pay`, hoặc dùng VietQR hóa đơn có sẵn. Cho phép nhiều lần thu nhưng tổng không vượt dư nợ. Cọc được phân bổ tối đa bằng tổng hóa đơn.
9. POST `/bookings/{id}:check-out` với `{}` sau khi dư nợ bằng 0. Không truyền thêm phụ thu ở bước này. Phòng thành `CAN_DON`; hệ thống tạo việc `DON_CHECKOUT`. Hoàn tất việc dọn mới đưa phòng về sẵn sàng.

## Dịch vụ và hoàn tiền

POST `/service-requests` với `booking_id`, `room_id`, `service_id`, `quantity`, `note`. Khách chỉ tạo cho booking của mình đang ở. Nhân viên PATCH `status` theo `CHO_XU_LY → DANG_XU_LY → HOAN_THANH`; có thể hủy khi chưa bắt đầu. Giá được lưu tại thời điểm tạo yêu cầu, không phụ thuộc bảng giá sửa sau đó.

Hoàn thành tạo hóa đơn dịch vụ riêng. GET `/service-invoices/{id}`, POST `:pay` với `payment_method_id,amount,reference,shift_id`. Không cộng khoản này vào hóa đơn phòng.

Hủy booking: POST `/bookings/{id}:cancel` với `reason`; quản lý POST `/cancellation-requests/{id}:approve` với `approved_refund,note` hoặc `:reject` với `reason`. Số hoàn không vượt chính sách tại thời điểm yêu cầu; duyệt mới tạo phiếu hoàn chờ xử lý.

Thu nhầm hóa đơn: POST `/invoices/{id}:void` hoặc `/service-invoices/{id}:void` với `reason`, rồi POST `/refunds`. Mỗi phiếu hoàn chỉ có một trong `deposit_id`, `room_payment_id`, `service_payment_id`, kèm `payment_method_id,amount,reason`. Cọc dư sau checkout có thể tạo phiếu hoàn tương tự. Tổng phiếu hoàn chờ xử lý và thành công không vượt nguồn còn khả dụng.

Sau khi đã thực sự hoàn tiền, nhân viên có `REFUND_CONFIRM` gọi `/refunds/{id}:confirm` với `reference` và tùy chọn `payment_method_id`. Với cọc QR, có thể chọn phương thức chuyển khoản thủ công để ghi nhận hoàn ngân hàng. API này không ra lệnh chuyển tiền. Chi tiết booking và hộp thư phản ánh kết quả hoàn cọc.

GET `/invoices/{id}/print` trả HTML in được; POST `:send` gửi đường dẫn hóa đơn vào hộp thư khách có tài khoản.

## Vận hành và kiểm thử

Worker kiểm tra mỗi phút, tối đa 100 booking hết HOLD/nhắc lịch mỗi đợt. Thông báo công việc, sự cố và thiếu hàng gửi cho tài khoản có quyền tương ứng, tôn trọng cài đặt nhận tin; cùng tập cảnh báo không gửi lại trong ngày.

Chạy `dotnet test Quanlychuoikhachsan.Api.Tests`. Test Swagger xuất JSON tại `Quanlychuoikhachsan.Api.Tests/bin/Debug/net10.0/api-openapi.json`. Chạy `python scripts/export_api_docs.py` sau khi test để cập nhật bản tài liệu; `python scripts/generate_api_permissions.py` tái sinh seed quyền từ controller.

Các ca tự động bao phủ phân quyền/ownership, logout, nhập/xuất FEFO, serial, điều chỉnh, hóa đơn không trùng, dịch vụ, đánh giá, cọc QR/tiền đến muộn, hoàn tiền và tài sản. EF InMemory không thực thi khóa/rollback/FK của MySQL: cần kiểm thử đồng thời và migration trên database thật trước triển khai.
