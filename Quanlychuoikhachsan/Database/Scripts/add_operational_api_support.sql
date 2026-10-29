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
