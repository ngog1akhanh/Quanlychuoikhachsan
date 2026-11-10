# Mã và thứ tự booking

- Danh sách lễ tân mặc định sắp theo ngày giờ nhận phòng giảm dần, cùng thời điểm thì theo ID giảm dần; bấm tiêu đề Nhận để đảo tăng/giảm và trở về trang 1. Query arrivalDescending mặc định true, sắp xếp trên API trước phân trang.
- Cột Nhận/Trả hiện ngày/tháng/năm và giờ. Mã có chiều rộng tối thiểu để dễ đọc.
- Booking mới dùng `DP-` + ID database, tối thiểu 4 chữ số: DP-0001, DP-0123, DP-10000. Không reset theo ngày/năm, không cắt số khi vượt 9999. ID có thể có khoảng trống do giao dịch rollback.
- Mã GUID tạm chỉ tồn tại trong giao dịch tạo booking; API trả về mã DP- sau khi commit. Cả web và quầy dùng dịch vụ chung.
- Database đã có booking: dùng `Database/Scripts/normalize_booking_codes.sql` sau khi chọn đúng database. Dừng API, lưu bảng đối chiếu mã cũ/mới trước khi chuyển. Chỉ đổi mã; liên kết cọc/hóa đơn/lịch lưu trú vẫn theo ID.
- Thông báo hoặc chứng từ văn bản đã phát hành có thể còn mã cũ, cần dùng bản đối chiếu. Không thay mã tham chiếu giao dịch ngân hàng.
- Nếu cài mới có demo: import file full và toàn bộ seed trước, sau đó chuyển mã. Không chạy lại seed cũ sau chuyển đổi vì seed nhận diện dữ liệu bằng mã DEMO/TK.
