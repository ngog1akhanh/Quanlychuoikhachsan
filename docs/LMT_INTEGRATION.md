# Tích hợp LMT với API mới — 03/11/2026

## Nguồn và nhánh làm việc

- Nhánh nhận: `codex/integrate-lmt-api`, nền trước merge `78887f5` (đã có bộ SQL tương thích).
- Nguồn merge: `origin/lmt` tại `ad78332` (QR demo và báo cáo hóa đơn), bao gồm `db1d4c1`, gồm module LMT ở `3fad2eb` và website từ Trung ở `ec459d5`.
- Giữ nguyên nhánh `lmt`; chỉ commit/push nhánh tích hợp. Lịch sử hai nhánh được giữ bằng merge commit.
- Dùng source trong `HeThongQuanLyKhachSan.sln`; RAR cũ và project thử payOS không còn dùng đã được LMT bỏ. Không giải nén RAR đè lên source hiện tại.
- Bốn project backend, web, WinForms và tests thống nhất .NET 10. EF Core/Pomelo vẫn dùng 9 để giữ tương thích database.
- Bộ cài database cho người clone mới nằm tại `Database/Install/hotel_management_compatible.sql` trong project backend; xem [hướng dẫn database](DATABASE_COMPATIBILITY.md).

## Nguyên tắc hợp nhất

API mới là nguồn nghiệp vụ chính. Hai controller `FrontDeskController`, `RoomAdminController` giữ đường dẫn và DTO mà WinForms sử dụng, chuyển thao tác về dịch vụ chung.

| Nhóm | Hợp đồng WinForms | Dịch vụ dùng chung |
|---|---|---|
| Đặt phòng, cọc, đổi ngày/phòng, hủy, no-show | `/v1/front-desk/...` | `BookingService` |
| Khai báo lưu trú | `PUT /v1/front-desk/bookings/{id}/guests` | `BookingGuests`: giới hạn số khách, đại diện, giấy tờ và ngày sinh |
| Nhận/trả phòng, hóa đơn | Route lễ tân + route chốt/thu tiền mới | Một hóa đơn được tạo nháp, chốt rồi thanh toán |
| Phòng và loại phòng | `room-board`, `room-calendar`, các route PATCH/`:set-status` | `RoomAdminService`; PUT/status của API mới cũng dùng dịch vụ này |
| Tài khoản | `GET /v1/me` | Giữ trường hồ sơ khách cũ, bổ sung account/staff/roles/permissions |
| Nhập hàng và các nhóm API đã bổ sung | Route của bộ API mới | Giữ phần kho, dịch vụ, hóa đơn, báo cáo, phân quyền... |

Không nhập lại `FrontDeskEntities` và cấu hình EF của RAR: các entity đó đã có trong `OperationalEntities`/`OperationalModel`. Không chạy song song hai triển khai đặt phòng hoặc tạo thêm lịch sử/hóa đơn sau khi dịch vụ chính đã thực hiện.

## Thay đổi cần biết khi nâng cấp client

Đây là bản tích hợp có cập nhật WinForms đi kèm, không phải cam kết mọi hành vi của file `.exe` cũ được giữ nguyên.

- Chưa thu đủ cọc: giữ phòng **15 phút**, thống nhất API mới; không còn thời hạn 24 giờ riêng của lễ tân cũ.
- Chỉ nhận phòng khi đến ngày lưu trú và mọi phòng đã sẵn sàng. Không hỗ trợ `allow_not_ready_rooms=true`.
- Trả phòng theo ba bước rõ ràng trong WinForms: **chốt hóa đơn → xác nhận đã thu tiền → trả phòng**. API trả phòng từ chối hóa đơn chưa phát hành hoặc chưa trả đủ. Phụ thu nhập tay là phần thêm ngoài phụ thu chính sách.
- Hóa đơn đã phát hành không được sửa âm thầm; sử dụng nghiệp vụ hủy/thay thế hóa đơn. Gửi lại cùng `transaction_id` và cùng dữ liệu thanh toán không thu lần hai.
- Cọc có trường `reference` tùy chọn. Client mới giữ reference khi thử lại. Client cũ không gửi reference dùng khóa `DESK-FIRST-{bookingId}` cho lần cọc đầu; muốn cọc thêm phải gửi reference mới.
- Yêu cầu hủy chờ duyệt; duyệt hủy tạo phiếu hoàn `CHO_XU_LY`. Đây chưa phải chuyển tiền ngân hàng thực tế.
- Thu cọc cần `BILLING_PAYMENT_WRITE`, kể cả cọc đi kèm tạo booking. Tạo khách vãng lai cần `CUSTOMER_WRITE`. Không dùng route lễ tân để bỏ qua quyền của API mới.
- Nút trả phòng cần `BOOKING_WRITE`, `BILLING_READ`, `BILLING_WRITE`, `BILLING_PAYMENT_WRITE`. Duyệt/từ chối hủy dùng `CANCELLATION_APPROVE`; đổi trạng thái phòng dùng `ROOM_WRITE`.

