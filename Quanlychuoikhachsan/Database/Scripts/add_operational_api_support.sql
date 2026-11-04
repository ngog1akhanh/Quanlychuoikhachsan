-- Chạy sau schema chính và add_vietqr_payment_order.sql. Không sửa dữ liệu kho hiện hữu.
-- Bảng riêng cho đơn cọc: hóa đơn phòng chỉ được lập khi khách đến.
CREATE TABLE IF NOT EXISTS vietqr_deposit_order (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  order_id VARCHAR(80) NOT NULL,
  booking_id BIGINT NOT NULL,
  account_id BIGINT NOT NULL,
  amount DECIMAL(15,2) NOT NULL,
  description VARCHAR(100) NOT NULL,
  qr_code LONGTEXT NOT NULL,
  qr_data_url LONGTEXT NOT NULL,
  status VARCHAR(30) NOT NULL DEFAULT 'PENDING',
  transaction_id VARCHAR(150) NULL,
  created_at DATETIME NOT NULL,
  paid_at DATETIME NULL,
  UNIQUE KEY uq_deposit_order (order_id),
  UNIQUE KEY uq_deposit_booking (booking_id),
  UNIQUE KEY uq_deposit_transaction (transaction_id),
  CONSTRAINT fk_deposit_booking FOREIGN KEY (booking_id) REFERENCES dat_phong(id) ON DELETE RESTRICT,
  CONSTRAINT fk_deposit_account FOREIGN KEY (account_id) REFERENCES tai_khoan(id) ON DELETE RESTRICT,
  CONSTRAINT ck_deposit_amount CHECK (amount > 0)
) ENGINE=InnoDB;

-- ============================================================
-- RÀNG BUỘC / VIEW BỔ SUNG (trước đây là Database/Install/compatible_constraints.sql)
-- Dành cho DATABASE MỚI. Không chạy trên dữ liệu cũ khi chưa đối chiếu và xử lý vi phạm.
-- ============================================================
-- byte/byte? của model tương ứng với TINYINT UNSIGNED của Pomelo.
ALTER TABLE chinh_sach_gia_thu MODIFY thu TINYINT UNSIGNED NOT NULL;
ALTER TABLE danh_gia
  MODIFY diem_tong TINYINT UNSIGNED NOT NULL,
  MODIFY diem_phong TINYINT UNSIGNED NULL,
  MODIFY diem_dich_vu TINYINT UNSIGNED NULL,
  ADD CONSTRAINT ck_dg_room_score CHECK (diem_phong IS NULL OR diem_phong BETWEEN 1 AND 5),
  ADD CONSTRAINT ck_dg_service_score CHECK (diem_dich_vu IS NULL OR diem_dich_vu BETWEEN 1 AND 5);

-- FEFO: tìm lô theo kho, mặt hàng, HSD; giữ các index tra cứu khác của schema gốc.
CREATE INDEX idx_lo_fefo ON lo_hang (kho_id, danh_muc_hang_hoa_id, han_su_dung, id);

-- Sổ kho chỉ được tham chiếu lô thuộc đúng kho và đúng mặt hàng.
ALTER TABLE lo_hang
  ADD UNIQUE KEY uq_lo_identity (id, kho_id, danh_muc_hang_hoa_id);
ALTER TABLE giao_dich_kho
  ADD CONSTRAINT fk_gdk_lo_identity FOREIGN KEY (lo_hang_id, kho_id, danh_muc_hang_hoa_id)
    REFERENCES lo_hang (id, kho_id, danh_muc_hang_hoa_id) ON DELETE RESTRICT ON UPDATE RESTRICT;

-- Các điều kiện đã có trong model/API cũng phải được bảo vệ khi nhập SQL trực tiếp.
ALTER TABLE chi_tiet_hoa_don_phong
  ADD CONSTRAINT ck_cthdp_values CHECK (so_luong > 0 AND don_gia >= 0 AND thanh_tien >= 0);
