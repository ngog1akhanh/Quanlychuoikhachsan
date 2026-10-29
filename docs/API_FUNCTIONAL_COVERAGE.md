# Đối chiếu chức năng hệ thống

Nguồn: `Chuc_nang_He_thong_Quan_ly_Khach_san (1).docx` và phần góp ý database được cung cấp. Phạm vi triển khai là backend API của toàn bộ nhóm chức năng, bổ sung chi tiết nhập hàng còn thiếu trong bản mô tả.

## Phân nhóm để tránh trùng nghiệp vụ

| Nhóm trong tài liệu | API và cách tổ chức |
|---|---|
| A1: Tài khoản khách hàng | Đăng ký/đăng nhập hiện có; thêm đăng xuất, quên/đặt lại mật khẩu qua email, hồ sơ cá nhân, đổi mật khẩu, lịch sử booking. Kiểm tra tài khoản đang hoạt động trên từng request. |
| A2: Tìm phòng | Công khai thông tin khách sạn, loại phòng, tiện nghi, hình ảnh, phòng trống, lọc loại/view/tổng giá, giá từng đêm. Dùng chung tính giá và lịch khóa của booking. |
| A3 và B7: Đặt phòng | Booking website và tại quầy dùng chung luồng tạo, HOLD, giá chốt và kiểm tra sức chứa. Thêm tìm kiếm, chi tiết, lịch sử, yêu cầu đổi lịch/đổi phòng, thao tác đổi lịch/gia hạn và đổi phòng của nhân viên. |
| A4 và B12: Cọc | Cọc online có đơn VietQR riêng vì lúc HOLD chưa có hóa đơn phòng. Số tiền do server tính; inquiry đối chiếu order, số tiền và mã giao dịch. Cọc tại quầy và cọc bổ sung cần quyền thu tiền. |
| A5 và B7: Hủy booking | Một quy trình yêu cầu → duyệt/từ chối; khách không tự hủy trực tiếp. Duyệt tạo phiếu hoàn chờ xử lý, giải phóng lịch; từ chối không phục hồi HOLD đã hết hạn. |
| A6 và B13: Đánh giá | Chỉ chủ booking đã trả phòng; một đánh giá/booking. Quản lý phản hồi, ẩn/hiện; khách xem đánh giá và phản hồi của mình. |
| B8: Nhận/trả phòng | Danh sách đến/đi, khai báo người ở, check-in phòng sẵn sàng, phụ thu sớm/trễ, lập/phát hành hóa đơn trước khi thu tiền, checkout khi hóa đơn đã thanh toán, tự tạo việc dọn phòng. |
| B9: Phòng | Thêm/sửa phòng và loại phòng, ngừng sử dụng, tiện nghi, URL hình ảnh, trạng thái vận hành, dữ liệu sơ đồ theo tầng và lịch phòng. Không đổi phòng đang ở sang trạng thái trống bằng API trạng thái. |
| B10: Giá và khuyến mãi | Dùng chính sách giá theo loại phòng, khoảng ngày, thứ và độ ưu tiên. Mức giảm giá nằm trong chính sách; không tạo thêm module coupon trùng lặp. Giá chốt của các đêm đã đặt được giữ nguyên. |
| B11: Dịch vụ | Danh mục, yêu cầu gắn booking/phòng, tiếp nhận/thực hiện/hoàn thành. Hoàn thành tạo hóa đơn dịch vụ riêng; thu nhiều lần, hủy và hoàn tiền có truy vết. Không cộng tiền dịch vụ vào hóa đơn phòng. |
| Nhập hàng và B15: Kho | Nhà cung cấp, kho, nhóm hàng, đơn vị, mặt hàng; phiếu nhập nháp/sửa/hủy/xác nhận; thông tin hóa đơn NCC; lô/hạn dùng/serial; phiếu xuất nháp/sửa/hủy/xác nhận FEFO; sổ kho, tồn, điều chỉnh, cảnh báo. |
| B15: Tài sản | Theo từng cá thể, chuyển kho/phòng/vị trí khác; hỏng/mất/bảo trì/thanh lý. Mỗi chuyển động kho phát sinh bút toán đúng dấu. Bồi thường của khách ghi qua sự cố phòng để lập hóa đơn. |
| B12: Tài chính | Hóa đơn phòng và dịch vụ độc lập, thanh toán nhiều phương thức/lần; phiếu hoàn tham chiếu đúng nguồn; in hóa đơn phòng HTML và gửi vào tài khoản khách; ca thu ngân và đối soát theo phương thức. |
| B13: Khách hàng | Khách có tài khoản và khách vãng lai; thêm/sửa/tìm hồ sơ, lịch sử booking. Tra cứu lịch sử lưu trú, dịch vụ và thanh toán theo ID booking qua các API dùng chung. |
| B14: Buồng phòng | Tạo việc, phân công, cập nhật tiến độ, sự cố và chi phí bồi thường. Dọn hằng ngày không biến phòng còn khách ở thành phòng sẵn sàng. |
| B16: Nhân viên và quyền | Chức vụ, nhân viên, tài khoản, đặt lại mật khẩu, khóa/mở khóa, vai trò, quyền, gán nhiều vai trò, nhật ký. Seed 6 vai trò và tập quyền dùng trong controller. |
| B17: Thông báo | Hộp thư cá nhân, đánh dấu đọc, cài đặt nhận tin, gửi nội bộ. Sự kiện booking giữ snapshot ngày đến/đi; nhắc trong 24 giờ; tổng hợp việc dọn, sự cố nghiêm trọng và thiếu tồn theo quyền người nhận. |
| B18: Báo cáo | Dashboard, doanh thu phòng/dịch vụ, phân tích ngày/loại phòng/phương thức, occupancy/ADR/RevPAR, booking/hủy/cọc/hoàn, dịch vụ, khách mới/quay lại/đánh giá, nhập–xuất–tồn. |

