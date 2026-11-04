-- ============================================================
-- DỮ LIỆU MẪU CHO MODULE KHO & TÀI SẢN (tùy chọn)
-- Chạy SAU seed_demo.sql (cần NCC_NUOC, NCC_VATTU, KHO_CHINH và 6 mặt hàng của seed_demo).
-- Dùng database đang chọn. Chạy lặp được: mọi mã bắt đầu bằng "DEMO-KHO-", đã có thì bỏ qua.
-- Thời gian lưu UTC (giống backend).
--
-- Kịch bản sau khi chạy (14 nhóm dữ liệu):
--   1  NCC mới            : NCC_THUCPHAM (Thực phẩm Bếp Xanh)
--   2  Kho mới            : KHO_TANG (kho buồng phòng tầng, trống - để thử chuyển tài sản/nhập kho khác)
--   3  Mặt hàng mới       : CA_PHE_GOI (lô + HSD), BAN_CHAI (vật tư), TV_43 (tài sản, serial)
--   4  PN01 đã xác nhận   : Nước suối 2 lô (1 lô SẮP HẾT HẠN), Bia 2 lô (1 lô ĐÃ HẾT HẠN)
--   5  PN02 đã xác nhận   : Khăn tắm 50, Dầu gội 200, Giấy VS 30 (dưới tồn tối thiểu), Máy sấy 3 cái có serial
--   6  PN03 NHÁP          : Cà phê 40, Bàn chải 100, Tivi 2 -> để test "Sửa" và "Xác nhận nhập kho" trên app
--   7  PN04 ĐÃ HỦY        : để test bộ lọc trạng thái
--   8  PX01 đã xác nhận   : xuất buồng phòng: Khăn tắm 10, Dầu gội 40
--   9  PX02 đã xác nhận   : xuất minibar Nước suối 30 theo FEFO (lấy từ lô sắp hết hạn NS-A trước)
--   10 PX03 NHÁP          : xuất minibar Bia 12 -> để test "Xác nhận xuất kho" (FEFO bỏ qua lô đã hết hạn)
--   11 Điều chỉnh HONG_MAT: Khăn tắm -2 (rách)
--   12 Điều chỉnh kiểm kê : Dầu gội +5
--   13 Tài sản            : 3 máy sấy (2 đang dùng ở phòng 101, 103; 1 trong kho)
--   14 Tài sản hỏng       : máy sấy ở phòng 103 chuyển BAO_TRI (không ảnh hưởng tồn kho vì đang ở phòng)
--
-- Tồn kho KHO_CHINH sau khi chạy (cột "Tồn"):
--   Nước suối 110 (lô NS-A còn 10, HSD còn 5 ngày -> cảnh báo; NS-B 100)   Bia 54 (BL-OLD 6 đã hết hạn + BL-B 48)
--   Khăn tắm 38 (< 40 tối thiểu -> cảnh báo tồn thấp)      Dầu gội 165     Giấy VS 30 (< 60 -> cảnh báo)
--   Máy sấy 1 (2 cái đã chuyển vào phòng)
-- ============================================================
SET NAMES utf8mb4;
SET time_zone = '+00:00';
START TRANSACTION;

SET @nv   = (SELECT n.id FROM nhan_vien n JOIN tai_khoan t ON t.id = n.tai_khoan_id WHERE t.email = 'kho@hotel.com' LIMIT 1);
SET @nv   = COALESCE(@nv, (SELECT n.id FROM nhan_vien n JOIN tai_khoan t ON t.id = n.tai_khoan_id WHERE t.email = 'admin.com' LIMIT 1));
SET @kho  = (SELECT id FROM kho WHERE ma_kho = 'KHO_CHINH');
SET @ncc_nuoc   = (SELECT id FROM nha_cung_cap WHERE ma_nha_cung_cap = 'NCC_NUOC');
SET @ncc_vattu  = (SELECT id FROM nha_cung_cap WHERE ma_nha_cung_cap = 'NCC_VATTU');
SET @today = DATE(UTC_TIMESTAMP() + INTERVAL 7 HOUR);   -- ngày theo giờ VN

-- 1. NCC mới ------------------------------------------------------------------
INSERT IGNORE INTO nha_cung_cap (ma_nha_cung_cap, ten_nha_cung_cap, nguoi_lien_he, so_dien_thoai, email, dia_chi, ma_so_thue, trang_thai) VALUES
  ('NCC_THUCPHAM', 'Thực phẩm Bếp Xanh', 'Anh Tuấn', '0283456789', 'order@bepxanh.vn', 'Q. 7, TP.HCM', '0312345678', 'HOAT_DONG');
