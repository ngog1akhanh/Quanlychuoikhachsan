-- ============================================================
-- DU LIEU MAU (DEMO) - chay SAU 4 script schema, CHI CHAY MOT LAN
-- Tai khoan nhan vien (app quan ly):
--   admin.com            / 12345   (ADMIN)
--   letan@hotel.com      / 12345   (RECEPTIONIST)
-- Tai khoan khach hang (web), mat khau deu la 12345:
--   an.nguyen@example.com, binh.tran@example.com,
--   cuong.le@example.com,  dung.pham@example.com
-- Ngay dat phong tinh theo ngay hien tai (CURDATE), nen luon hop le.
-- ============================================================
USE hotel_management;
SET NAMES utf8mb4;
SET time_zone = '+07:00';

-- 1. TAI KHOAN ---------------------------------------------------
INSERT IGNORE INTO tai_khoan (email, so_dien_thoai, mat_khau_hash, loai_tai_khoan, trang_thai, email_da_xac_thuc) VALUES
  ('admin.com', '0900000000', 'AQAAAAIAAYagAAAAEHOZzLFdlGCx5Kuk7v8SXrS6TNET2LqvPRyx0Wwy949b2Nf7bklZUHrc0X/+n2l1vg==', 'NHAN_VIEN', 'HOAT_DONG', 1),
  ('letan@hotel.com', '0900000001', 'AQAAAAIAAYagAAAAEMCC9ne+859hXeaA6C63K+4cqoVZvb7eui5dHBgYU/Lavs0T16iVsjo9UZekWNHgbg==', 'NHAN_VIEN', 'HOAT_DONG', 1),
  ('an.nguyen@example.com', '0901234567', 'AQAAAAIAAYagAAAAEKhdGMkgK7ul7JtJpiBCqkh/UeY7zMMLijyl3k8BRuFwliwM/5AtpBgU0LtYGq7uXg==', 'KHACH_HANG', 'HOAT_DONG', 1),
  ('binh.tran@example.com', '0912345678', 'AQAAAAIAAYagAAAAEB7+GAVRDpsBDp+b8yFYXuxPiq1CRJ4kN3e1BXmW6Fw4vQP3D34TZk1x5CvBT0HgLA==', 'KHACH_HANG', 'HOAT_DONG', 1),
  ('cuong.le@example.com', '0923456789', 'AQAAAAIAAYagAAAAEO31PwFB3FyuqykrI0HHj+R9+pQxioYs59fdv1wKYJ+zg8zj1+2D5f6+qVU2FTkkrQ==', 'KHACH_HANG', 'HOAT_DONG', 1),
  ('dung.pham@example.com', '0934567890', 'AQAAAAIAAYagAAAAEJCOujMPLauL7D8HnaQYaOFRGq15pFPgDVmbqkf3MF0atamUnlmRULtgcV1gbHjyNQ==', 'KHACH_HANG', 'HOAT_DONG', 1);

-- 2. NHAN VIEN + VAI TRO -----------------------------------------
INSERT INTO chuc_vu (ma_chuc_vu, ten_chuc_vu, trang_thai) VALUES
  ('QUAN_TRI', 'Quản trị viên', 'HOAT_DONG'),
  ('LE_TAN', 'Lễ tân', 'HOAT_DONG')
ON DUPLICATE KEY UPDATE ten_chuc_vu = VALUES(ten_chuc_vu);

INSERT IGNORE INTO nhan_vien (ma_nhan_vien, tai_khoan_id, chuc_vu_id, ho_ten, so_dien_thoai, email, ngay_vao_lam, trang_thai)
SELECT 'NV001', t.id, c.id, 'Quản trị viên', '0900000000', 'admin.com', CURDATE(), 'DANG_LAM'
FROM tai_khoan t JOIN chuc_vu c ON c.ma_chuc_vu = 'QUAN_TRI' WHERE t.email = 'admin.com';

INSERT IGNORE INTO nhan_vien (ma_nhan_vien, tai_khoan_id, chuc_vu_id, ho_ten, so_dien_thoai, email, ngay_vao_lam, trang_thai)
SELECT 'NV002', t.id, c.id, 'Nguyễn Thu Lễ Tân', '0900000001', 'letan@hotel.com', CURDATE(), 'DANG_LAM'
FROM tai_khoan t JOIN chuc_vu c ON c.ma_chuc_vu = 'LE_TAN' WHERE t.email = 'letan@hotel.com';

