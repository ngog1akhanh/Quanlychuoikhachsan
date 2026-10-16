# Quản lý chuỗi khách sạn

Backend .NET 10 sử dụng Razor Pages, ASP.NET Core Web API, Entity Framework Core và MySQL/MariaDB. API được chia theo các lớp `Api`, `Application`, `Domain` và `Infrastructure`.

## Công nghệ và cấu trúc

- .NET 10 / Razor Pages và ASP.NET Core Web API.
- MySQL 8 hoặc MariaDB 10.4+ qua Pomelo Entity Framework Core.
- JWT Bearer authentication; quyền được kiểm tra từ quan hệ tài khoản-vai trò-quyền trong cơ sở dữ liệu.
- Swagger được bật trong môi trường Development tại `/swagger`.
- `Quanlychuoikhachsan/Pages` chứa Razor Pages; `Api/Controllers` định nghĩa HTTP API; `Application` chứa DTO và nghiệp vụ; `Infrastructure` chứa tích hợp, xác thực, middleware và truy cập dữ liệu.

## Chạy ứng dụng

Yêu cầu .NET 10 SDK và một máy chủ MySQL 8/MariaDB 10.4+.

1. Tạo cơ sở dữ liệu ban đầu bằng script `Quanlychuoikhachsan/Database/Scripts/hotel_management_final_food_inventory.sql`. Script tạo database `hotel_management`, các bảng, view và dữ liệu danh mục khởi đầu. Chạy trên database mới; không chạy lại nguyên script trên database đã có schema vì phần lớn lệnh tạo bảng không dùng `IF NOT EXISTS`.
2. Cấu hình chuỗi kết nối, JWT và thông tin VietQR sandbox/nhận tiền bằng User Secrets hoặc biến môi trường. Không lưu thông tin thật trong Git.
3. Khôi phục package và chạy ứng dụng:

```powershell
dotnet restore Quanlychuoikhachsan/Quanlychuoikhachsan.csproj
dotnet run --project Quanlychuoikhachsan/Quanlychuoikhachsan.csproj
```

Build toàn solution:

```powershell
dotnet build Quanlychuoikhachsan.slnx
```

Khi chạy ở Development, Swagger UI ở `/swagger`. Các URL cổng local được khai báo trong `Quanlychuoikhachsan/Properties/launchSettings.json`.

### Cấu hình

Project đã khai báo `UserSecretsId`. Ví dụ cấu hình từ thư mục solution:

```powershell
dotnet user-secrets set "ConnectionStrings:HotelManagement" "Server=localhost;Port=3306;Database=hotel_management;User=hotel_app;Password=<password>;CharSet=utf8mb4;Allow User Variables=true" --project Quanlychuoikhachsan/Quanlychuoikhachsan.csproj
dotnet user-secrets set "Jwt:SigningKey" "<random-secret-at-least-32-characters>" --project Quanlychuoikhachsan/Quanlychuoikhachsan.csproj
dotnet user-secrets set "Jwt:Issuer" "hotel-management-api" --project Quanlychuoikhachsan/Quanlychuoikhachsan.csproj
dotnet user-secrets set "Jwt:Audience" "hotel-management-client" --project Quanlychuoikhachsan/Quanlychuoikhachsan.csproj
```

Cấu hình tài khoản sandbox VietQR và tài khoản nhận tiền:

- `VietQR:Username`
- `VietQR:Password`
- `VietQR:BankAccount`
- `VietQR:BankCode`
- `VietQR:UserBankName`

Dùng `__` thay cho `:` trong tên biến môi trường, ví dụ `VietQR__Username`. Tài khoản sandbox được gửi Basic Authentication để lấy token; tạo QR và inquiry dùng Bearer token. Endpoint hiện cấu hình trong gateway là endpoint sandbox `api.vietqr.org`; chưa cấu hình endpoint production.