SET @ncc_tp = (SELECT id FROM nha_cung_cap WHERE ma_nha_cung_cap = 'NCC_THUCPHAM');

-- 2. Kho mới ------------------------------------------------------------------
INSERT IGNORE INTO kho (ma_kho, ten_kho, loai_kho, vi_tri, ghi_chu, trang_thai) VALUES
  ('KHO_TANG', 'Kho buồng phòng tầng 3', 'KHO_BUONG_PHONG', 'Tầng 3, cạnh thang máy', 'Kho demo, chưa có tồn', 'HOAT_DONG');

-- 3. Mặt hàng mới ---------------------------------------------------------------
INSERT IGNORE INTO danh_muc_hang_hoa (ma_hang, ten_hang, loai_hang, nhom_hang_id, don_vi_tinh_id, gia_von_tham_chieu, muc_ton_toi_thieu, theo_doi_lo, theo_doi_han_su_dung, theo_doi_ca_the, dieu_kien_bao_quan, trang_thai)
SELECT v.ma, v.ten, v.loai, (SELECT id FROM nhom_hang WHERE ma_nhom = v.nhom), (SELECT id FROM don_vi_tinh WHERE ma_don_vi = v.dvt), v.gia, v.toithieu, v.lo, v.hsd, v.cathe, v.baoquan, 'HOAT_DONG'
FROM (
  SELECT 'CA_PHE_GOI' AS ma, 'Cà phê hòa tan (gói)' AS ten, 'HANG_MINIBAR' AS loai, 'MINIBAR' AS nhom, 'GOI' AS dvt, 3000 AS gia, 30 AS toithieu, 1 AS lo, 1 AS hsd, 0 AS cathe, 'Nơi khô ráo' AS baoquan UNION ALL
  SELECT 'BAN_CHAI', 'Bàn chải đánh răng', 'VAT_TU', 'VAT_TU_BUONG', 'CAI', 2500, 80, 0, 0, 0, NULL UNION ALL
  SELECT 'TV_43', 'Tivi LED 43 inch', 'TAI_SAN', 'TAI_SAN_PHONG', 'CAI', 6500000, 0, 0, 0, 1, NULL
) v;

SET @h_ns   = (SELECT id FROM danh_muc_hang_hoa WHERE ma_hang = 'NUOC_SUOI');
SET @h_bia  = (SELECT id FROM danh_muc_hang_hoa WHERE ma_hang = 'BIA_LON');
SET @h_khan = (SELECT id FROM danh_muc_hang_hoa WHERE ma_hang = 'KHAN_TAM');
SET @h_dg   = (SELECT id FROM danh_muc_hang_hoa WHERE ma_hang = 'DAU_GOI');
SET @h_gvs  = (SELECT id FROM danh_muc_hang_hoa WHERE ma_hang = 'GIAY_VS');
SET @h_ms   = (SELECT id FROM danh_muc_hang_hoa WHERE ma_hang = 'MAY_SAY');
SET @h_cf   = (SELECT id FROM danh_muc_hang_hoa WHERE ma_hang = 'CA_PHE_GOI');
SET @h_bc   = (SELECT id FROM danh_muc_hang_hoa WHERE ma_hang = 'BAN_CHAI');
SET @h_tv   = (SELECT id FROM danh_muc_hang_hoa WHERE ma_hang = 'TV_43');

-- 4. PN01 (đã xác nhận, 30 ngày trước): nước & bia ------------------------------
SET @t_pn1 = UTC_TIMESTAMP() - INTERVAL 30 DAY;
INSERT INTO phieu_nhap (ma_phieu_nhap, nha_cung_cap_id, kho_id, nhan_vien_tao_id, nhan_vien_xac_nhan_id, ngay_nhap, xac_nhan_luc, so_hoa_don_ncc, ngay_hoa_don_ncc,
                        tong_tien_hang, chiet_khau, tien_thue, tong_thanh_toan, trang_thai, ghi_chu, created_at, updated_at)
SELECT 'DEMO-KHO-PN01', @ncc_nuoc, @kho, @nv, @nv, @t_pn1, @t_pn1, 'HD-NGK-0101', DATE(@t_pn1),
       1830000, 30000, 0, 1800000, 'DA_XAC_NHAN', 'Nhập nước & bia cho minibar', @t_pn1, @t_pn1