INSERT IGNORE INTO tai_khoan_vai_tro (tai_khoan_id, vai_tro_id)
SELECT t.id, v.id FROM tai_khoan t JOIN vai_tro v ON v.ma_vai_tro = 'ADMIN' WHERE t.email = 'admin.com';
INSERT IGNORE INTO tai_khoan_vai_tro (tai_khoan_id, vai_tro_id)
SELECT t.id, v.id FROM tai_khoan t JOIN vai_tro v ON v.ma_vai_tro = 'RECEPTIONIST' WHERE t.email = 'letan@hotel.com';

-- 3. KHACH HANG (da dang ky tren web) ------------------------------
INSERT IGNORE INTO khach_hang (ma_khach_hang, tai_khoan_id, ho_ten, ngay_sinh, so_dien_thoai, email, cccd_ho_chieu, dia_chi)
SELECT 'KHDEMO01', t.id, 'Nguyễn Văn An',  '1998-05-12', '0901234567', t.email, '079098000001', 'Quận Gò Vấp, TP. Hồ Chí Minh' FROM tai_khoan t WHERE t.email = 'an.nguyen@example.com';
INSERT IGNORE INTO khach_hang (ma_khach_hang, tai_khoan_id, ho_ten, ngay_sinh, so_dien_thoai, email, cccd_ho_chieu, dia_chi)
SELECT 'KHDEMO02', t.id, 'Trần Thị Bình',  '1995-09-03', '0912345678', t.email, '079095000002', 'Quận 1, TP. Hồ Chí Minh' FROM tai_khoan t WHERE t.email = 'binh.tran@example.com';
INSERT IGNORE INTO khach_hang (ma_khach_hang, tai_khoan_id, ho_ten, ngay_sinh, so_dien_thoai, email, cccd_ho_chieu, dia_chi)
SELECT 'KHDEMO03', t.id, 'Lê Quốc Cường',  '1990-11-21', '0923456789', t.email, '079090000003', 'TP. Thủ Đức, TP. Hồ Chí Minh' FROM tai_khoan t WHERE t.email = 'cuong.le@example.com';
INSERT IGNORE INTO khach_hang (ma_khach_hang, tai_khoan_id, ho_ten, ngay_sinh, so_dien_thoai, email, cccd_ho_chieu, dia_chi)
SELECT 'KHDEMO04', t.id, 'Phạm Thùy Dung', '2000-02-28', '0934567890', t.email, '079100000004', 'Quận 7, TP. Hồ Chí Minh' FROM tai_khoan t WHERE t.email = 'dung.pham@example.com';

-- 4. KHACH SAN, LOAI PHONG, PHONG ----------------------------------
INSERT INTO khach_san (ten_khach_san, dia_chi, so_dien_thoai, email, gio_nhan_phong, gio_tra_phong, mo_ta, trang_thai)
SELECT 'Khách sạn Demo', 'Địa chỉ demo, TP. Hồ Chí Minh', '0900000000', 'lienhe@hotel.com', '14:00:00', '12:00:00',
       'Khách sạn mẫu dùng để kiểm thử hệ thống đặt phòng.', 'HOAT_DONG'
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM khach_san);

INSERT IGNORE INTO tien_nghi (ma_tien_nghi, ten_tien_nghi, trang_thai) VALUES
  ('WIFI', 'Wi-Fi miễn phí', 'HOAT_DONG'), ('DIEU_HOA', 'Điều hòa', 'HOAT_DONG'),
  ('TV', 'Tivi màn hình phẳng', 'HOAT_DONG'), ('MINIBAR', 'Minibar', 'HOAT_DONG'),
  ('BON_TAM', 'Bồn tắm', 'HOAT_DONG'), ('BAN_CONG', 'Ban công', 'HOAT_DONG'),
  ('BUA_SANG', 'Bữa sáng', 'HOAT_DONG');

INSERT IGNORE INTO loai_phong (ma_loai_phong, ten_loai_phong, so_nguoi_lon_toi_da, so_tre_em_toi_da, dien_tich_m2, gia_co_ban_dem, mo_ta, trang_thai) VALUES
  ('STD',   'Standard', 2, 1, 28, 800000,  'Phòng tiêu chuẩn gọn gàng, đầy đủ tiện nghi cơ bản.', 'HOAT_DONG'),
  ('DLX',   'Deluxe',   2, 1, 35, 1200000, 'Phòng rộng rãi, có ban công và tầm nhìn đẹp.', 'HOAT_DONG'),
  ('SUITE', 'Suite',    3, 2, 55, 2200000, 'Phòng hạng sang gồm phòng khách riêng và bồn tắm.', 'HOAT_DONG');