Sau khi chạy schema chính trên database mới, chạy thêm `Quanlychuoikhachsan/Database/Scripts/add_vietqr_payment_order.sql` để tạo bảng đơn VietQR, phương thức thanh toán và quyền API. Script gán quyền đọc và ghi chú cho vai trò `ADMIN`/`MANAGER` nếu các vai trò này đã tồn tại; quyền cập nhật trạng thái chỉ gán cho `ADMIN`. Nếu vai trò được tạo sau đó, cần gán quyền tương ứng riêng.

## Quy ước API

- Base path: `/v1`.
- JSON body và JSON response dùng `snake_case`; tên query parameter dùng `camelCase` (ví dụ `pageSize`, `pageToken`, `checkInDate`).
- API bảo vệ yêu cầu header `Authorization: Bearer <access_token>`, trừ hai API đăng ký/đăng nhập.
- Các response chưa được chuẩn hóa đồng nhất: nhiều endpoint trả DTO trực tiếp; endpoint tạo tài nguyên có thể trả `data`/`request_id`; API thanh toán trả `success`/`message`/`data`/`request_id`. Endpoint có thể phát sinh lỗi trả Google-style error hoặc payment envelope tùy nhóm API.
- Xem Swagger trong Development hoặc DTO/controller tương ứng để biết đầy đủ field, validation và schema response.

## Danh sách API hiện có

### Tài khoản

| Method | Endpoint | Quyền / chức năng |
|---|---|---|
| `POST` | `/v1/auth/register` | Công khai; đăng ký khách hàng, trả `201 Created`. |
| `POST` | `/v1/auth/login` | Công khai; đăng nhập bằng email hoặc số điện thoại và mật khẩu, trả JWT. |

### Đặt phòng, nhận/trả phòng và hóa đơn

| Method | Endpoint | Quyền / chức năng |
|---|---|---|
| `POST` | `/v1/bookings` | JWT; tạo đặt phòng cho tài khoản đăng nhập. |
| `PATCH` | `/v1/bookings/{id}` | JWT; cập nhật yêu cầu đặc biệt của đặt phòng đang giữ chỗ. |
| `POST` | `/v1/bookings/{id}:confirm-deposit` | JWT; xác nhận tiền cọc theo thông tin request. |
| `POST` | `/v1/bookings/{id}:check-in` | JWT; nhận phòng và tạo hóa đơn ban đầu. |
| `POST` | `/v1/bookings/{id}:check-out` | JWT; trả phòng và chốt các khoản hóa đơn. |
| `POST` | `/v1/bookings/{id}:cancel` | JWT; tạo yêu cầu hủy đặt phòng. |
| `POST` | `/v1/bookings/{id}:mark-no-show` | JWT; ghi nhận khách không đến. |
| `POST` | `/v1/cancellation-requests/{id}:approve` | JWT; duyệt yêu cầu hủy. |
| `POST` | `/v1/invoices/{id}:pay` | JWT + `BILLING_PAYMENT_WRITE`; ghi nhận thanh toán thủ công. Không cho ghi nhận VietQR qua API này. |
| `GET` | `/v1/bookings` | JWT; danh sách phân trang bằng `pageSize`/`pageToken`. |
| `GET` | `/v1/invoices` | JWT; danh sách hóa đơn phân trang. |
| `GET` | `/v1/transactions` | JWT; danh sách giao dịch phân trang. |

Các endpoint phân trang dùng cursor `pageToken`; kích thước trang mặc định 20, tối đa 100. API công khai để tìm phòng trống chưa được expose dù service có logic truy vấn nội bộ.

### Khách hàng, nhân viên và phân quyền

| Method | Endpoint | Quyền |
|---|---|---|
| `GET` | `/v1/customers` | `CUSTOMER_READ`; phân trang bằng `pageSize`/`pageToken`. |
| `GET` | `/v1/customers/{id}` | `CUSTOMER_READ`. |
| `PATCH` | `/v1/customers/{id}` | `CUSTOMER_WRITE`. |
| `GET` | `/v1/staff` | `STAFF_READ`. |
| `GET` | `/v1/staff/{id}` | `STAFF_READ`. |
| `GET` | `/v1/roles` | `ROLE_READ`. |
| `GET` | `/v1/roles/{id}` | `ROLE_READ`. |

