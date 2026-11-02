# Cập nhật 01/11/2026 – Hoàn thiện App quản lý

Nhánh: `lmt` (đã fast-forward lên `trung` – commit `ec459d5 app and web`, rồi thêm commit này).
Phạm vi: 8 module WinForms còn thiếu, 1 controller API chỉ đọc, 1 sửa lỗi backend, dữ liệu demo, tài liệu.

## 1. Tóm tắt

| Trước | Sau |
|---|---|
| App quản lý chạy được 4 màn hình: Tổng quan, Phòng, Đặt phòng, Nhận/trả phòng; 8 module còn lại là màn hình tạm | Cả 12 module đều có màn hình thật, gọi API thật |
| Danh sách hóa đơn, công việc, phiếu cọc… chỉ có ID, không lọc/tìm được | Thêm 17 API `GET /v1/app/...` trả sẵn tên hiển thị, có tìm kiếm và phân trang |
| Seed demo không có dịch vụ, mặt hàng kho, dòng hóa đơn | Seed có đủ dữ liệu để demo mọi module |

## 2. Các module mới

| Module | Tab | Thao tác |
|---|---|---|
| Hóa đơn & thanh toán | Hóa đơn phòng · Hóa đơn dịch vụ · Phiếu cọc · Phiếu hoàn tiền · Ca thu ngân | Chốt hóa đơn, thu tiền nhiều lần/nhiều phương thức (gắn ca đang mở), in HTML, gửi khách, hủy; lập và xác nhận phiếu hoàn; mở ca, đối soát và đóng ca |
| Buồng phòng | Công việc · Sự cố & hư hỏng | Tạo việc dọn/kiểm tra, phân công, bắt đầu, hoàn thành (phòng về Sẵn sàng); ghi nhận sự cố, chi phí bồi thường, mở lại phòng sau bảo trì |
| Dịch vụ | Yêu cầu dịch vụ · Danh mục | Tạo yêu cầu cho phòng đang ở → tiếp nhận → hoàn thành (tự lập hóa đơn dịch vụ) hoặc hủy; thêm/sửa/ngừng bán dịch vụ |
| Khách hàng | Hồ sơ · Đánh giá | Tìm theo tên/SĐT/email/CCCD, thêm khách vãng lai, sửa hồ sơ, lịch sử đặt phòng; phản hồi, ẩn/hiện đánh giá |
| Báo cáo | Doanh thu · Đặt phòng & công suất · Dịch vụ · Khách hàng · Nhập–xuất–tồn · Vận hành hôm nay | Chọn khoảng ngày, thẻ KPI, biểu đồ cột/tròn, xuất CSV mở bằng Excel |
| Giá phòng & chính sách | Chính sách giá · Cọc · Hủy · Phụ thu sớm/trễ | Chính sách giá theo loại phòng, khoảng ngày, thứ trong tuần; thêm và bật/tắt chính sách cọc, hủy, phụ thu |
| Nhân viên & phân quyền | Nhân viên · Vai trò & quyền · Chức vụ · Nhật ký | Thêm/sửa nhân viên kèm tài khoản, gán nhiều vai trò, khóa/mở khóa, đặt lại mật khẩu, phân quyền theo nhóm, xem nhật ký |
| Kho & tài sản | Mặt hàng & tồn · Phiếu nhập · Phiếu xuất · Lô hàng · Sổ kho · Tài sản · Danh mục | Mặt hàng, điều chỉnh tồn; phiếu nhập nháp → xác nhận (nhập lô, HSD, serial); phiếu xuất FEFO; cảnh báo tồn thấp; chuyển/cập nhật tài sản; nhà cung cấp, kho, đơn vị, nhóm hàng |

Nút thao tác tự ẩn khi tài khoản thiếu quyền và tự mờ khi dòng đang chọn không hợp lệ (vd. chỉ thu tiền khi hóa đơn Đã phát hành / Thu một phần). Backend vẫn kiểm tra quyền thật ở mọi API.

## 3. File thay đổi

### WinForms (`HotelAdmin.WinForms/`)