INSERT IGNORE INTO loai_phong_tien_nghi (loai_phong_id, tien_nghi_id)
SELECT l.id, n.id FROM loai_phong l JOIN tien_nghi n
WHERE (l.ma_loai_phong = 'STD'   AND n.ma_tien_nghi IN ('WIFI','DIEU_HOA','TV'))
   OR (l.ma_loai_phong = 'DLX'   AND n.ma_tien_nghi IN ('WIFI','DIEU_HOA','TV','MINIBAR','BAN_CONG'))
   OR (l.ma_loai_phong = 'SUITE' AND n.ma_tien_nghi IN ('WIFI','DIEU_HOA','TV','MINIBAR','BON_TAM','BAN_CONG','BUA_SANG'));

INSERT IGNORE INTO phong (ma_phong, loai_phong_id, so_phong, tang, loai_view, phu_thu_dem_mac_dinh, trang_thai, mo_ta)
SELECT r.ma, l.id, r.so, r.tang, r.vw, 0, 'SAN_SANG', CONCAT('Phòng ', r.so) FROM loai_phong l JOIN (
  SELECT 'P101' AS ma, '101' AS so, 'STD' AS lp, '1' AS tang, 'Hướng phố' AS vw
  UNION ALL
  SELECT 'P102' AS ma, '102' AS so, 'STD' AS lp, '1' AS tang, 'Hướng phố' AS vw
  UNION ALL
  SELECT 'P103' AS ma, '103' AS so, 'STD' AS lp, '1' AS tang, 'Hướng nội khu' AS vw
  UNION ALL
  SELECT 'P201' AS ma, '201' AS so, 'DLX' AS lp, '2' AS tang, 'Hướng sông' AS vw
  UNION ALL
  SELECT 'P202' AS ma, '202' AS so, 'DLX' AS lp, '2' AS tang, 'Hướng sông' AS vw
  UNION ALL
  SELECT 'P203' AS ma, '203' AS so, 'DLX' AS lp, '2' AS tang, 'Hướng thành phố' AS vw
  UNION ALL
  SELECT 'P301' AS ma, '301' AS so, 'SUITE' AS lp, '3' AS tang, 'Toàn cảnh thành phố' AS vw
  UNION ALL
  SELECT 'P302' AS ma, '302' AS so, 'SUITE' AS lp, '3' AS tang, 'Toàn cảnh thành phố' AS vw
) r ON r.lp = l.ma_loai_phong;

-- 5. CHINH SACH ---------------------------------------------------
INSERT INTO chinh_sach_coc (ten_chinh_sach, kieu_tinh, gia_tri, trang_thai)
SELECT 'Đặt cọc 30%', 'PHAN_TRAM', 30, 'HOAT_DONG' FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM chinh_sach_coc WHERE ten_chinh_sach = 'Đặt cọc 30%');

INSERT INTO chinh_sach_huy (ten_chinh_sach, mo_ta, trang_thai)
SELECT 'Hủy linh hoạt', 'Hoàn 100% nếu hủy trước 48 giờ, 50% nếu hủy trước 24 giờ.', 'HOAT_DONG'
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM chinh_sach_huy WHERE ten_chinh_sach = 'Hủy linh hoạt');

INSERT INTO chi_tiet_chinh_sach_huy (chinh_sach_huy_id, so_gio_truoc_toi_thieu, phan_tram_hoan, phi_huy_co_dinh)
SELECT h.id, v.gio, v.pt, 0 FROM chinh_sach_huy h
JOIN (SELECT 48 AS gio, 100 AS pt UNION ALL SELECT 24, 50 UNION ALL SELECT 0, 0) v
WHERE h.ten_chinh_sach = 'Hủy linh hoạt'
  AND NOT EXISTS (SELECT 1 FROM chi_tiet_chinh_sach_huy d WHERE d.chinh_sach_huy_id = h.id);

