# App quản lý khách sạn (WinForms)

Ứng dụng Windows (.NET 9) cho nhân viên khách sạn, gọi REST API của backend `Quanlychuoikhachsan`.

## Chạy

1. Chạy backend trước (profile **http**, cổng 5005).
2. Kiểm tra `appsettings.json` → `ApiBaseUrl` trỏ đúng địa chỉ backend.
3. Chạy app: `dotnet run --project HotelAdmin.WinForms.csproj`, hoặc nhấp đúp `chay-he-thong.bat` ở thư mục gốc để mở cả API, Web và App.
4. Đăng nhập bằng tài khoản trong `tai khoan test demo.txt` (mật khẩu `12345`).

## Các module

| Nhóm | Module |
|---|---|
| Vận hành | Tổng quan · Đặt phòng · Nhận/trả phòng · Phòng · Buồng phòng · Dịch vụ |
| Tài chính | Hóa đơn & thanh toán · Giá phòng & chính sách |
| Khách hàng | Khách hàng & đánh giá |
| Kho | Kho & tài sản |
| Hệ thống | Nhân viên & phân quyền · Báo cáo |

Menu và nút thao tác hiện theo vai trò, quyền của tài khoản đăng nhập.

## Cấu trúc thư mục

| Thư mục | Chứa gì |
|---|---|
| `Pages/` | Mỗi module một trang; trang nhiều tab kế thừa `ModulePage` |
| `Controls/` | Control tự vẽ (nút, thẻ, biểu đồ, lưới) và `DataView` – danh sách dùng chung |
| `Forms/Dialogs/` | Hộp thoại; `FormDialog` dựng form nhập theo khai báo trường |
| `Services/` | `ApiClient` gọi API, `Lookups` tải danh mục cho ComboBox |
| `Models/` | Model dữ liệu; `J` đọc JSON an toàn |
| `Theme/` | Màu, font, icon, nhãn tiếng Việt cho mã trạng thái |

## Ghi chú

- Trả phòng gồm 3 bước: **chốt hóa đơn → xác nhận đã thu tiền → trả phòng**.
- Chi tiết thay đổi đợt 01/11: [docs/update_11_1.md](../docs/update_11_1.md).