| File | Nội dung |
|---|---|
| `Pages/BillingPage.cs`, `HousekeepingPage.cs`, `ServicesPage.cs`, `CustomersPage.cs`, `ReportsPage.cs`, `PricingPage.cs`, `StaffPage.cs`, `InventoryPage.cs` | 8 trang module |
| `Pages/ModulePage.cs` | Khung trang nhiều tab; tab chỉ tải khi mở lần đầu hoặc bấm Làm mới |
| `Controls/DataView.cs` | Danh sách dùng chung: tiêu đề + nút thao tác, tìm kiếm, lọc trạng thái, lưới, phân trang |
| `Controls/LinesEditor.cs` | Bảng nhập nhiều dòng (dòng phiếu, điều chỉnh giá, quy tắc hủy, đối soát ca) |
| `Forms/Dialogs/FormDialog.cs` | Hộp thoại nhập liệu theo khai báo trường; khóa trường = tên JSON gửi API |
| `Forms/Dialogs/DetailDialog.cs`, `ChecklistDialog.cs` | Xem chi tiết; chọn nhiều mục có nhóm (vai trò, quyền) |
| `Models/J.cs` | Đọc JSON an toàn, đổi giờ UTC sang giờ máy |
| `Services/ApiClient.Json.cs`, `Services/Lookups.cs` | Gọi API trả JSON; danh mục cho ComboBox có bộ nhớ đệm |
| `Theme/Labels.cs` | Nhãn tiếng Việt và màu cho mọi mã trạng thái/loại |
| `Forms/FrmMain.cs` | Nối 8 trang mới vào menu |

Các trang mới đọc JSON theo tên trường API nên không cần thêm model C#. Muốn thêm một danh sách mới: tạo `DataView`, khai báo `Columns(...)`, `Loader` và `AddAction(...)`.

### Backend (`Quanlychuoikhachsan/`)

| File | Nội dung |
|---|---|
| `Api/Controllers/AppViewsController.cs` | 17 API chỉ đọc cho App (mục 4) |
| `Application/Housekeeping/IHousekeepingService.cs` | Sửa lỗi: sự cố mức Nghiêm trọng không còn đổi phòng đang có khách sang Bảo trì |
| `Database/Scripts/seed_demo.sql` | Thêm dòng chi tiết hóa đơn, 6 dịch vụ, 2 nhà cung cấp, 6 mặt hàng |

### Tài liệu và dữ liệu

| File | Thay đổi |
|---|---|
| `docs/HUONG_DAN_BACKEND.md` | Viết lại cho dễ đọc; gộp phần quy tắc nghiệp vụ, vai trò, múi giờ từ `LMT_INTEGRATION.md` |
| `HotelAdmin.WinForms/README.md` | Viết lại: cách chạy, danh sách module, cấu trúc thư mục |
| `README.md` | Thứ tự 5 script database, tài khoản demo |
| `tai khoan test demo.txt` | Thêm 4 tài khoản nhân viên |
| `docs/BAO_CAO_TICH_HOP.md`, `docs/LMT_INTEGRATION.md` | **Đã xóa** – nội dung lỗi thời; phần còn dùng đã chuyển sang `HUONG_DAN_BACKEND.md` |

## 4. API mới `GET /v1/app/...`

Chỉ đọc, quyền giống API gốc tương ứng, thời gian trả về là UTC (có `Z`). Danh sách phân trang nhận `page`, `pageSize`, `search`, `status` và trả `items, total, page, page_size`.