## Quyền và dữ liệu thời gian

Với database mới, bundle `hotel_management_compatible.sql` đã bao gồm quyền. Với database cũ đã có đủ schema bổ sung, chạy lại `Database/Scripts/seed_api_permissions.sql`. Script thêm quyền còn thiếu, giữ nguyên các liên kết tài khoản hiện hữu. Script cũng bổ sung quyền tương ứng cho vai trò cũ nếu chúng tồn tại:

| Mã LMT | Mã vai trò chuẩn |
|---|---|
| `LE_TAN` | `RECEPTIONIST` |
| `BUONG_PHONG` | `HOUSEKEEPING` |
| `DICH_VU` | `SERVICE` |
| `KHO` | `WAREHOUSE` |

WinForms nhận cả hai mã khi dựng menu; backend luôn kiểm tra quyền thực tế. Script không tự gán quyền cho tài khoản không có vai trò. Vai trò tùy chỉnh cần được cấp các quyền cần dùng bởi quản trị viên.

`HotelTime` quy đổi giờ quầy theo `Asia/Ho_Chi_Minh`, độc lập múi giờ máy chủ. Adapter nhận/trả giờ địa phương không kèm offset cho WinForms; dữ liệu thời điểm lưu UTC. Ngày đêm phòng được tính theo ngày tại khách sạn, kể cả trước 07:00. Đối với API mới, nên gửi thời gian kèm `Z` hoặc offset rõ ràng.

**Dữ liệu đã ghi bằng backend LMT cũ cần được đối chiếu riêng:** backend đó có chỗ lưu giờ địa phương, có chỗ lưu UTC. Bản tích hợp không tự trừ 7 giờ hàng loạt. Trước khi dùng database cũ, xác định nguồn các bản ghi `du_kien_nhan`, `du_kien_tra` và thời điểm thực tế; chuyển đổi theo nguồn trên bản sao rồi kiểm chứng. Ngày `gia_dem_dat_phong`/`khoa_lich_phong` là ngày lưu trú, không phải timestamp để trừ giờ.

## Chạy solution

1. Chuẩn bị database và cấu hình backend theo [README](../README.md).
2. Build `HeThongQuanLyKhachSan.sln` (backend, web, tests và WinForms).
3. Chạy backend. Chỉnh `HotelAdmin.WinForms/appsettings.json` → `ApiBaseUrl` về URL backend đang chạy.
4. Chạy WinForms trên Windows, đăng nhập lại để nhận JWT hợp lệ với phiên bản mới.

```powershell
dotnet build HeThongQuanLyKhachSan.sln
dotnet test Quanlychuoikhachsan.Api.Tests/Quanlychuoikhachsan.Api.Tests.csproj
dotnet run --project Quanlychuoikhachsan --launch-profile http
```

Trong các terminal khác:

```powershell
dotnet run --project HotelAdmin.WinForms
dotnet run --project QuanLyKhachSan.Web --launch-profile http
```

LMT đã bổ sung 8 module WinForms: hóa đơn, buồng phòng, dịch vụ, khách hàng, báo cáo, giá/chính sách, nhân viên/phân quyền, kho/tài sản. Cùng 4 trang cũ, tổng cộng 12 trang thực thi. Controller `AppViews` bổ sung 18 API GET `/v1/app/...` ghép tên hiển thị, dùng cùng database và hệ thống quyền; thao tác ghi vẫn gọi các service nghiệp vụ hiện hữu.

Website Blazor dùng API thật khi `Api:UseMockData=false`. Trang chủ lấy loại phòng và giá cơ bản từ database; link chọn loại phòng truyền đúng query `type`. Giá cuối cùng theo ngày lưu trú được lấy khi tìm và đặt phòng.

`seed_demo.sql` là tùy chọn, chạy trên database demo đã chọn, lưu timestamp UTC và tính ngày demo theo Việt Nam. Bundle cài mới vẫn độc lập dữ liệu demo và không tạo mật khẩu quản trị chung.

## Clean code

