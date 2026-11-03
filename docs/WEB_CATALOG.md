# Danh sách phòng và giới thiệu dịch vụ

- Trang tìm phòng không có hàng nút lọc hạng. Các thẻ hạng phòng xếp 2×2 ở giữa trên desktop, một cột trên điện thoại.
- Mỗi hạng có một thẻ đại diện; Single/Twin/Double là tùy chọn trong thẻ theo cấu hình giường thực có. Không phân tách theo số phòng, tầng, hướng hay view.
- Chỉ hiện số khả dụng khi hạng có từ 1 đến 4 phòng trong kết quả tìm kiếm theo ngày và số khách. Không trừ số đang chọn trong giỏ vì lựa chọn chưa phải giữ chỗ.
- Có thể bấm Chọn phòng nhiều lần để chọn nhiều phòng cùng nhóm. Hệ thống chọn phòng chưa được chọn có giá thấp nhất trước; giỏ và checkout tính theo giá từng phòng thực tế. API vẫn kiểm tra khả dụng khi giữ chỗ.
- Giá trên thẻ là giá từ; ảnh đại diện ổn định theo hạng phòng.
- Trang dịch vụ chỉ giới thiệu: banner dẫn tới `/services/{id}`, có hình SVG minh họa, mô tả, giá và liên hệ lễ tân. Không gọi API booking hay yêu cầu dịch vụ.

## Thông tin giường

Database hiện chưa có cột giường riêng. Website đọc thông tin rõ ràng trong mô tả phòng, sau đó mới lấy mô tả hạng phòng làm mặc định. Không suy luận giường từ sức chứa hoặc số phòng.

Trong app quản trị, cập nhật mô tả theo cấu hình thực tế, ví dụ:

| Mô tả | Nhãn hiển thị |
| --- | --- |
| 1 giường đơn | Standard Single (nếu hạng là Standard) |
| 2 giường đơn | Standard Twin |
| 1 giường đôi | Standard Double |
| 1 giường đôi và 1 giường đơn | Standard · 1 giường đôi + 1 giường đơn |
| Hai giường Queen | Family · 2 giường queen (nếu hạng là Family) |

Phòng chưa có thông tin vẫn gom theo hạng và hiện “Đang cập nhật loại giường”. Không tự sửa dữ liệu giường trong database. Không cần chạy SQL để áp dụng thay đổi giao diện này.

## Kiểm tra thủ công

1. Stop Debugging trong Visual Studio, build lại solution rồi chạy cả API và web.
2. Tìm theo ngày, xem các thẻ Standard/Deluxe. Kiểm tra mỗi hạng chỉ có một thẻ và tùy chọn giường nằm bên trong, không có số phòng/tầng/view.
3. Với nhóm có 4 phòng, phải thấy “Chỉ còn 4 phòng”; từ 5 phòng trở lên không hiện số khả dụng.
4. Bấm Chọn phòng hai lần ở nhóm có ít nhất hai phòng, mở checkout để kiểm tra số phòng và tổng giá; bấm dấu trừ để bớt một phòng.
5. Mở Dịch vụ khi chưa đăng nhập, bấm banner để xem chi tiết. Không có booking lưu trú, số lượng, ghi chú hay nút gửi yêu cầu.

Kiểm thử tự động: `RoomCatalogPresentationTests` gồm 10 ca kiểm tra nhận diện giường, gom thẻ, ngưỡng khả dụng, ẩn số phòng/hướng và trang dịch vụ chỉ đọc.
