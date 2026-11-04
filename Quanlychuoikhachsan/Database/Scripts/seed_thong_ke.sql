-- ============================================================
-- DỮ LIỆU LỊCH SỬ CHO THỐNG KÊ HÓA ĐƠN (tùy chọn)
-- Chạy SAU seed_demo.sql. Chạy lại được (INSERT IGNORE / NOT EXISTS), mọi mã đều bắt đầu bằng "TK-".
--
-- seed_demo.sql chỉ có ~200 ngày gần đây, 1 nhân viên lập hóa đơn và chưa có hóa đơn dịch vụ,
-- nên tab "HĐ theo thời gian / theo nhân viên / Sản phẩm theo năm" gần như trống. Script này thêm:
--   - ~270 booking đã trả phòng từ 01/01/2024 đến 31/03/2026 (cách nhau 3 ngày, 1–3 đêm)
--   - Hóa đơn phòng (VAT 10%) + dòng chi tiết + thanh toán; người lập luân phiên NV001 / NV002 / NV003
--   - Hóa đơn dịch vụ cho 1/2 số booking (buffet, giặt ủi, spa, đưa đón...), người lập NV005 / NV002
-- Không tạo khóa lịch phòng (booking đã ở quá khứ) nên không ảnh hưởng sơ đồ phòng / đặt phòng mới.
-- Xóa dữ liệu này: xem cuối file.
-- ============================================================
-- Chọn database demo ở client (--database=...), không đổi database tại đây.
SET NAMES utf8mb4;
SET time_zone = '+00:00';
SET SQL_SAFE_UPDATES = 0;
START TRANSACTION;

-- Dãy số 1..300 -> ngày nhận phòng; số đêm, phòng, khách, nhân viên chọn theo phần dư
-- (mã TK-xxxx tính trực tiếp bằng LPAD trong từng câu, không lưu chuỗi trong bảng tạm để tránh lỗi lệch collation)
DROP TEMPORARY TABLE IF EXISTS tk_seq;
CREATE TEMPORARY TABLE tk_seq AS
WITH RECURSIVE s(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM s WHERE n < 300)
SELECT n,
       DATE_ADD('2024-01-01', INTERVAL (n - 1) * 3 DAY) AS ngay_nhan,
       1 + (n * 7) % 3 AS so_dem
FROM s
WHERE DATE_ADD('2024-01-01', INTERVAL (n - 1) * 3 DAY) <= '2026-03-28';

DROP TEMPORARY TABLE IF EXISTS tk_phong;
CREATE TEMPORARY TABLE tk_phong AS
SELECT p.id, p.loai_phong_id, l.gia_co_ban_dem, ROW_NUMBER() OVER (ORDER BY p.id) - 1 AS stt
FROM phong p JOIN loai_phong l ON l.id = p.loai_phong_id;

DROP TEMPORARY TABLE IF EXISTS tk_khach;
CREATE TEMPORARY TABLE tk_khach AS
SELECT id, ROW_NUMBER() OVER (ORDER BY id) - 1 AS stt FROM khach_hang;

SET @so_phong = (SELECT COUNT(*) FROM tk_phong);
SET @so_khach = (SELECT COUNT(*) FROM tk_khach);

-- 1. Booking đã trả phòng ------------------------------------------------------
INSERT IGNORE INTO dat_phong (ma_dat_phong, khach_hang_id, ngay_dat, du_kien_nhan, du_kien_tra, so_nguoi_lon, so_tre_em, kenh_dat, trang_thai, tong_tien_phong_goc, tong_tien_du_kien)
SELECT CONCAT('TK-', LPAD(s.n, 4, '0')), k.id,
       CONCAT(DATE_SUB(s.ngay_nhan, INTERVAL 5 DAY), ' 03:00:00'),
       CONCAT(s.ngay_nhan, ' 07:00:00'),                                 -- 14:00 giờ VN
       CONCAT(DATE_ADD(s.ngay_nhan, INTERVAL s.so_dem DAY), ' 05:00:00'), -- 12:00 giờ VN
       2, 0, ELT(1 + s.n % 3, 'WEB', 'QUAY', 'DIEN_THOAI'), 'DA_TRA',
       p.gia_co_ban_dem * s.so_dem, p.gia_co_ban_dem * s.so_dem
FROM tk_seq s
JOIN tk_phong p ON p.stt = (s.n * 5) % @so_phong
JOIN tk_khach k ON k.stt = s.n % @so_khach;

