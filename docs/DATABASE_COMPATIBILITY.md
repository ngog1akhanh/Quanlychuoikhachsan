# Database tương thích với API và WinForms LMT

Đã tạo bản cài mới: [`hotel_management_compatible.sql`](../Quanlychuoikhachsan/Database/Install/hotel_management_compatible.sql).
Database mặc định tên `hotel_management_compatible`, gồm **67 bảng, 5 view, 6 vai trò và 42 quyền**. Không có tài khoản/mật khẩu ứng dụng mặc định hoặc giao dịch mẫu.

## Nguồn và kết quả đối chiếu

Nguồn yêu cầu là [cuộc trò chuyện được chia sẻ](https://chatgpt.com/share/6ae8026e-5824-83ec-8651-1ba32901bf5a).
File SQL nguồn đã được đối chiếu với `Database/Scripts/hotel_management_final_food_inventory.sql` trong repo: nội dung trùng nhau sau khi chuẩn hóa LF/CRLF. Đây là bản cuối 65 bảng/5 view, không phải bản cũ có khuyến mãi trong cùng cuộc trò chuyện.

| Thành phần | Đánh giá và xử lý |
|---|---|
| Booking, khách hàng, phòng, lễ tân, hóa đơn, dịch vụ, housekeeping | Giữ tên bảng/cột, khóa chính và quan hệ đang được API sử dụng; WinForms tiếp tục gọi lớp API tương thích LMT |
| Nhập/xuất hàng, thực phẩm, vật tư, tài sản | Dùng chung danh mục hàng; thực phẩm có lô/HSD, tài sản có cá thể; tồn kho lấy từ sổ giao dịch, không thêm cột tồn có thể sửa riêng |
| Thanh toán VietQR mới | Ghép `vietqr_payment_order` và `vietqr_deposit_order`; tổng cộng 67 bảng |
| Phân quyền | Ghép bộ seed 42 quyền/6 vai trò; kiểm tra ADMIN có toàn bộ quyền khai báo tại controller |
| Thanh toán tại quầy | Có danh mục `TIEN_MAT`, `CHUYEN_KHOAN`, `VIETQR` |
| `chinh_sach_gia_thu.thu`, 3 cột điểm của `danh_gia` | Đổi sang `TINYINT UNSIGNED`, khớp `byte`/`byte?` của model; điểm tùy chọn chỉ nhận NULL hoặc 1–5 |
| Số lượng và tiền | Giữ số lượng kho `DECIMAL(15,3)`, dòng hóa đơn phòng `DECIMAL(12,2)`, tiền `DECIMAL(15,2)`; không áp dụng đề xuất cũ nhầm dòng hóa đơn là INT |
| Thời gian | Bản cài mới dùng `DATETIME(6)`/`TIME(6)`; interceptor đặt mỗi kết nối EF về UTC, khớp `DateTime.UtcNow` của API; ngày lưu trú/HSD vẫn là DATE |
| Kho theo lô | Bổ sung index FEFO và FK ghép `(lo_hang_id,kho_id,danh_muc_hang_hoa_id)` để chặn ghi sổ sai kho/mặt hàng |
| Chi tiết hóa đơn và áp dụng cọc | CHECK số lượng > 0, đơn giá/thành tiền >= 0, cọc áp dụng > 0 |
| View báo cáo | Công nợ loại `NHAP`/`DA_HUY`; HSD dùng ngày Việt Nam từ UTC+7, không phụ thuộc timezone session; giữ cảnh báo lô đã hết hạn còn tồn |

Các endpoint và DTO không bị đổi. Validation HSD, FEFO, chống âm kho, trạng thái chứng từ và tính tiền vẫn do service xử lý trong transaction; bộ SQL không thay thế các quy tắc nghiệp vụ đó.

## Cài mới trên server của bạn

Sau khi clone, chuyển sang nhánh tích hợp bằng `git switch codex/integrate-lmt-api`. File SQL hoàn chỉnh đã có trong Git, không cần tải lại tài liệu nguồn, giải nén RAR hay lấy thư mục `review` từ máy khác. Python chỉ cần khi muốn sinh lại SQL hoặc đổi tên database; import file có sẵn không cần Python.

Yêu cầu MySQL **8.0.16 trở lên** hoặc MariaDB **10.4 trở lên** để CHECK được thực thi. Đã chạy thực tế với **MariaDB 11.4.13**, chưa chạy bộ cài trên MySQL riêng.

Chạy tại thư mục gốc repo bằng PowerShell 7, thay đường dẫn client/host/user theo máy:

```powershell
Get-Content './Quanlychuoikhachsan/Database/Install/hotel_management_compatible.sql' -Raw -Encoding utf8 |
    mysql --host=127.0.0.1 --port=3306 --user=root --password --default-character-set=utf8mb4
if ($LASTEXITCODE -ne 0) { throw 'Import thất bại; xem lỗi trước khi chạy lại.' }
```

Có thể thay `mysql` bằng `mariadb`. Client hỏi mật khẩu; không ghi mật khẩu vào file SQL. Không dùng `--force` hoặc tùy chọn tiếp tục khi lỗi. `CREATE DATABASE` cố ý không có `IF NOT EXISTS`: client chạy batch phải dừng nếu tên database đã tồn tại. DDL không rollback toàn bộ được; nếu lỗi giữa chừng, giữ nguyên database để kiểm tra và tạo bản mới với tên khác.

Để đổi tên, tạo lại file bằng Python 3 thay vì thay tên thủ công ở nhiều chỗ:

```powershell
python scripts/build_database.py --database hotel_management_demo --output review/hotel_management_demo.sql
```

Cấu hình User Secrets hoặc biến môi trường cho backend:

```text
ConnectionStrings:HotelManagement = Server=127.0.0.1;Port=3306;Database=hotel_management_compatible;User ID=<app_user>;Password=<password>
Database:ServerVersion = <phiên bản thực tế>-mariadb
```

Với MySQL, ví dụ `8.0.36-mysql`. Tài khoản DB của API cần SELECT/INSERT/UPDATE/DELETE trên database này; tài khoản cài đặt cần quyền tạo schema, view, FK, index và seed.

Ví dụ cấu hình backend trên máy mới bằng User Secrets (thay các giá trị trong dấu `<...>` bằng thông tin của bạn):

```powershell
dotnet user-secrets set 'ConnectionStrings:HotelManagement' 'Server=127.0.0.1;Port=3306;Database=hotel_management_compatible;User ID=<app_user>;Password=<password>' --project Quanlychuoikhachsan
dotnet user-secrets set 'Database:ServerVersion' '11.4.13-mariadb' --project Quanlychuoikhachsan
```

Sau đó cấu hình JWT và các tích hợp cần dùng theo [README](../README.md#cấu-hình-và-chạy), restore/build solution và chạy backend. WinForms chạy trên Windows; đặt `HotelAdmin.WinForms/appsettings.json` → `ApiBaseUrl` đúng URL backend. Database không tự được tạo khi chạy `dotnet run`: phải import SQL trước.

Tài khoản đăng nhập nghiệp vụ đầu tiên phải có password hash ASP.NET Core Identity, `loai_tai_khoan='NHAN_VIEN'`, trạng thái `HOAT_DONG`, hồ sơ `nhan_vien` ở trạng thái `DANG_LAM`, và liên kết vai trò `ADMIN`. Tài khoản DB kết nối server không phải tài khoản đăng nhập API. Bộ cài không đặt mật khẩu quản trị chung cho mọi máy.

Repo chỉ chia sẻ schema, seed danh mục/quyền và công cụ kiểm chứng. Dữ liệu vật lý của server, thông tin kết nối riêng, thư mục `review`, tài khoản và giao dịch của từng máy không nằm trong bộ cài.

## Kiểm chứng và tái tạo

Ngày kiểm chứng: 2026-11-02. **55/55 test đạt**, không bỏ qua test nào trong lần chạy có MariaDB:

- 51 test API/nghiệp vụ hiện có.
- 4 test tích hợp mới: cài toàn bộ bundle; đối chiếu model với tên cột, nullability, kiểu dữ liệu, precision/scale, độ dài khai báo và PK; chạy các query EF và 5 view; kiểm tra quyền ADMIN.
- Luồng service nhập 1.125 đơn vị, xác nhận, từ chối xác nhận lặp, xuất 0.125 và đối chiếu tồn 1.000; kiểm tra FK sai kho/lô, CHECK số lượng âm và rollback.
- Chặn hai booking cùng phòng/ngày, trùng đơn cọc cùng booking và trùng transaction VietQR; đối chiếu công nợ loại hóa đơn nháp/hủy.
- Kết nối EF đồng bộ/bất đồng bộ đều đặt UTC sau khi mở lại.

Chạy lại trên server kiểm thử riêng bằng cách đặt `HOTEL_TEST_MYSQL` thành connection string của tài khoản có quyền tạo/xóa database rồi chạy:

```powershell
dotnet test Quanlychuoikhachsan.Api.Tests/Quanlychuoikhachsan.Api.Tests.csproj --no-restore -v minimal
python scripts/build_database.py --check
```

Mỗi test tạo database `hotel_compat_test_<GUID>` rồi chỉ xóa database do test đó tạo. Nếu không đặt biến môi trường, 4 test tích hợp sẽ báo **Skipped**, không được coi là đã kiểm chứng server thật.

File bundle được sinh từ 4 script trong `Database/Scripts` cộng `Database/Install/compatible_constraints.sql`. Chạy `python scripts/build_database.py` sau khi sửa các nguồn. Các script gốc được giữ để đối chiếu; không chạy lại 4 script đó sau khi đã import bundle.

Chưa kiểm thử tải đồng thời, deadlock khi nhiều người thao tác, hay kết nối ngân hàng/SMTP thật. Chưa di chuyển dữ liệu database cũ. Nếu nâng cấp database đang có dữ liệu, cần migration riêng, đối chiếu giá trị sai FK/CHECK và quy ước thời gian cũ trước; không dùng bản cài mới để ghi đè.
