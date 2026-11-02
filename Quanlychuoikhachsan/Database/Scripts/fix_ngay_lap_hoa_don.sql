-- ============================================================
-- SỬA NGÀY LẬP HÓA ĐƠN PHÒNG (chạy 1 lần, chạy lại cũng không sao)
-- Lỗi cũ: hóa đơn tạm tạo lúc nhận phòng; khi chốt hóa đơn, ngay_lap vẫn giữ giờ nhận phòng,
-- nên báo cáo Doanh thu (nhóm theo ngay_lap) đếm tiền vào ngày nhận phòng thay vì ngày chốt.
-- Code đã sửa (BookingOperations.PrepareInvoiceAsync). Script này sửa các hóa đơn đã chốt trước đó:
-- ngay_lap = thời điểm thanh toán thành công đầu tiên; nếu chưa thanh toán thì = giờ trả phòng thực tế.
-- Chỉ đổi khi giá trị mới MUỘN hơn ngay_lap hiện tại (dữ liệu demo không bị ảnh hưởng).
-- ============================================================
-- Chọn database cần đối chiếu ở client; không chạy tự động cùng bộ cài mới.
-- Thời điểm thu tiền/trả phòng chỉ là giá trị suy ra, không phải lịch sử phát hành chính xác.
-- Chỉ áp dụng sau khi đối chiếu và sao lưu dữ liệu cũ.
SET time_zone = '+00:00';
SET SQL_SAFE_UPDATES = 0;

UPDATE hoa_don_phong h
JOIN (
    SELECT h2.id,
           COALESCE(
               (SELECT MIN(t.thoi_gian_thanh_toan) FROM thanh_toan_hoa_don_phong t
                 WHERE t.hoa_don_phong_id = h2.id AND t.trang_thai = 'THANH_CONG'),
               (SELECT MAX(c.tra_phong_luc) FROM chi_tiet_dat_phong c WHERE c.dat_phong_id = h2.dat_phong_id)
           ) AS ngay_moi
    FROM hoa_don_phong h2
    WHERE h2.trang_thai NOT IN ('NHAP', 'DA_HUY')
) x ON x.id = h.id
SET h.ngay_lap = x.ngay_moi
WHERE x.ngay_moi IS NOT NULL AND x.ngay_moi > h.ngay_lap;

SELECT ROW_COUNT() AS so_hoa_don_da_sua;