INSERT IGNORE INTO chi_tiet_dat_phong (dat_phong_id, phong_id, so_nguoi_lon, so_tre_em, tien_phong_du_kien, nhan_phong_luc, tra_phong_luc, trang_thai)
SELECT d.id, p.id, 2, 0, d.tong_tien_du_kien, d.du_kien_nhan, d.du_kien_tra, 'DA_TRA'
FROM tk_seq s
JOIN dat_phong d ON d.ma_dat_phong = CONCAT('TK-', LPAD(s.n, 4, '0'))
JOIN tk_phong p ON p.stt = (s.n * 5) % @so_phong;

-- 2. Hóa đơn phòng: người lập luân phiên NV002 (lễ tân) / NV003 (quản lý) / NV001 (admin) -----------
INSERT IGNORE INTO hoa_don_phong (ma_hoa_don, dat_phong_id, nhan_vien_lap_id, ngay_lap, tien_phong, tien_phu_thu, tien_boi_thuong, tien_thue, tong_tien, trang_thai)
SELECT CONCAT('HD-TK-', LPAD(s.n, 4, '0')), d.id,
       (SELECT id FROM nhan_vien WHERE ma_nhan_vien = ELT(1 + s.n % 3, 'NV002', 'NV003', 'NV001')),
       d.du_kien_tra, d.tong_tien_du_kien, 0, 0, ROUND(d.tong_tien_du_kien * 0.1, 0), ROUND(d.tong_tien_du_kien * 1.1, 0), 'DA_THANH_TOAN'
FROM tk_seq s JOIN dat_phong d ON d.ma_dat_phong = CONCAT('TK-', LPAD(s.n, 4, '0'));

INSERT INTO chi_tiet_hoa_don_phong (hoa_don_phong_id, chi_tiet_dat_phong_id, loai_dong, noi_dung, ngay_ap_dung, so_luong, don_gia, thanh_tien)
SELECT h.id, ct.id, 'TIEN_PHONG', CONCAT('Tiền phòng ', s.so_dem, ' đêm'), s.ngay_nhan, s.so_dem, ct.tien_phong_du_kien / s.so_dem, ct.tien_phong_du_kien
FROM tk_seq s
JOIN hoa_don_phong h ON h.ma_hoa_don = CONCAT('HD-TK-', LPAD(s.n, 4, '0'))
JOIN chi_tiet_dat_phong ct ON ct.dat_phong_id = h.dat_phong_id
WHERE NOT EXISTS (SELECT 1 FROM chi_tiet_hoa_don_phong x WHERE x.hoa_don_phong_id = h.id AND x.loai_dong = 'TIEN_PHONG');

INSERT INTO chi_tiet_hoa_don_phong (hoa_don_phong_id, loai_dong, noi_dung, so_luong, don_gia, thanh_tien)
SELECT h.id, 'THUE', 'VAT 10%', 1, h.tien_thue, h.tien_thue
FROM hoa_don_phong h
WHERE h.ma_hoa_don LIKE 'HD-TK-%' AND h.tien_thue > 0
  AND NOT EXISTS (SELECT 1 FROM chi_tiet_hoa_don_phong x WHERE x.hoa_don_phong_id = h.id AND x.loai_dong = 'THUE');

INSERT IGNORE INTO thanh_toan_hoa_don_phong (ma_thanh_toan, hoa_don_phong_id, nhan_vien_thu_id, phuong_thuc_id, so_tien, thoi_gian_thanh_toan, cong_thanh_toan, ma_giao_dich, trang_thai)
SELECT CONCAT('TT-', h.ma_hoa_don), h.id, h.nhan_vien_lap_id,
       (SELECT id FROM phuong_thuc_thanh_toan WHERE ma_phuong_thuc = ELT(1 + h.id % 3, 'TIEN_MAT', 'THE', 'CHUYEN_KHOAN')),
       h.tong_tien, h.ngay_lap, 'MANUAL', CONCAT('TT-', h.ma_hoa_don), 'THANH_CONG'
FROM hoa_don_phong h WHERE h.ma_hoa_don LIKE 'HD-TK-%';

-- 3. Dịch vụ cho booking chẵn: 1 lần sử dụng + 1 hóa đơn dịch vụ đã thanh toán --------------------
INSERT IGNORE INTO su_dung_dich_vu (ma_su_dung, dat_phong_id, phong_id, dich_vu_id, thoi_gian_yeu_cau, thoi_gian_phuc_vu, so_luong, don_gia, thanh_tien, trang_thai)
SELECT CONCAT('SD-TK-', LPAD(s.n, 4, '0')), d.id, ct.phong_id, dv.id,
       DATE_ADD(d.du_kien_nhan, INTERVAL 18 HOUR), DATE_ADD(d.du_kien_nhan, INTERVAL 19 HOUR),
       1 + s.n % 3, dv.don_gia, dv.don_gia * (1 + s.n % 3), 'HOAN_THANH'