ALTER TABLE ap_dung_coc_hoa_don
  ADD CONSTRAINT ck_adchd_amount CHECK (so_tien_ap_dung > 0);

-- Seed danh mục phương thức dùng cho lễ tân; không có giao dịch hoặc tài khoản mẫu.
INSERT INTO phuong_thuc_thanh_toan (ma_phuong_thuc, ten_phuong_thuc, trang_thai) VALUES
  ('TIEN_MAT', 'Tiền mặt', 'HOAT_DONG'),
  ('CHUYEN_KHOAN', 'Chuyển khoản', 'HOAT_DONG')
ON DUPLICATE KEY UPDATE ten_phuong_thuc = VALUES(ten_phuong_thuc);

-- Ngày HSD theo lịch Việt Nam, độc lập với múi giờ session và bảng timezone của server.
CREATE OR REPLACE VIEW vw_hang_sap_het_han AS
SELECT lh.id AS lo_hang_id, lh.ma_lo, h.ma_hang, h.ten_hang, k.ma_kho,
       lh.han_su_dung,
       DATEDIFF(lh.han_su_dung, DATE(UTC_TIMESTAMP() + INTERVAL 7 HOUR)) AS so_ngay_con_lai,
       tk.so_luong_con
FROM lo_hang lh
JOIN danh_muc_hang_hoa h ON h.id = lh.danh_muc_hang_hoa_id
JOIN kho k ON k.id = lh.kho_id
JOIN vw_ton_kho_theo_lo tk ON tk.lo_hang_id = lh.id
WHERE lh.han_su_dung IS NOT NULL AND tk.so_luong_con > 0
  AND DATEDIFF(lh.han_su_dung, DATE(UTC_TIMESTAMP() + INTERVAL 7 HOUR)) <= 7;

-- Công nợ chỉ tính hóa đơn đã phát hành, cùng quy tắc báo cáo của API.
CREATE OR REPLACE VIEW vw_cong_no_hoa_don_phong AS
SELECT hd.id hoa_don_phong_id, hd.ma_hoa_don, hd.dat_phong_id, hd.tong_tien,
       COALESCE(c.tien_coc_ap_dung,0) tien_coc_ap_dung,
       COALESCE(t.tien_da_thanh_toan,0) tien_da_thanh_toan,
       GREATEST(hd.tong_tien-COALESCE(c.tien_coc_ap_dung,0)-COALESCE(t.tien_da_thanh_toan,0),0) con_phai_thu
FROM hoa_don_phong hd
LEFT JOIN (SELECT hoa_don_phong_id,SUM(so_tien_ap_dung) tien_coc_ap_dung FROM ap_dung_coc_hoa_don GROUP BY hoa_don_phong_id) c ON c.hoa_don_phong_id=hd.id
LEFT JOIN (SELECT hoa_don_phong_id,SUM(so_tien) tien_da_thanh_toan FROM thanh_toan_hoa_don_phong WHERE trang_thai='THANH_CONG' GROUP BY hoa_don_phong_id) t ON t.hoa_don_phong_id=hd.id
WHERE hd.trang_thai NOT IN ('NHAP','DA_HUY');

CREATE OR REPLACE VIEW vw_cong_no_hoa_don_dich_vu AS
SELECT hd.id hoa_don_dich_vu_id, hd.ma_hoa_don, hd.dat_phong_id, hd.tong_tien,
       COALESCE(t.tien_da_thanh_toan,0) tien_da_thanh_toan,
       GREATEST(hd.tong_tien-COALESCE(t.tien_da_thanh_toan,0),0) con_phai_thu
FROM hoa_don_dich_vu hd
LEFT JOIN (SELECT hoa_don_dich_vu_id,SUM(so_tien) tien_da_thanh_toan FROM thanh_toan_dich_vu WHERE trang_thai='THANH_CONG' GROUP BY hoa_don_dich_vu_id) t ON t.hoa_don_dich_vu_id=hd.id
WHERE hd.trang_thai NOT IN ('NHAP','DA_HUY');