WHERE NOT EXISTS (SELECT 1 FROM phieu_nhap WHERE ma_phieu_nhap = 'DEMO-KHO-PN01');
SET @pn1 = (SELECT id FROM phieu_nhap WHERE ma_phieu_nhap = 'DEMO-KHO-PN01');

-- Dòng: (ghi_chu dùng làm khóa để chạy lặp)
INSERT INTO chi_tiet_phieu_nhap (phieu_nhap_id, danh_muc_hang_hoa_id, so_luong, don_gia_nhap, thanh_tien, ghi_chu)
SELECT @pn1, v.h, v.sl, v.gia, v.sl * v.gia, v.k FROM (
  SELECT @h_ns AS h, 40 AS sl, 4500 AS gia, 'L-NS-A' AS k UNION ALL
  SELECT @h_ns, 100, 4500, 'L-NS-B' UNION ALL
  SELECT @h_bia, 6, 14000, 'L-BL-OLD' UNION ALL
  SELECT @h_bia, 48, 14000, 'L-BL-B'
) v WHERE NOT EXISTS (SELECT 1 FROM chi_tiet_phieu_nhap c WHERE c.phieu_nhap_id = @pn1 AND c.ghi_chu = v.k);
-- Tổng tiền hàng tính lại từ chi tiết để khớp kiểm tra của API khi xem/xác nhận
UPDATE phieu_nhap p SET tong_tien_hang = (SELECT SUM(thanh_tien) FROM chi_tiet_phieu_nhap WHERE phieu_nhap_id = p.id),
                        tong_thanh_toan = (SELECT SUM(thanh_tien) FROM chi_tiet_phieu_nhap WHERE phieu_nhap_id = p.id) - p.chiet_khau + p.tien_thue
WHERE p.id = @pn1;

-- Lô hàng: NS-A sắp hết hạn (còn 5 ngày), BL-OLD đã hết hạn (3 ngày trước)
INSERT IGNORE INTO lo_hang (ma_lo, danh_muc_hang_hoa_id, chi_tiet_phieu_nhap_id, kho_id, ngay_san_xuat, han_su_dung, so_luong_ban_dau, don_gia_nhap, trang_thai, ghi_chu, created_at)
SELECT v.ma_lo, c.danh_muc_hang_hoa_id, c.id, @kho, v.nsx, v.hsd, c.so_luong, c.don_gia_nhap, v.tt, 'DEMO-KHO', @t_pn1
FROM chi_tiet_phieu_nhap c JOIN (
  SELECT 'L-NS-A' AS k, 'NS-A' AS ma_lo, @today - INTERVAL 360 DAY AS nsx, @today + INTERVAL 5 DAY AS hsd, 'SAP_HET_HAN' AS tt UNION ALL
  SELECT 'L-NS-B', 'NS-B', @today - INTERVAL 40 DAY, @today + INTERVAL 320 DAY, 'CON_HAN' UNION ALL
  SELECT 'L-BL-OLD', 'BL-OLD', @today - INTERVAL 180 DAY, @today - INTERVAL 3 DAY, 'HET_HAN' UNION ALL
  SELECT 'L-BL-B', 'BL-B', @today - INTERVAL 35 DAY, @today + INTERVAL 150 DAY, 'CON_HAN'
) v ON v.k = c.ghi_chu
WHERE c.phieu_nhap_id = @pn1;

INSERT IGNORE INTO giao_dich_kho (ma_giao_dich, kho_id, danh_muc_hang_hoa_id, lo_hang_id, phieu_nhap_id, chi_tiet_phieu_nhap_id, nhan_vien_id, loai_giao_dich, so_luong_thay_doi, thoi_gian, ghi_chu)
SELECT CONCAT('DEMO-KHO-NK1-', l.ma_lo), @kho, l.danh_muc_hang_hoa_id, l.id, @pn1, l.chi_tiet_phieu_nhap_id, @nv, 'NHAP', l.so_luong_ban_dau, @t_pn1, 'Nhập PN01'
FROM lo_hang l JOIN chi_tiet_phieu_nhap c ON c.id = l.chi_tiet_phieu_nhap_id WHERE c.phieu_nhap_id = @pn1;

