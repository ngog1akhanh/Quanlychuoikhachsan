# Quản lý khách sạn và website đặt phòng

Backend ASP.NET Core/.NET 10, Entity Framework Core 9, Pomelo MySQL; giữ cấu trúc `Api`, `Application`, `Domain`, `Infrastructure` và Razor Pages hiện có.

## Bản tích hợp LMT

WinForms từ RAR đã được đưa vào solution và kết nối với API mới qua lớp tương thích. Xem [hướng dẫn tích hợp](docs/LMT_INTEGRATION.md) để chạy, cập nhật quyền và kiểm tra dữ liệu thời gian cũ.

## Tài liệu API

- [Đối chiếu chức năng và các nghiệp vụ dùng chung](docs/API_FUNCTIONAL_COVERAGE.md).
- [Hướng dẫn tích hợp và ví dụ nhập hàng, đặt phòng, thanh toán](docs/API_WORKFLOWS.md).
- [Danh mục endpoint](docs/API_ENDPOINTS.md).
- [OpenAPI JSON để import vào Postman](docs/openapi.json).
- Swagger khi chạy Development: `/swagger`; JSON: `/swagger/v1/swagger.json`.

## Chuẩn bị database

Người clone repo mới cần chuyển sang nhánh `codex/integrate-lmt-api`, cài MySQL/MariaDB rồi import file SQL bên dưới trước khi chạy API. Bộ cài đã có sẵn trong Git, không cần RAR hoặc thư mục `review` trên máy người khác.

Với database mới, dùng một file [hotel_management_compatible.sql](Quanlychuoikhachsan/Database/Install/hotel_management_compatible.sql): **67 bảng, 5 view, 6 vai trò, 42 quyền**, đã kiểm thử trên MariaDB 11.4.13. Xem [đối chiếu schema và hướng dẫn cài/kết nối](docs/DATABASE_COMPATIBILITY.md).

Bộ cài tạo database riêng `hotel_management_compatible` và dừng nếu tên đã tồn tại khi chạy batch không dùng `--force`. Không chạy thêm các script nguồn sau khi import bundle.

Các script nguồn cũ, giữ để đối chiếu hoặc lập kế hoạch nâng cấp database đang có:

1. `Quanlychuoikhachsan/Database/Scripts/hotel_management_final_food_inventory.sql`.
2. `Quanlychuoikhachsan/Database/Scripts/add_vietqr_payment_order.sql`.
3. `Quanlychuoikhachsan/Database/Scripts/add_operational_api_support.sql`.
4. `Quanlychuoikhachsan/Database/Scripts/seed_api_permissions.sql`.

Với database đã có, chỉ áp dụng các script bổ sung chưa chạy; không chạy lại schema gốc hoặc bộ cài mới. Script quyền thêm quyền và liên kết vai trò còn thiếu, không tạo tài khoản/mật khẩu mặc định. Các ràng buộc bổ sung của bản cài mới cần được kiểm tra với dữ liệu cũ trước khi lập migration.

Tài khoản quản trị đầu tiên cần có hồ sơ nhân viên đang làm việc và liên kết vai trò `ADMIN` trong database. Dùng mật khẩu băm bởi ASP.NET Core Identity; không nhập mật khẩu dạng rõ vào `mat_khau_hash`. Admin hiện có có thể tạo nhân viên và gán vai trò qua API.

## Cấu hình và chạy

Cấu hình bằng User Secrets hoặc biến môi trường (`__` thay `:`):

| Khóa | Ý nghĩa |
|---|---|
| `ConnectionStrings:HotelManagement` | Chuỗi kết nối tới database đã áp dụng script |
| `Database:ServerVersion` | Mặc định `8.0.36-mysql`; nếu dùng MariaDB, đặt đúng phiên bản, ví dụ `10.4.32-mariadb` |
| `Jwt:SigningKey` | Khóa bí mật ngẫu nhiên tối thiểu 32 ký tự |
| `Jwt:Issuer`, `Jwt:Audience` | Issuer/audience của ứng dụng |
| `VietQR:Username`, `VietQR:Password` | Thông tin gateway sandbox hiện có |
| `VietQR:BankAccount`, `VietQR:BankCode`, `VietQR:UserBankName` | Tài khoản nhận tiền |
| `Smtp:Host`, `Smtp:Port`, `Smtp:From`, `Smtp:Username`, `Smtp:Password` | Gửi mã đặt lại mật khẩu; TLS, port mặc định 587 |
| `BookingMaintenance:Enabled` | Mặc định `true`: mỗi phút xử lý HOLD hết hạn, nhắc lịch và thông báo vận hành |

```powershell
dotnet restore Quanlychuoikhachsan.slnx
dotnet build Quanlychuoikhachsan.slnx
dotnet test Quanlychuoikhachsan.Api.Tests/Quanlychuoikhachsan.Api.Tests.csproj
dotnet run --project Quanlychuoikhachsan/Quanlychuoikhachsan.csproj
```

Cấu hình bí mật không được đưa vào Git. ServerVersion được cấu hình tường minh để khởi động Swagger không cần tự dò phiên bản database. API nghiệp vụ vẫn cần database hoạt động.

## Quy ước

- Base path `/v1`; JSON dùng `snake_case`, query dùng `camelCase`.
- Endpoint bảo vệ nhận `Authorization: Bearer <token>`. Các route công khai được ghi trong tài liệu endpoint.
- Giữ response cũ để hạn chế phá client: endpoint cũ có thể trả `{data, request_id}`, endpoint mới thường trả DTO trực tiếp; thanh toán VietQR hóa đơn giữ envelope riêng. Xem ví dụ tích hợp.
- Một số danh sách cũ dùng `pageSize/pageToken`; các danh sách mới dùng `page/pageSize/search/status`.
- ID nhân viên thu/tạo/xác nhận được lấy từ tài khoản đăng nhập; trường `employee_id` cũ chỉ giữ tương thích DTO.
- Phiên JWT cũ cần đăng nhập lại sau lần nâng cấp này. Đổi mật khẩu, khóa tài khoản và đăng xuất có hiệu lực ở backend.

## Kiểm chứng và giới hạn

Bộ kiểm thử đang dùng `Quanlychuoikhachsan.Api.Tests` gồm kiểm thử nghiệp vụ, HTTP với JWT thật và đối chiếu model/SQL. Dự án test payOS cũ không thuộc solution hiện tại và không đại diện cho tích hợp VietQR.

Đã đạt 55 test, gồm EF InMemory/HTTP và 4 test tích hợp MariaDB 11.4.13 thực tế: import schema, đối chiếu model, luồng nhập/xuất, FK/CHECK/unique, rollback, booking/VietQR, view công nợ và UTC. Test server thật cần biến `HOTEL_TEST_MYSQL`; thiếu biến này thì 4 test tích hợp sẽ bị bỏ qua. Chưa kiểm thử tải đồng thời hoặc MySQL riêng; chưa gọi ngân hàng hay SMTP thật. Gateway hiện có dùng VietQR sandbox. API hoàn tiền ghi nhận khoản đã hoàn thực tế, không tự chuyển tiền ngân hàng.

Ảnh phòng được quản lý bằng URL. Thông báo nghiệp vụ được gửi vào hộp thư trong ứng dụng; chưa có push/SMS/email nghiệp vụ ngoài email đặt lại mật khẩu. WinForms có màn hình tổng quan, phòng, booking và lễ tân; các module khác vẫn là màn hình tạm của LMT, dù API nghiệp vụ đã có.