FROM tk_seq s
JOIN dat_phong d ON d.ma_dat_phong = CONCAT('TK-', LPAD(s.n, 4, '0'))
JOIN chi_tiet_dat_phong ct ON ct.dat_phong_id = d.id
JOIN dich_vu dv ON dv.ma_dich_vu = ELT(1 + (s.n DIV 2) % 5, 'AN_SANG', 'GIAT_UI', 'SPA_60', 'DUA_DON', 'ROOM_SERVICE')
WHERE s.n % 2 = 0;

INSERT IGNORE INTO hoa_don_dich_vu (ma_hoa_don, dat_phong_id, nhan_vien_lap_id, ngay_lap, tong_tien, trang_thai)
SELECT CONCAT('HDV-TK-', SUBSTRING(u.ma_su_dung, 7)), u.dat_phong_id,
       (SELECT id FROM nhan_vien WHERE ma_nhan_vien = IF(u.id % 2 = 0, 'NV005', 'NV002')),
       u.thoi_gian_phuc_vu, u.thanh_tien, 'DA_THANH_TOAN'
FROM su_dung_dich_vu u WHERE u.ma_su_dung LIKE 'SD-TK-%';

INSERT IGNORE INTO chi_tiet_hoa_don_dich_vu (hoa_don_dich_vu_id, su_dung_dich_vu_id, thanh_tien)
SELECT h.id, u.id, u.thanh_tien
FROM su_dung_dich_vu u
JOIN hoa_don_dich_vu h ON h.ma_hoa_don = CONCAT('HDV-TK-', SUBSTRING(u.ma_su_dung, 7))
WHERE u.ma_su_dung LIKE 'SD-TK-%';

INSERT IGNORE INTO thanh_toan_dich_vu (ma_thanh_toan, hoa_don_dich_vu_id, nhan_vien_thu_id, phuong_thuc_id, so_tien, thoi_gian_thanh_toan, ma_giao_dich, trang_thai)
SELECT CONCAT('TT-', h.ma_hoa_don), h.id, h.nhan_vien_lap_id,
       (SELECT id FROM phuong_thuc_thanh_toan WHERE ma_phuong_thuc = IF(h.id % 2 = 0, 'TIEN_MAT', 'THE')),
       h.tong_tien, h.ngay_lap, CONCAT('TT-', h.ma_hoa_don), 'THANH_CONG'
FROM hoa_don_dich_vu h WHERE h.ma_hoa_don LIKE 'HDV-TK-%';

DROP TEMPORARY TABLE IF EXISTS tk_seq;
DROP TEMPORARY TABLE IF EXISTS tk_phong;
DROP TEMPORARY TABLE IF EXISTS tk_khach;

-- Kiểm tra nhanh
COMMIT;
SELECT YEAR(ngay_lap) AS nam, COUNT(*) AS so_hd_phong, SUM(tong_tien) AS tong_hd_phong
FROM hoa_don_phong WHERE trang_thai NOT IN ('NHAP', 'DA_HUY') GROUP BY YEAR(ngay_lap) ORDER BY nam;

-- ------------------------------------------------------------
-- XÓA DỮ LIỆU THỐNG KÊ (nếu cần): bỏ comment và chạy theo thứ tự
-- DELETE FROM thanh_toan_dich_vu WHERE ma_thanh_toan LIKE 'TT-HDV-TK-%';
-- DELETE FROM hoa_don_dich_vu WHERE ma_hoa_don LIKE 'HDV-TK-%';          -- cascade chi_tiet_hoa_don_dich_vu
-- DELETE FROM su_dung_dich_vu WHERE ma_su_dung LIKE 'SD-TK-%';
-- DELETE FROM thanh_toan_hoa_don_phong WHERE ma_thanh_toan LIKE 'TT-HD-TK-%';
-- DELETE FROM hoa_don_phong WHERE ma_hoa_don LIKE 'HD-TK-%';             -- cascade chi_tiet_hoa_don_phong
-- DELETE FROM dat_phong WHERE ma_dat_phong LIKE 'TK-%';                  -- cascade chi_tiet_dat_phong