-- 5. PN02 (đã xác nhận, 20 ngày trước): vật tư buồng phòng + máy sấy -------------
SET @t_pn2 = UTC_TIMESTAMP() - INTERVAL 20 DAY;
INSERT INTO phieu_nhap (ma_phieu_nhap, nha_cung_cap_id, kho_id, nhan_vien_tao_id, nhan_vien_xac_nhan_id, ngay_nhap, xac_nhan_luc, so_hoa_don_ncc, ngay_hoa_don_ncc,
                        tong_tien_hang, chiet_khau, tien_thue, tong_thanh_toan, trang_thai, ghi_chu, created_at, updated_at)
SELECT 'DEMO-KHO-PN02', @ncc_vattu, @kho, @nv, @nv, @t_pn2, @t_pn2, 'HP-2026-118', DATE(@t_pn2),
       0, 0, 0, 0, 'DA_XAC_NHAN', 'Nhập vật tư buồng phòng và máy sấy tóc', @t_pn2, @t_pn2
WHERE NOT EXISTS (SELECT 1 FROM phieu_nhap WHERE ma_phieu_nhap = 'DEMO-KHO-PN02');
SET @pn2 = (SELECT id FROM phieu_nhap WHERE ma_phieu_nhap = 'DEMO-KHO-PN02');
INSERT INTO chi_tiet_phieu_nhap (phieu_nhap_id, danh_muc_hang_hoa_id, so_luong, don_gia_nhap, thanh_tien, ghi_chu)
SELECT @pn2, v.h, v.sl, v.gia, v.sl * v.gia, v.k FROM (
  SELECT @h_khan AS h, 50 AS sl, 80000 AS gia, 'L-KHAN' AS k UNION ALL
  SELECT @h_dg, 200, 3800, 'L-DG' UNION ALL
  SELECT @h_gvs, 30, 5500, 'L-GVS' UNION ALL
  SELECT @h_ms, 3, 320000, 'L-MS'
) v WHERE NOT EXISTS (SELECT 1 FROM chi_tiet_phieu_nhap c WHERE c.phieu_nhap_id = @pn2 AND c.ghi_chu = v.k);
-- VAT 10%
UPDATE phieu_nhap p SET tong_tien_hang = (SELECT SUM(thanh_tien) FROM chi_tiet_phieu_nhap WHERE phieu_nhap_id = p.id),
                        tien_thue = ROUND((SELECT SUM(thanh_tien) FROM chi_tiet_phieu_nhap WHERE phieu_nhap_id = p.id) * 0.1, 0),
                        tong_thanh_toan = ROUND((SELECT SUM(thanh_tien) FROM chi_tiet_phieu_nhap WHERE phieu_nhap_id = p.id) * 1.1, 0)
WHERE p.id = @pn2;

-- Không theo lô -> sổ kho không có lo_hang_id
INSERT IGNORE INTO giao_dich_kho (ma_giao_dich, kho_id, danh_muc_hang_hoa_id, phieu_nhap_id, chi_tiet_phieu_nhap_id, nhan_vien_id, loai_giao_dich, so_luong_thay_doi, thoi_gian, ghi_chu)
SELECT CONCAT('DEMO-KHO-NK2-', c.ghi_chu), @kho, c.danh_muc_hang_hoa_id, @pn2, c.id, @nv, 'NHAP', c.so_luong, @t_pn2, 'Nhập PN02'
FROM chi_tiet_phieu_nhap c WHERE c.phieu_nhap_id = @pn2;

-- 13. Tài sản cá thể: 3 máy sấy có serial (tạo khi xác nhận PN02)
SET @ct_ms = (SELECT id FROM chi_tiet_phieu_nhap WHERE phieu_nhap_id = @pn2 AND ghi_chu = 'L-MS');
INSERT IGNORE INTO tai_san_ca_the (ma_tai_san, danh_muc_hang_hoa_id, chi_tiet_phieu_nhap_id, kho_id, so_serial, ngay_mua, gia_mua, han_bao_hanh, tinh_trang, trang_thai_su_dung, ghi_chu, created_at, updated_at)
SELECT v.ma, @h_ms, @ct_ms, @kho, v.serial, DATE(@t_pn2), 320000, DATE(@t_pn2) + INTERVAL 12 MONTH, 'TOT', 'TRONG_KHO', 'DEMO-KHO', @t_pn2, @t_pn2
FROM (SELECT 'DEMO-KHO-TS01' AS ma, 'MS-PH2026-0001' AS serial UNION ALL
      SELECT 'DEMO-KHO-TS02', 'MS-PH2026-0002' UNION ALL
      SELECT 'DEMO-KHO-TS03', 'MS-PH2026-0003') v;