-- 6. DAT PHONG MAU -------------------------------------------------
-- DPDEMO0001: KHDEMO01 / phòng P201 / DA_TRA
INSERT IGNORE INTO dat_phong (ma_dat_phong, khach_hang_id, tai_khoan_tao_id, chinh_sach_coc_id, chinh_sach_huy_id, ngay_dat, du_kien_nhan, du_kien_tra, so_nguoi_lon, so_tre_em, kenh_dat, trang_thai, yeu_cau_dac_biet, tong_tien_phong_goc, tong_tien_du_kien)
SELECT 'DPDEMO0001', k.id, k.tai_khoan_id, (SELECT MIN(id) FROM chinh_sach_coc WHERE ten_chinh_sach = 'Đặt cọc 30%'), (SELECT MIN(id) FROM chinh_sach_huy WHERE ten_chinh_sach = 'Hủy linh hoạt'),
       DATE_SUB(NOW(), INTERVAL 22 DAY), CONCAT(DATE_ADD(CURDATE(), INTERVAL -20 DAY), ' 14:00:00'), CONCAT(DATE_ADD(CURDATE(), INTERVAL -18 DAY), ' 12:00:00'), 2, 0, 'WEB', 'DA_TRA', 'Phòng yên tĩnh, tầng cao.', 2400000, 2400000
FROM khach_hang k WHERE k.ma_khach_hang = 'KHDEMO01';
INSERT IGNORE INTO chi_tiet_dat_phong (dat_phong_id, phong_id, so_nguoi_lon, so_tre_em, tien_phong_du_kien, nhan_phong_luc, tra_phong_luc, trang_thai)
SELECT d.id, p.id, 2, 0, 2400000, CONCAT(DATE_ADD(CURDATE(), INTERVAL -20 DAY), ' 14:00:00'), CONCAT(DATE_ADD(CURDATE(), INTERVAL -18 DAY), ' 12:00:00'), 'DA_TRA' FROM dat_phong d JOIN phong p ON p.ma_phong = 'P201' WHERE d.ma_dat_phong = 'DPDEMO0001';
INSERT IGNORE INTO gia_dem_dat_phong (chi_tiet_dat_phong_id, ngay_luu_tru, gia_co_ban, phu_thu_phong, tien_dieu_chinh, gia_sau_dieu_chinh)
SELECT c.id, DATE_ADD(CURDATE(), INTERVAL -20 DAY), 1200000, 0, 0, 1200000 FROM chi_tiet_dat_phong c JOIN dat_phong d ON d.id = c.dat_phong_id WHERE d.ma_dat_phong = 'DPDEMO0001';
INSERT IGNORE INTO gia_dem_dat_phong (chi_tiet_dat_phong_id, ngay_luu_tru, gia_co_ban, phu_thu_phong, tien_dieu_chinh, gia_sau_dieu_chinh)
SELECT c.id, DATE_ADD(CURDATE(), INTERVAL -19 DAY), 1200000, 0, 0, 1200000 FROM chi_tiet_dat_phong c JOIN dat_phong d ON d.id = c.dat_phong_id WHERE d.ma_dat_phong = 'DPDEMO0001';
INSERT IGNORE INTO phieu_coc (ma_phieu_coc, dat_phong_id, tai_khoan_thanh_toan_id, phuong_thuc_id, so_tien, cong_thanh_toan, ma_giao_dich, thoi_gian_coc, trang_thai)
SELECT 'PCDPDEMO0001', d.id, d.tai_khoan_tao_id, (SELECT id FROM phuong_thuc_thanh_toan WHERE ma_phuong_thuc = 'VIETQR'), 720000, 'VietQR', 'DEMO-DPDEMO0001', d.ngay_dat, 'DA_THANH_TOAN' FROM dat_phong d WHERE d.ma_dat_phong = 'DPDEMO0001';
INSERT INTO lich_su_trang_thai_dat_phong (dat_phong_id, trang_thai_cu, trang_thai_moi, tai_khoan_thuc_hien_id, ly_do)
SELECT d.id, NULL, 'DA_TRA', d.tai_khoan_tao_id, 'Dữ liệu mẫu' FROM dat_phong d WHERE d.ma_dat_phong = 'DPDEMO0001'
  AND NOT EXISTS (SELECT 1 FROM lich_su_trang_thai_dat_phong h WHERE h.dat_phong_id = d.id);