| Endpoint | Quyền | Nội dung |
|---|---|---|
| `invoices` | BILLING_READ | Hóa đơn phòng + mã booking, khách, phòng, cọc trừ, đã thu, còn nợ |
| `service-invoices` | BILLING_READ | Hóa đơn dịch vụ + tên dịch vụ, còn nợ |
| `deposits` | BILLING_READ | Phiếu cọc + đã trừ, đã hoàn, còn có thể hoàn |
| `refunds` | BILLING_READ | Phiếu hoàn + nguồn tiền, mã booking |
| `shifts` | BILLING_READ | Ca thu ngân + tên nhân viên, cờ `mine` |
| `housekeeping-tasks` (`mine=true`) | HOUSEKEEPING_READ | Công việc + số phòng, người làm, ưu tiên |
| `room-conditions` | HOUSEKEEPING_READ | Sự cố + số phòng, booking, người ghi nhận |
| `employees` | HOUSEKEEPING_READ | Nhân viên đang làm (phân công) |
| `service-requests` | SERVICE_READ | Yêu cầu dịch vụ + khách, phòng, tên dịch vụ |
| `active-stays` | SERVICE_READ | Booking đang ở + phòng |
| `customers` | CUSTOMER_READ | Khách hàng + số lần ở, lần ở gần nhất, tổng chi |
| `reviews` | REVIEW_MANAGE | Đánh giá + tên khách, mã booking |
| `staff` | STAFF_READ | Nhân viên + `account_id`, trạng thái tài khoản, vai trò |
| `cancellation-policies` | PRICING_WRITE | Mọi chính sách hủy, kể cả đã ngừng |
| `inventory-items` (`warehouseId`) | INVENTORY_READ | Mặt hàng + đơn vị, nhóm, tồn, cảnh báo |
| `assets` | INVENTORY_READ | Tài sản + tên hàng, vị trí |
| `rooms` | INVENTORY_READ | Danh sách phòng tối giản cho nhân viên kho |

`docs/API_ENDPOINTS.md` chưa có các route này; chạy `dotnet test` rồi `python scripts/export_api_docs.py` để sinh lại.

## 5. Cài đặt cho thành viên khác

1. Pull nhánh `lmt`, build `HeThongQuanLyKhachSan.sln`.
2. Tạo lại database `hotel_management`, chạy lần lượt: `hotel_management_final_food_inventory.sql` → `add_vietqr_payment_order.sql` → `add_operational_api_support.sql` → `seed_api_permissions.sql` → `seed_demo.sql`.
3. Chạy `chay-he-thong.bat` (API cổng 5005 profile http, Web 5100, App quản lý).
4. Đăng nhập App bằng tài khoản trong `tai khoan test demo.txt` (mật khẩu `12345`): `admin.com`, `quanly@hotel.com`, `letan@hotel.com`, `buongphong@hotel.com`, `dichvu@hotel.com`, `kho@hotel.com`.

## 6. Kịch bản demo

- **Lễ tân** (`letan@hotel.com`): Nhận/trả phòng → chốt hóa đơn khách đang ở → Hóa đơn & thanh toán → thu tiền → In.
- **Dịch vụ** (`dichvu@hotel.com`): tạo yêu cầu "Buffet sáng" cho phòng đang ở → Tiếp nhận → Hoàn thành; lễ tân thu tiền ở tab Hóa đơn dịch vụ.
- **Buồng phòng** (`buongphong@hotel.com`): công việc dọn phòng → Bắt đầu → Hoàn thành → phòng chuyển Sẵn sàng.
- **Kho** (`kho@hotel.com`): lập phiếu nhập "Nước suối" → Xác nhận nhập kho (mã lô + HSD) → xem Lô hàng, Sổ kho → lập phiếu xuất Minibar.
- **Quản lý** (`quanly@hotel.com`): Báo cáo tháng này → Xuất CSV; Giá phòng → thêm chính sách giảm giá cuối tuần.

## 7. Kiểm chứng

- Build Visual Studio (.NET 9): không báo lỗi; 3 cảnh báo CS8620 (thêm dòng có ô `null` vào `DataGridView`) đã sửa trong `DataView.cs`, `DetailDialog.cs`, `ReportsPage.cs`.
- 120 đường dẫn API mà các trang gọi khớp `docs/openapi.json` và controller mới.
- `seed_demo.sql` chạy trên database trống 2 lần liên tiếp không lỗi; tổng dòng hóa đơn khớp tổng hóa đơn; không trùng lịch phòng.

Chưa kiểm thử: thao tác thực tế trên từng màn hình với API đang chạy; `dotnet test` sau khi thêm `AppViewsController`.

## 8. Giới hạn còn lại

- Báo cáo lọc ngày theo mốc 00:00 trong khi dữ liệu lưu UTC: giao dịch 00:00–07:00 giờ VN có thể tính sang ngày trước.
- Chưa có màn hình Thông báo nội bộ (`/v1/notifications`).
- Thanh toán QR đặt cọc: đã hoàn thiện ở đợt 02/11 – xem [THANH_TOAN_QR_COC.md](THANH_TOAN_QR_COC.md).