-- Chuyển 2 máy sấy vào phòng 101 và 103 (giống API assets/{id}:transfer: XUAT -1 khỏi kho)
SET @p101 = (SELECT id FROM phong WHERE so_phong = '101');
SET @p103 = (SELECT id FROM phong WHERE so_phong = '103');
SET @t_mv = UTC_TIMESTAMP() - INTERVAL 18 DAY;
INSERT IGNORE INTO giao_dich_kho (ma_giao_dich, kho_id, danh_muc_hang_hoa_id, nhan_vien_id, loai_giao_dich, so_luong_thay_doi, thoi_gian, tham_chieu, ghi_chu)
SELECT CONCAT('DEMO-KHO-TSX-', t.ma_tai_san), @kho, @h_ms, @nv, 'XUAT', -1, @t_mv, CONCAT('assets/', t.id), 'Lắp máy sấy cho phòng'
FROM tai_san_ca_the t WHERE t.ma_tai_san IN ('DEMO-KHO-TS01', 'DEMO-KHO-TS02') AND t.kho_id IS NOT NULL;
UPDATE tai_san_ca_the SET kho_id = NULL, phong_id = @p101, trang_thai_su_dung = 'DANG_SU_DUNG', ghi_chu = 'Lắp máy sấy cho phòng', updated_at = @t_mv
WHERE ma_tai_san = 'DEMO-KHO-TS01' AND kho_id IS NOT NULL;
UPDATE tai_san_ca_the SET kho_id = NULL, phong_id = @p103, trang_thai_su_dung = 'DANG_SU_DUNG', ghi_chu = 'Lắp máy sấy cho phòng', updated_at = @t_mv
WHERE ma_tai_san = 'DEMO-KHO-TS02' AND kho_id IS NOT NULL;

-- 14. Máy sấy phòng 103 hỏng -> bảo trì (đang ở phòng nên không ghi sổ kho, giống API :condition)
UPDATE tai_san_ca_the SET tinh_trang = 'BAO_TRI', trang_thai_su_dung = 'BAO_TRI', ghi_chu = 'Máy kêu to, chờ kỹ thuật kiểm tra'
WHERE ma_tai_san = 'DEMO-KHO-TS02' AND tinh_trang = 'TOT';

-- 6. PN03 NHÁP (chưa xác nhận) -> để test Sửa / Xác nhận nhập kho ------------------
INSERT INTO phieu_nhap (ma_phieu_nhap, nha_cung_cap_id, kho_id, nhan_vien_tao_id, ngay_nhap, tong_tien_hang, chiet_khau, tien_thue, tong_thanh_toan, trang_thai, ghi_chu)
SELECT 'DEMO-KHO-PN03', @ncc_tp, @kho, @nv, UTC_TIMESTAMP(), 0, 0, 0, 0, 'NHAP', 'Nháp: cà phê + bàn chải + tivi (thử xác nhận trên app)'
WHERE NOT EXISTS (SELECT 1 FROM phieu_nhap WHERE ma_phieu_nhap = 'DEMO-KHO-PN03');
SET @pn3 = (SELECT id FROM phieu_nhap WHERE ma_phieu_nhap = 'DEMO-KHO-PN03');
INSERT INTO chi_tiet_phieu_nhap (phieu_nhap_id, danh_muc_hang_hoa_id, so_luong, don_gia_nhap, thanh_tien)
SELECT @pn3, v.h, v.sl, v.gia, v.sl * v.gia FROM (
  SELECT @h_cf AS h, 40 AS sl, 2800 AS gia UNION ALL
  SELECT @h_bc, 100, 2200 UNION ALL
  SELECT @h_tv, 2, 6200000
) v WHERE NOT EXISTS (SELECT 1 FROM chi_tiet_phieu_nhap c WHERE c.phieu_nhap_id = @pn3);
UPDATE phieu_nhap p SET tong_tien_hang = (SELECT SUM(thanh_tien) FROM chi_tiet_phieu_nhap WHERE phieu_nhap_id = p.id),
                        tong_thanh_toan = (SELECT SUM(thanh_tien) FROM chi_tiet_phieu_nhap WHERE phieu_nhap_id = p.id) - p.chiet_khau + p.tien_thue