-- DPDEMO0002: KHDEMO02 / phòng P101 / DA_TRA
INSERT IGNORE INTO dat_phong (ma_dat_phong, khach_hang_id, tai_khoan_tao_id, chinh_sach_coc_id, chinh_sach_huy_id, ngay_dat, du_kien_nhan, du_kien_tra, so_nguoi_lon, so_tre_em, kenh_dat, trang_thai, yeu_cau_dac_biet, tong_tien_phong_goc, tong_tien_du_kien)
SELECT 'DPDEMO0002', k.id, k.tai_khoan_id, (SELECT MIN(id) FROM chinh_sach_coc WHERE ten_chinh_sach = 'Đặt cọc 30%'), (SELECT MIN(id) FROM chinh_sach_huy WHERE ten_chinh_sach = 'Hủy linh hoạt'),
       DATE_SUB(NOW(), INTERVAL 12 DAY), CONCAT(DATE_ADD(CURDATE(), INTERVAL -10 DAY), ' 14:00:00'), CONCAT(DATE_ADD(CURDATE(), INTERVAL -8 DAY), ' 12:00:00'), 2, 0, 'WEB', 'DA_TRA', NULL, 1600000, 1600000
FROM khach_hang k WHERE k.ma_khach_hang = 'KHDEMO02';
INSERT IGNORE INTO chi_tiet_dat_phong (dat_phong_id, phong_id, so_nguoi_lon, so_tre_em, tien_phong_du_kien, nhan_phong_luc, tra_phong_luc, trang_thai)
SELECT d.id, p.id, 2, 0, 1600000, CONCAT(DATE_ADD(CURDATE(), INTERVAL -10 DAY), ' 14:00:00'), CONCAT(DATE_ADD(CURDATE(), INTERVAL -8 DAY), ' 12:00:00'), 'DA_TRA' FROM dat_phong d JOIN phong p ON p.ma_phong = 'P101' WHERE d.ma_dat_phong = 'DPDEMO0002';
INSERT IGNORE INTO gia_dem_dat_phong (chi_tiet_dat_phong_id, ngay_luu_tru, gia_co_ban, phu_thu_phong, tien_dieu_chinh, gia_sau_dieu_chinh)
SELECT c.id, DATE_ADD(CURDATE(), INTERVAL -10 DAY), 800000, 0, 0, 800000 FROM chi_tiet_dat_phong c JOIN dat_phong d ON d.id = c.dat_phong_id WHERE d.ma_dat_phong = 'DPDEMO0002';
INSERT IGNORE INTO gia_dem_dat_phong (chi_tiet_dat_phong_id, ngay_luu_tru, gia_co_ban, phu_thu_phong, tien_dieu_chinh, gia_sau_dieu_chinh)
SELECT c.id, DATE_ADD(CURDATE(), INTERVAL -9 DAY), 800000, 0, 0, 800000 FROM chi_tiet_dat_phong c JOIN dat_phong d ON d.id = c.dat_phong_id WHERE d.ma_dat_phong = 'DPDEMO0002';
INSERT IGNORE INTO phieu_coc (ma_phieu_coc, dat_phong_id, tai_khoan_thanh_toan_id, phuong_thuc_id, so_tien, cong_thanh_toan, ma_giao_dich, thoi_gian_coc, trang_thai)
SELECT 'PCDPDEMO0002', d.id, d.tai_khoan_tao_id, (SELECT id FROM phuong_thuc_thanh_toan WHERE ma_phuong_thuc = 'VIETQR'), 480000, 'VietQR', 'DEMO-DPDEMO0002', d.ngay_dat, 'DA_THANH_TOAN' FROM dat_phong d WHERE d.ma_dat_phong = 'DPDEMO0002';
INSERT INTO lich_su_trang_thai_dat_phong (dat_phong_id, trang_thai_cu, trang_thai_moi, tai_khoan_thuc_hien_id, ly_do)
SELECT d.id, NULL, 'DA_TRA', d.tai_khoan_tao_id, 'Dữ liệu mẫu' FROM dat_phong d WHERE d.ma_dat_phong = 'DPDEMO0002'
  AND NOT EXISTS (SELECT 1 FROM lich_su_trang_thai_dat_phong h WHERE h.dat_phong_id = d.id);

