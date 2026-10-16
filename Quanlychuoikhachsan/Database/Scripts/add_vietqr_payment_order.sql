-- Chạy sau hotel_management_final_food_inventory.sql trên MySQL 8.x / MariaDB 10.4+.
-- Tạo bảng lưu đơn QR, khóa duy nhất chống ghi nhận trùng và quan hệ với hóa đơn/tài khoản.
CREATE TABLE IF NOT EXISTS vietqr_payment_order (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  order_id VARCHAR(80) NOT NULL,
  hoa_don_phong_id BIGINT NOT NULL,
  so_tien DECIMAL(15,2) NOT NULL,
  noi_dung VARCHAR(100) NOT NULL,
  qr_code LONGTEXT NOT NULL,
  qr_data_url LONGTEXT NOT NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'PENDING',
  ma_giao_dich VARCHAR(150) NULL,
  transaction_date_time VARCHAR(40) NULL,
  ghi_chu VARCHAR(1000) NULL,
  tai_khoan_tao_id BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  paid_at DATETIME NULL,
  UNIQUE KEY uq_vietqr_order_id (order_id),
  UNIQUE KEY uq_vietqr_transaction_id (ma_giao_dich),
  KEY idx_vietqr_invoice_status (hoa_don_phong_id, trang_thai),
  CONSTRAINT ck_vietqr_payment_order_amount CHECK (so_tien > 0),
  CONSTRAINT fk_vietqr_payment_order_invoice FOREIGN KEY (hoa_don_phong_id)
	REFERENCES hoa_don_phong(id) ON DELETE RESTRICT,
  CONSTRAINT fk_vietqr_payment_order_account FOREIGN KEY (tai_khoan_tao_id)
	REFERENCES tai_khoan(id) ON DELETE SET NULL
) ENGINE=InnoDB;

INSERT INTO phuong_thuc_thanh_toan (ma_phuong_thuc, ten_phuong_thuc, trang_thai)
VALUES ('VIETQR', 'VietQR', 'HOAT_DONG')
ON DUPLICATE KEY UPDATE ten_phuong_thuc = VALUES(ten_phuong_thuc);

-- Khai báo quyền VietQR trước khi liên kết các quyền theo vai trò.
INSERT INTO quyen (ma_quyen, ten_quyen, nhom_quyen, mo_ta)
VALUES
  ('VIETQR_PAYMENT_READ', 'Xem yêu cầu thanh toán VietQR', 'PAYMENT', 'Xem danh sách yêu cầu thanh toán VietQR.'),
  ('VIETQR_PAYMENT_NOTE_WRITE', 'Cập nhật ghi chú VietQR', 'PAYMENT', 'Cập nhật ghi chú của yêu cầu VietQR.'),
  ('VIETQR_PAYMENT_STATUS_WRITE', 'Cập nhật trạng thái VietQR', 'PAYMENT', 'Quản lý trạng thái không được xác nhận PAID thủ công.')
ON DUPLICATE KEY UPDATE ten_quyen = VALUES(ten_quyen), nhom_quyen = VALUES(nhom_quyen), mo_ta = VALUES(mo_ta);

-- ADMIN được quản lý trạng thái; MANAGER chỉ đọc và sửa ghi chú.
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT roles.id, permissions.id
FROM vai_tro roles
JOIN quyen permissions ON permissions.ma_quyen IN (
  'VIETQR_PAYMENT_READ', 'VIETQR_PAYMENT_NOTE_WRITE', 'VIETQR_PAYMENT_STATUS_WRITE'
)
WHERE roles.ma_vai_tro = 'ADMIN';

INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT roles.id, permissions.id
FROM vai_tro roles
JOIN quyen permissions ON permissions.ma_quyen IN (
  'VIETQR_PAYMENT_READ', 'VIETQR_PAYMENT_NOTE_WRITE'
)
WHERE roles.ma_vai_tro = 'MANAGER';