WHERE p.id = @pn3;

-- 7. PN04 ĐÃ HỦY ---------------------------------------------------------------
INSERT INTO phieu_nhap (ma_phieu_nhap, nha_cung_cap_id, kho_id, nhan_vien_tao_id, ngay_nhap, tong_tien_hang, chiet_khau, tien_thue, tong_thanh_toan, trang_thai, ghi_chu)
SELECT 'DEMO-KHO-PN04', @ncc_vattu, @kho, @nv, UTC_TIMESTAMP() - INTERVAL 10 DAY, 0, 0, 0, 0, 'DA_HUY', 'Hủy: NCC giao sai mẫu khăn'
WHERE NOT EXISTS (SELECT 1 FROM phieu_nhap WHERE ma_phieu_nhap = 'DEMO-KHO-PN04');
SET @pn4 = (SELECT id FROM phieu_nhap WHERE ma_phieu_nhap = 'DEMO-KHO-PN04');
INSERT INTO chi_tiet_phieu_nhap (phieu_nhap_id, danh_muc_hang_hoa_id, so_luong, don_gia_nhap, thanh_tien)
SELECT @pn4, @h_khan, 20, 80000, 1600000 WHERE NOT EXISTS (SELECT 1 FROM chi_tiet_phieu_nhap WHERE phieu_nhap_id = @pn4);
UPDATE phieu_nhap SET tong_tien_hang = 1600000, tong_thanh_toan = 1600000 WHERE id = @pn4;

-- 8. PX01 đã xác nhận: xuất cho buồng phòng --------------------------------------
SET @t_px1 = UTC_TIMESTAMP() - INTERVAL 15 DAY;
INSERT INTO phieu_xuat (ma_phieu_xuat, kho_id, nhan_vien_tao_id, nhan_vien_xac_nhan_id, nguoi_nhan, muc_dich, ly_do, ngay_xuat, xac_nhan_luc, trang_thai, created_at, updated_at)
SELECT 'DEMO-KHO-PX01', @kho, @nv, @nv, 'Tổ buồng phòng', 'BUONG_PHONG', 'Cấp vật tư tuần 1', @t_px1, @t_px1, 'DA_XAC_NHAN', @t_px1, @t_px1
WHERE NOT EXISTS (SELECT 1 FROM phieu_xuat WHERE ma_phieu_xuat = 'DEMO-KHO-PX01');
SET @px1 = (SELECT id FROM phieu_xuat WHERE ma_phieu_xuat = 'DEMO-KHO-PX01');
INSERT INTO chi_tiet_phieu_xuat (phieu_xuat_id, danh_muc_hang_hoa_id, so_luong, ghi_chu)
SELECT @px1, v.h, v.sl, v.k FROM (SELECT @h_khan AS h, 10 AS sl, 'X-KHAN' AS k UNION ALL SELECT @h_dg, 40, 'X-DG') v
WHERE NOT EXISTS (SELECT 1 FROM chi_tiet_phieu_xuat c WHERE c.phieu_xuat_id = @px1 AND c.ghi_chu = v.k);
INSERT IGNORE INTO giao_dich_kho (ma_giao_dich, kho_id, danh_muc_hang_hoa_id, phieu_xuat_id, chi_tiet_phieu_xuat_id, nhan_vien_id, loai_giao_dich, so_luong_thay_doi, thoi_gian, ghi_chu)
SELECT CONCAT('DEMO-KHO-XK1-', c.ghi_chu), @kho, c.danh_muc_hang_hoa_id, @px1, c.id, @nv, 'XUAT', -c.so_luong, @t_px1, 'Xuất PX01'
FROM chi_tiet_phieu_xuat c WHERE c.phieu_xuat_id = @px1;