- Tách interface booking khỏi implementation; chia phần khai báo khách, từ chối hủy và dự toán trả phòng thành các file nghiệp vụ riêng.
- Tách DTO/interface lễ tân và quản lý phòng khỏi implementation.
- Loại bỏ business logic trùng của LMT, dùng một phép tính phụ thu cho dự toán và hóa đơn.
- Các thao tác ghép như tạo khách + booking + cọc, hoặc lưu khách + nhận phòng dùng chung giao dịch; chỉ lớp sở hữu giao dịch ngoài commit.
- Dọn sáu cảnh báo WinForms về nullability/tên property che khuất `Control.Created`; sửa lifetime của HTTP request và chỉ chuyển tiếp Bearer khi redirect nâng HTTP lên HTTPS cùng host.
- Giữ `LangVersion=13.0` ở backend để các biểu thức LINQ `Contains` tương thích EF Core/Pomelo 9.
- Thư mục `review/` chỉ dùng đối chiếu và được bỏ qua bởi Git.
- Sửa query tổng tồn kho `/v1/inventory-items` bị lỗi 500 trên MariaDB do SUM lồng truy vấn GROUP BY; tính trực tiếp từ sổ giao dịch kho.
- Báo cáo doanh thu dùng biên ngày Việt Nam quy về UTC và nhóm theo ngày Việt Nam, tránh mất doanh thu 00:00–07:00 hoặc cộng nhầm ngày tiếp theo.
- Giữ sửa lỗi housekeeping của LMT: ghi sự cố nghiêm trọng không tự chuyển phòng đang có khách từ `DANG_O` sang `BAO_TRI`.
- Web chỉ giữ cấu hình API; bỏ các mục DB/JWT không dùng. Cập nhật MySqlConnector lên 2.6.1 theo [bản vá chính thức](https://github.com/advisories/GHSA-473q-m89c-ghf8).

## Kiểm chứng

- Build cả solution: **0 lỗi, 0 cảnh báo**.
- **66/66 kiểm thử** qua, 0 bỏ qua: 57 test nghiệp vụ/HTTP và 9 test tích hợp MariaDB thật. TRX sinh trong `review/test-results/` khi dùng logger TRX; không commit dữ liệu QA.
- HTTP tests gọi trực tiếp `ApiClient` và DTO của WinForms: hồ sơ nhân viên/khách, cọc và thử lại, nhận/trả phòng chỉ một hóa đơn, thanh toán chỉ một lần, công việc dọn phòng không trùng, hủy qua route cũ/duyệt qua route mới, quyền thu tiền, giới hạn khách, trạng thái phòng và ngày lưu trú quanh nửa đêm.
- OpenAPI: **245 thao tác HTTP / 200 đường dẫn**, không có action trùng. Xem [danh mục](API_ENDPOINTS.md) và [JSON](openapi.json).
- MariaDB 11.4.13: import bundle, model, FK/CHECK/unique, rollback, nhập/xuất, công nợ, UTC; chạy seed demo hai lần cùng ngày không tăng số booking/hóa đơn. Toàn bộ 18 route app trả đúng quyền: chưa đăng nhập 401, khách 403, admin 200; nhân viên kho không đọc được danh sách nhân viên.
- Hai HTTP request đặt cùng phòng/ngày đồng thời: một 201, một 409, đúng hai khóa cho hai đêm.
- Client web thật gọi API test host/database thật: đăng nhập, danh mục/giá trang chủ, tìm phòng, tạo HOLD, lịch sử, sửa ghi chú, tạo cọc, inquiry PAID lặp không thu hai lần, BOOKED và gửi yêu cầu hủy. Chỉ gateway ngân hàng được thay bằng fake.
- WinForms smoke với API/DB chạy thật: **12 trang, 43 tab; 90 request trên 54 đường dẫn API**, không lỗi HTTP. Đã xem ảnh render màn hình kho/báo cáo. Công cụ tải trang/tab, không bấm mọi nút CRUD.
- Trình duyệt: đăng nhập tài khoản khách demo, tìm phòng; kiểm tra danh mục/giá thật trên trang chủ và lọc Suite từ link trang chủ.
- Bản `ad78332`: thống kê hóa đơn theo kỳ/nhân viên/sản phẩm, tab đơn cọc QR, vẽ QR trên web, gia hạn giữ phòng khi tạo QR và xử lý tiền cọc thiếu/hết hạn. Hóa đơn được gán thời điểm và người phát hành khi chốt.
- Seed thống kê lịch sử chạy lặp không tăng số booking/hóa đơn; báo cáo kỳ và nhân viên khớp tổng tiền, doanh thu dịch vụ khớp tổng dòng dịch vụ. Khách hàng bị chặn cả ba API báo cáo.
- QR demo dùng chính `DemoVietQrGateway`: tạo QR/inquiry PENDING/xác nhận PAID hai lần chỉ một phiếu cọc; chặn khách khác, Live và Production. Trên trình duyệt đã tạo booking QA, hiển thị ảnh QR, giả lập cọc và xem chi tiết booking ở trạng thái Đã xác nhận.

Chạy lại kiểm tra màn hình trên database demo (API đang chạy):

```powershell
$env:HOTEL_SMOKE_IDENTIFIER = 'admin.com'
$env:HOTEL_SMOKE_PASSWORD = '12345' # Chỉ dùng với seed demo.
dotnet run --project tools/WinFormsSmoke -- http://localhost:5005/ review/winforms-acceptance
```

Phạm vi còn lại: chưa kiểm thử tải nhiều người dùng, deadlock diện rộng, MySQL riêng, ngân hàng/SMTP thật hoặc mọi thao tác ghi trên từng màn hình. Kết quả trên xác nhận các luồng đã kiểm tra, không phải cam kết không còn lỗi trong mọi tình huống.

Sau khi chạy test, sinh lại tài liệu bằng `python scripts/export_api_docs.py`. Khi đổi phân quyền controller, sinh lại SQL bằng `python scripts/generate_api_permissions.py` rồi rà soát trước khi áp dụng vào database.
