# Tích hợp LMT với API mới

## Nguồn và nhánh làm việc

- Nhánh chính của repo là `master` (`cc0ed99`), không có nhánh `main`.
- `lmt` (`4170245`) bổ sung `Quanlychuoikhachsan-master.rar` trên nền `master`.
- Nhánh tích hợp: `codex/integrate-lmt-api`, tạo từ `lmt`.
- Bộ API mới được khôi phục từ stash `On master: !!GitHub_Desktop<master>` bằng `stash apply`. Stash vẫn được giữ nguyên.
- WinForms và các hợp đồng API lễ tân/phòng được lấy từ RAR. Không đưa `bin`, `obj`, `.vs`, bản sao database hoặc cấu hình máy cũ vào mã nguồn tích hợp.
- Các thay đổi đang ở working tree; chưa commit/push hoặc cập nhật lại RAR. Khi chia sẻ bản mới, dùng solution trong repo, không giải nén RAR cũ đè lên mã nguồn này.

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

Chạy lại `Database/Scripts/seed_api_permissions.sql` trên database đã có đủ schema bổ sung. Script thêm quyền còn thiếu, giữ nguyên các liên kết tài khoản hiện hữu. Script cũng bổ sung quyền tương ứng cho vai trò cũ nếu chúng tồn tại:

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
2. Build `Quanlychuoikhachsan.slnx` (backend, tests và WinForms).
3. Chạy backend. Chỉnh `HotelAdmin.WinForms/appsettings.json` → `ApiBaseUrl` về URL backend đang chạy.
4. Chạy WinForms trên Windows, đăng nhập lại để nhận JWT hợp lệ với phiên bản mới.

```powershell
dotnet build Quanlychuoikhachsan.slnx
dotnet test Quanlychuoikhachsan.Api.Tests/Quanlychuoikhachsan.Api.Tests.csproj
dotnet run --project Quanlychuoikhachsan
```

Trong terminal khác:

```powershell
dotnet run --project HotelAdmin.WinForms
```

Các màn hình thực thi đã có từ LMT: tổng quan, phòng, đặt phòng và lễ tân. Các module khác vẫn là màn hình tạm của WinForms; API đã có không đồng nghĩa toàn bộ giao diện đã được triển khai. Danh mục API trên các màn hình tạm đã được cập nhật theo route mới.

## Clean code

- Tách interface booking khỏi implementation; chia phần khai báo khách, từ chối hủy và dự toán trả phòng thành các file nghiệp vụ riêng.
- Tách DTO/interface lễ tân và quản lý phòng khỏi implementation.
- Loại bỏ business logic trùng của LMT, dùng một phép tính phụ thu cho dự toán và hóa đơn.
- Các thao tác ghép như tạo khách + booking + cọc, hoặc lưu khách + nhận phòng dùng chung giao dịch; chỉ lớp sở hữu giao dịch ngoài commit.
- Dọn sáu cảnh báo WinForms về nullability/tên property che khuất `Control.Created`; sửa lifetime của HTTP request và chỉ chuyển tiếp Bearer khi redirect nâng HTTP lên HTTPS cùng host.
- Giữ `LangVersion=13.0` ở backend để các biểu thức LINQ `Contains` tương thích EF Core/Pomelo 9.
- Thư mục `review/` chỉ dùng đối chiếu và được bỏ qua bởi Git.

## Kiểm chứng

- Build cả solution: **0 lỗi, 0 cảnh báo**.
- **51/51 kiểm thử** qua: 38 kiểm thử nền và 13 trường hợp bổ sung.
- HTTP tests gọi trực tiếp `ApiClient` và DTO của WinForms: hồ sơ nhân viên/khách, cọc và thử lại, nhận/trả phòng chỉ một hóa đơn, thanh toán chỉ một lần, công việc dọn phòng không trùng, hủy qua route cũ/duyệt qua route mới, quyền thu tiền, giới hạn khách, trạng thái phòng và ngày lưu trú quanh nửa đêm.
- OpenAPI: **223 thao tác HTTP / 178 đường dẫn**, không có action trùng. Xem [danh mục](API_ENDPOINTS.md) và [JSON](openapi.json).
- Kiểm thử dùng EF InMemory và một số kiểm tra sinh SQL/model bằng provider MySQL. **Chưa xác nhận transaction rollback, cạnh tranh đồng thời, FK hoặc script trên MySQL/MariaDB thật; chưa thử ngân hàng, SMTP hay tương tác trực tiếp toàn bộ màn hình WinForms.**

Sau khi chạy test, sinh lại tài liệu bằng `python scripts/export_api_docs.py`. Khi đổi phân quyền controller, sinh lại SQL bằng `python scripts/generate_api_permissions.py` rồi rà soát trước khi áp dụng vào database.