-- 9. PX02 đã xác nhận: xuất minibar 30 chai nước theo FEFO (lấy từ lô NS-A hết hạn sớm nhất) ----
SET @t_px2 = UTC_TIMESTAMP() - INTERVAL 7 DAY;
INSERT INTO phieu_xuat (ma_phieu_xuat, kho_id, nhan_vien_tao_id, nhan_vien_xac_nhan_id, nguoi_nhan, muc_dich, ly_do, ngay_xuat, xac_nhan_luc, trang_thai, created_at, updated_at)
SELECT 'DEMO-KHO-PX02', @kho, @nv, @nv, 'Nhân viên minibar', 'MINIBAR', 'Bổ sung minibar tầng 1-2', @t_px2, @t_px2, 'DA_XAC_NHAN', @t_px2, @t_px2
WHERE NOT EXISTS (SELECT 1 FROM phieu_xuat WHERE ma_phieu_xuat = 'DEMO-KHO-PX02');
SET @px2 = (SELECT id FROM phieu_xuat WHERE ma_phieu_xuat = 'DEMO-KHO-PX02');
INSERT INTO chi_tiet_phieu_xuat (phieu_xuat_id, danh_muc_hang_hoa_id, so_luong, ghi_chu)
SELECT @px2, @h_ns, 30, 'X-NS' WHERE NOT EXISTS (SELECT 1 FROM chi_tiet_phieu_xuat WHERE phieu_xuat_id = @px2);
SET @ct_px2 = (SELECT id FROM chi_tiet_phieu_xuat WHERE phieu_xuat_id = @px2 AND ghi_chu = 'X-NS');
INSERT IGNORE INTO giao_dich_kho (ma_giao_dich, kho_id, danh_muc_hang_hoa_id, lo_hang_id, phieu_xuat_id, chi_tiet_phieu_xuat_id, nhan_vien_id, loai_giao_dich, so_luong_thay_doi, thoi_gian, ghi_chu)
SELECT CONCAT('DEMO-KHO-XK2-', l.ma_lo), @kho, @h_ns, l.id, @px2, @ct_px2, @nv, 'XUAT', v.sl, @t_px2, 'Xuất PX02 (FEFO)'
FROM lo_hang l JOIN (SELECT 'NS-A' AS ma, -30 AS sl) v ON v.ma = l.ma_lo
WHERE l.kho_id = @kho AND l.danh_muc_hang_hoa_id = @h_ns;

-- 10. PX03 NHÁP: xuất minibar 12 lon bia -> test "Xác nhận xuất kho" (FEFO phải bỏ qua BL-OLD đã hết hạn)
INSERT INTO phieu_xuat (ma_phieu_xuat, kho_id, nhan_vien_tao_id, nguoi_nhan, muc_dich, ly_do, ngay_xuat, trang_thai)
SELECT 'DEMO-KHO-PX03', @kho, @nv, 'Nhân viên minibar', 'MINIBAR', 'Nháp: bổ sung bia tầng 3', UTC_TIMESTAMP(), 'NHAP'
WHERE NOT EXISTS (SELECT 1 FROM phieu_xuat WHERE ma_phieu_xuat = 'DEMO-KHO-PX03');
SET @px3 = (SELECT id FROM phieu_xuat WHERE ma_phieu_xuat = 'DEMO-KHO-PX03');
INSERT INTO chi_tiet_phieu_xuat (phieu_xuat_id, danh_muc_hang_hoa_id, so_luong)
SELECT @px3, @h_bia, 12 WHERE NOT EXISTS (SELECT 1 FROM chi_tiet_phieu_xuat WHERE phieu_xuat_id = @px3);

-- 11 + 12. Điều chỉnh tồn (giống API stock-adjustments) ---------------------------
INSERT IGNORE INTO giao_dich_kho (ma_giao_dich, kho_id, danh_muc_hang_hoa_id, nhan_vien_id, loai_giao_dich, so_luong_thay_doi, thoi_gian, ghi_chu) VALUES
  ('DEMO-KHO-DC01', @kho, @h_khan, @nv, 'HONG_MAT', -2, UTC_TIMESTAMP() - INTERVAL 5 DAY, 'Khăn rách khi giặt'),
  ('DEMO-KHO-DC02', @kho, @h_dg,   @nv, 'DIEU_CHINH', 5, UTC_TIMESTAMP() - INTERVAL 2 DAY, 'Kiểm kê cuối tuần: dư 5 chai');

COMMIT;

-- Kiểm tra nhanh tồn kho sau khi chạy
SELECT h.ma_hang, h.ten_hang, h.muc_ton_toi_thieu AS toi_thieu, COALESCE(SUM(g.so_luong_thay_doi), 0) AS ton
FROM danh_muc_hang_hoa h LEFT JOIN giao_dich_kho g ON g.danh_muc_hang_hoa_id = h.id AND g.kho_id = (SELECT id FROM kho WHERE ma_kho = 'KHO_CHINH')
GROUP BY h.id, h.ma_hang, h.ten_hang, h.muc_ton_toi_thieu ORDER BY h.id;