## Nguyên tắc dữ liệu

- Tồn kho luôn bằng tổng `giao_dich_kho.so_luong_thay_doi`; không thêm cột tồn có thể bị cập nhật lệch.
- Xác nhận nhập tạo lô, tài sản cá thể và bút toán tăng tồn trong một transaction. Chứng từ đã xác nhận không được sửa hoặc xác nhận lần hai.
- Xuất hàng theo FEFO, bỏ lô hết hạn/ngừng dùng; cộng nhu cầu của các dòng trùng mặt hàng trước khi kiểm tra tồn.
- Các luồng ghi kho khóa dòng kho trước khi tính tồn. Điều chỉnh phải đúng lô–kho–mặt hàng và không làm âm tồn. Tài sản cá thể dùng API chuyển/đổi tình trạng riêng.
- Ràng buộc unique phòng–đêm và unique giao dịch cọc có sẵn tiếp tục là lớp bảo vệ tại database.
- Hóa đơn phòng được lập nháp khi check-in, phát hành trước checkout. Checkout dùng lại hóa đơn đó, tránh lập hai hóa đơn cho cùng lần lưu trú.
- Hóa đơn phòng đã phát hành không sửa số tiền. Hủy và lập bản thay thế giữ hóa đơn cũ, liên kết `hoa_don_thay_the_id`; khoản thu cũ vẫn giữ để hoàn theo nguồn. Dịch vụ có một hóa đơn cho một lượt sử dụng theo ràng buộc schema; hủy dịch vụ dùng quy trình hoàn tiền, không xóa giao dịch.
- Hoàn cọc được lưu ở phiếu hoàn riêng, giữ nguyên số tiền nhận gốc. Chi tiết booking trả danh sách phiếu hoàn, số cọc yêu cầu và cọc còn thiếu.
- Các API tự phục vụ kiểm tra chủ sở hữu booking. Các thao tác nhân viên kiểm tra quyền backend và lấy nhân viên thực hiện từ JWT.

## Diễn giải báo cáo

- Doanh thu dựa trên hóa đơn đã phát hành, loại nháp/hủy; số thu theo phương thức dựa trên giao dịch thanh toán. Hai chỉ tiêu này khác nhau. `revenue-breakdown.daily.rooms` bao gồm tổng hóa đơn phòng, còn `revenue.room_revenue` gồm tiền phòng và phụ thu, tách bồi thường/VAT ở trường riêng.
- Cọc và hoàn nằm trong báo cáo booking. Đối soát ca tổng hợp tiền đã thu của phòng, dịch vụ và cọc; `actual_total` là tiền thu của ca, không bao gồm tiền đầu ca. Phiếu hoàn hiện không có liên kết ca trong schema, nên phải đối chiếu riêng.
- Công suất tính từ đêm lưu trú của chi tiết đang ở/đã trả. Mẫu số dùng số phòng hiện không ngừng sử dụng × số ngày; schema chưa có lịch sử đóng/bảo trì để tái dựng chính xác năng lực phòng trong quá khứ.
- Nhập/xuất trong báo cáo kho là tổng biến động dương/âm, bao gồm điều chỉnh, hỏng/mất và chuyển tài sản; dùng sổ kho để phân tích theo loại giao dịch.

## Phạm vi tích hợp thực tế

API quản lý URL hình ảnh, không tải file ảnh lên một dịch vụ lưu trữ. Sơ đồ, biểu đồ và màn hình website/app cần frontend sử dụng dữ liệu API.

VietQR sử dụng gateway sandbox có sẵn; VNPay/MoMo không được thêm vì yêu cầu cho phép lựa chọn QR. Inquiry do client gọi để đối chiếu; không xác nhận tiền từ dữ liệu callback tự khai. Cọc đến sau khi mất HOLD được ghi nhận và tạo yêu cầu hoàn, không lấy lại phòng đã hết giữ chỗ. Khi thu ngân xác nhận hoàn toàn bộ qua phương thức thực tế, đơn QR chuyển `REFUNDED`.

SMTP phải được cấu hình mới gửi được mã đặt lại mật khẩu. Thông báo vận hành là thông báo trong app; worker chạy khi ứng dụng hoạt động. Chưa triển khai dịch vụ push/SMS, hóa đơn điện tử thuế hoặc lệnh chuyển tiền hoàn qua ngân hàng.

Schema hiện tại không gắn phòng/kho với chi nhánh khách sạn. Các API được bổ sung theo mô hình hiện có; thông tin khách sạn không đồng nghĩa với đã có cách ly dữ liệu nhiều chi nhánh.

Kiểm thử tự động không thay thế kiểm thử MySQL/MariaDB thật. Cần chạy script và kiểm chứng transaction/khóa/FK cùng tài khoản gateway/email của môi trường triển khai trước khi dùng dữ liệu thật.