### Phòng và loại phòng

| Method | Endpoint | Quyền / chức năng |
|---|---|---|
| `GET` | `/v1/rooms` | `ROOM_READ`; danh sách phòng. |
| `GET` | `/v1/rooms/{id}` | `ROOM_READ`; chi tiết phòng. |
| `GET` | `/v1/rooms/{id}/price?checkInDate=YYYY-MM-DD&checkOutDate=YYYY-MM-DD` | `ROOM_READ`; xem trước giá. |
| `POST` | `/v1/rooms` | `ROOM_WRITE`; tạo phòng. |
| `GET` | `/v1/room-types` | `ROOM_TYPE_READ`; danh sách loại phòng. |
| `GET` | `/v1/room-types/{id}` | `ROOM_TYPE_READ`; chi tiết loại phòng. |
| `POST` | `/v1/room-types` | `ROOM_TYPE_WRITE`; tạo loại phòng. |

### Kho và buồng phòng

Các endpoint dưới đây yêu cầu JWT. Controller hiện không gắn permission cụ thể cho từng thao tác nhóm này.

| Method | Endpoint | Chức năng |
|---|---|---|
| `GET` | `/v1/inventory-items` | Danh sách mặt hàng và số lượng tồn. |
| `POST` | `/v1/inventory-items` | Tạo mặt hàng. |
| `POST` | `/v1/purchase-orders` | Tạo phiếu nhập. |
| `POST` | `/v1/purchase-orders/{id}:confirm` | Xác nhận phiếu nhập và phân bổ hàng nhận. |
| `POST` | `/v1/stock-issues` | Tạo phiếu xuất kho. |
| `POST` | `/v1/stock-issues/{id}:confirm` | Xác nhận phiếu xuất kho. |
| `GET` | `/v1/warehouses/{warehouseId}/inventory-items/{itemId}/balance` | Xem tồn kho một mặt hàng tại kho. |
| `GET` | `/v1/housekeeping-tasks` | Danh sách công việc buồng phòng. |
| `POST` | `/v1/housekeeping-tasks/checkout` | Tạo công việc dọn phòng sau checkout. |
| `POST` | `/v1/room-conditions` | Ghi nhận tình trạng phòng. |

### Báo cáo

Các endpoint yêu cầu JWT; `from` và `to` là ngày theo định dạng `YYYY-MM-DD`, `to` phải bằng hoặc sau `from`.

| Method | Endpoint |
|---|---|
| `GET` | `/v1/reports/dashboard?from=YYYY-MM-DD&to=YYYY-MM-DD` |
| `GET` | `/v1/reports/revenue?from=YYYY-MM-DD&to=YYYY-MM-DD` |
| `GET` | `/v1/reports/occupancy?from=YYYY-MM-DD&to=YYYY-MM-DD` |

### Thanh toán hóa đơn VietQR

| Method | Endpoint | Quyền / chức năng |
|---|---|---|
| `POST` | `/v1/invoices/{invoiceId}/vietqr-payment` | JWT của tài khoản tạo đặt phòng; tạo QR cho số dư còn lại do server tính, tài khoản nhận lấy từ cấu hình. Đơn được lưu với trạng thái `PENDING`. |
| `POST` | `/v1/payments/vietqr/{orderId}/inquiry` | JWT của tài khoản tạo đơn; gọi Inquiry VietQR. Chỉ khi mã đơn/số tiền khớp, trạng thái thành công và có mã giao dịch mới ghi nhận thanh toán. |
| `GET` | `/v1/payments/vietqr?status=PENDING` | `VIETQR_PAYMENT_READ`; danh sách đơn thanh toán. |
| `PUT` | `/v1/payments/vietqr/{orderId}/note` | `VIETQR_PAYMENT_NOTE_WRITE`; cập nhật ghi chú. |
| `PATCH` | `/v1/payments/vietqr/{orderId}/status` | `VIETQR_PAYMENT_STATUS_WRITE`; chỉ ADMIN; không cho đặt thủ công `PAID` hoặc `REFUNDED`. |