-- DPDEMO0003: KHDEMO01 / phòng P301 / BOOKED
INSERT IGNORE INTO dat_phong (ma_dat_phong, khach_hang_id, tai_khoan_tao_id, chinh_sach_coc_id, chinh_sach_huy_id, ngay_dat, du_kien_nhan, du_kien_tra, so_nguoi_lon, so_tre_em, kenh_dat, trang_thai, yeu_cau_dac_biet, tong_tien_phong_goc, tong_tien_du_kien)
SELECT 'DPDEMO0003', k.id, k.tai_khoan_id, (SELECT MIN(id) FROM chinh_sach_coc WHERE ten_chinh_sach = 'Đặt cọc 30%'), (SELECT MIN(id) FROM chinh_sach_huy WHERE ten_chinh_sach = 'Hủy linh hoạt'),
       DATE_SUB(NOW(), INTERVAL 3 DAY), CONCAT(DATE_ADD(CURDATE(), INTERVAL 10 DAY), ' 14:00:00'), CONCAT(DATE_ADD(CURDATE(), INTERVAL 12 DAY), ' 12:00:00'), 2, 0, 'WEB', 'BOOKED', 'Cho mình xin giường đôi.', 4400000, 4400000
FROM khach_hang k WHERE k.ma_khach_hang = 'KHDEMO01';
INSERT IGNORE INTO chi_tiet_dat_phong (dat_phong_id, phong_id, so_nguoi_lon, so_tre_em, tien_phong_du_kien, trang_thai)
SELECT d.id, p.id, 2, 0, 4400000, 'BOOKED' FROM dat_phong d JOIN phong p ON p.ma_phong = 'P301' WHERE d.ma_dat_phong = 'DPDEMO0003';
INSERT IGNORE INTO gia_dem_dat_phong (chi_tiet_dat_phong_id, ngay_luu_tru, gia_co_ban, phu_thu_phong, tien_dieu_chinh, gia_sau_dieu_chinh)
SELECT c.id, DATE_ADD(CURDATE(), INTERVAL 10 DAY), 2200000, 0, 0, 2200000 FROM chi_tiet_dat_phong c JOIN dat_phong d ON d.id = c.dat_phong_id WHERE d.ma_dat_phong = 'DPDEMO0003';
INSERT IGNORE INTO khoa_lich_phong (chi_tiet_dat_phong_id, phong_id, ngay_luu_tru, trang_thai)
SELECT c.id, c.phong_id, DATE_ADD(CURDATE(), INTERVAL 10 DAY), 'BOOKED' FROM chi_tiet_dat_phong c JOIN dat_phong d ON d.id = c.dat_phong_id WHERE d.ma_dat_phong = 'DPDEMO0003';
INSERT IGNORE INTO gia_dem_dat_phong (chi_tiet_dat_phong_id, ngay_luu_tru, gia_co_ban, phu_thu_phong, tien_dieu_chinh, gia_sau_dieu_chinh)
SELECT c.id, DATE_ADD(CURDATE(), INTERVAL 11 DAY), 2200000, 0, 0, 2200000 FROM chi_tiet_dat_phong c JOIN dat_phong d ON d.id = c.dat_phong_id WHERE d.ma_dat_phong = 'DPDEMO0003';
INSERT IGNORE INTO khoa_lich_phong (chi_tiet_dat_phong_id, phong_id, ngay_luu_tru, trang_thai)
SELECT c.id, c.phong_id, DATE_ADD(CURDATE(), INTERVAL 11 DAY), 'BOOKED' FROM chi_tiet_dat_phong c JOIN dat_phong d ON d.id = c.dat_phong_id WHERE d.ma_dat_phong = 'DPDEMO0003';
INSERT IGNORE INTO phieu_coc (ma_phieu_coc, dat_phong_id, tai_khoan_thanh_toan_id, phuong_thuc_id, so_tien, cong_thanh_toan, ma_giao_dich, thoi_gian_coc, trang_thai)
SELECT 'PCDPDEMO0003', d.id, d.tai_khoan_tao_id, (SELECT id FROM phuong_thuc_thanh_toan WHERE ma_phuong_thuc = 'VIETQR'), 1320000, 'VietQR', 'DEMO-DPDEMO0003', d.ngay_dat, 'DA_THANH_TOAN' FROM dat_phong d WHERE d.ma_dat_phong = 'DPDEMO0003';
INSERT INTO lich_su_trang_thai_dat_phong (dat_phong_id, trang_thai_cu, trang_thai_moi, tai_khoan_thuc_hien_id, ly_do)
SELECT d.id, NULL, 'BOOKED', d.tai_khoan_tao_id, 'Dữ liệu mẫu' FROM dat_phong d WHERE d.ma_dat_phong = 'DPDEMO0003'
  AND NOT EXISTS (SELECT 1 FROM lich_su_trang_thai_dat_phong h WHERE h.dat_phong_id = d.id);

-- DPDEMO0004: KHDEMO03 / phòng P202 / DA_HUY
INSERT IGNORE INTO dat_phong (ma_dat_phong, khach_hang_id, tai_khoan_tao_id, chinh_sach_coc_id, chinh_sach_huy_id, ngay_dat, du_kien_nhan, du_kien_tra, so_nguoi_lon, so_tre_em, kenh_dat, trang_thai, yeu_cau_dac_biet, tong_tien_phong_goc, tong_tien_du_kien)
SELECT 'DPDEMO0004', k.id, k.tai_khoan_id, (SELECT MIN(id) FROM chinh_sach_coc WHERE ten_chinh_sach = 'Đặt cọc 30%'), (SELECT MIN(id) FROM chinh_sach_huy WHERE ten_chinh_sach = 'Hủy linh hoạt'),
       DATE_SUB(NOW(), INTERVAL 5 DAY), CONCAT(DATE_ADD(CURDATE(), INTERVAL 20 DAY), ' 14:00:00'), CONCAT(DATE_ADD(CURDATE(), INTERVAL 22 DAY), ' 12:00:00'), 2, 0, 'WEB', 'DA_HUY', NULL, 2400000, 2400000
FROM khach_hang k WHERE k.ma_khach_hang = 'KHDEMO03';
INSERT IGNORE INTO chi_tiet_dat_phong (dat_phong_id, phong_id, so_nguoi_lon, so_tre_em, tien_phong_du_kien, trang_thai)
SELECT d.id, p.id, 2, 0, 2400000, 'DA_HUY' FROM dat_phong d JOIN phong p ON p.ma_phong = 'P202' WHERE d.ma_dat_phong = 'DPDEMO0004';
INSERT IGNORE INTO gia_dem_dat_phong (chi_tiet_dat_phong_id, ngay_luu_tru, gia_co_ban, phu_thu_phong, tien_dieu_chinh, gia_sau_dieu_chinh)
SELECT c.id, DATE_ADD(CURDATE(), INTERVAL 20 DAY), 1200000, 0, 0, 1200000 FROM chi_tiet_dat_phong c JOIN dat_phong d ON d.id = c.dat_phong_id WHERE d.ma_dat_phong = 'DPDEMO0004';
INSERT IGNORE INTO gia_dem_dat_phong (chi_tiet_dat_phong_id, ngay_luu_tru, gia_co_ban, phu_thu_phong, tien_dieu_chinh, gia_sau_dieu_chinh)
SELECT c.id, DATE_ADD(CURDATE(), INTERVAL 21 DAY), 1200000, 0, 0, 1200000 FROM chi_tiet_dat_phong c JOIN dat_phong d ON d.id = c.dat_phong_id WHERE d.ma_dat_phong = 'DPDEMO0004';
INSERT INTO lich_su_trang_thai_dat_phong (dat_phong_id, trang_thai_cu, trang_thai_moi, tai_khoan_thuc_hien_id, ly_do)
SELECT d.id, NULL, 'DA_HUY', d.tai_khoan_tao_id, 'Dữ liệu mẫu' FROM dat_phong d WHERE d.ma_dat_phong = 'DPDEMO0004'
  AND NOT EXISTS (SELECT 1 FROM lich_su_trang_thai_dat_phong h WHERE h.dat_phong_id = d.id);

-- 7. DANH GIA (booking DPDEMO0001 da danh gia; DPDEMO0002 de ban tu thu danh gia tren web) --
INSERT IGNORE INTO danh_gia (ma_danh_gia, dat_phong_id, khach_hang_id, diem_tong, diem_phong, diem_dich_vu, noi_dung, phan_hoi_khach_san, nhan_vien_phan_hoi_id, ngay_phan_hoi)
SELECT 'DGDEMO0001', d.id, d.khach_hang_id, 5, 5, 4, 'Phòng sạch sẽ, nhân viên thân thiện. Sẽ quay lại!',
       'Cảm ơn bạn đã lưu trú. Hẹn gặp lại bạn!', (SELECT id FROM nhan_vien WHERE ma_nhan_vien = 'NV001'), NOW()
FROM dat_phong d WHERE d.ma_dat_phong = 'DPDEMO0001';