Theo quy tắc phân quyền dự án, ADMIN quản lý trạng thái và ghi chú; MANAGER chỉ xem và sửa ghi chú, không được tự ghi nhận thanh toán thành công. Chỉ webhook payOS đã xác minh mới được đánh dấu thanh toán payOS là `PAID`.

Cấu hình URL webhook trên payOS là `https://<domain>/v1/payments/payos/webhook`.

> **Tình trạng database payOS:** script `Quanlychuoikhachsan/Database/Scripts/add_payos_payment_order.sql` hiện đang rỗng. Schema chính cũng chưa tạo bảng `payos_payment_order`. Cần bổ sung và áp dụng migration/schema cùng dữ liệu phương thức `PAYOS` và quyền tương ứng trước khi sử dụng thanh toán payOS; không coi script rỗng là migration đã chạy.

### VietQR Sandbox

| Method | Endpoint | Quyền / chức năng |
|---|---|---|
| `POST` | `/v1/invoices/{invoiceId}/vietqr-payment` | JWT; tạo QR thanh toán hóa đơn từ số dư còn lại tính ở server. |

Ví dụ body theo quy ước JSON của ứng dụng:

QR được lưu trong đơn `PENDING`; hóa đơn chỉ được cập nhật sau Inquiry đã xác thực khớp mã đơn, số tiền, trạng thái thành công và mã giao dịch duy nhất. Không nhận số tiền hoặc tài khoản ngân hàng từ client.

## Ghi chú database

- Script chính dùng cú pháp MySQL/MariaDB, không phải SQL Server/T-SQL.
- EF Core được cấu hình kết nối tới MySQL; script SQL là schema khởi tạo, không phải EF migration tự động.
- Script chính có dữ liệu khởi đầu cho đơn vị tính, nhóm hàng và kho; chưa thay thế quy trình tạo tài khoản, gán vai trò/quyền hoặc dữ liệu cấu hình mọi nghiệp vụ.
- Không có bộ test project trong solution hiện tại.

### Các điểm cần rà soát trước khi bổ sung migration kho

- Schema hiện đã có unique key cho lịch phòng theo đêm, chi tiết booking theo đêm, giao dịch cọc qua gateway và mã lô trong kho/mặt hàng; không tạo lại các ràng buộc này.
- Rà soát database ghi nhận ledger kho hiện có các khóa ngoại riêng cho lô, kho và mặt hàng, nhưng các khóa đó không tự bảo đảm cả bộ giá trị thuộc cùng một lô. Quy ước dấu lượng theo loại giao dịch cũng cần được duy trì ở service; đánh giá xem có cần ràng buộc database bổ sung.
- Ràng buộc dấu lượng ledger, kiểm tra giá trị hóa đơn và điều kiện ngày hết hạn của lô có thể cần được siết chặt. Trước khi đổi schema, kiểm tra dữ liệu hiện hữu và xác nhận yêu cầu nghiệp vụ, đặc biệt precision của `chi_tiet_hoa_don_phong.so_luong` (`DECIMAL(12,2)`) so với lượng kho (`DECIMAL(15,3)`).
- Chỉ mục ghép cho truy vấn FEFO là đề xuất hiệu năng, cần xác nhận bằng `EXPLAIN` trên workload thực tế; không loại bỏ chỉ mục đơn nếu chưa rà soát các truy vấn khác.
- Tồn kho được tính từ `giao_dich_kho`; các bất biến tổng nhiều dòng, chống xác nhận lặp và ghi ledger nguyên tử cần được bảo đảm bằng transaction/khóa trong service, không thể thay bằng một `CHECK` đơn dòng.
- Các đề xuất migration trong tài liệu rà soát cũ chưa được chạy hoặc kiểm thử trên database thật. Sao lưu, truy vấn tìm dữ liệu vi phạm, thử trên staging và kiểm tra tương thích phiên bản MySQL trước khi viết/chạy migration. MySQL chỉ thực thi `CHECK` từ phiên bản 8.0.16 trở lên.
