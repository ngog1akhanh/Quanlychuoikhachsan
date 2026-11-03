-- Chọn database trước khi chạy. Chạy một lần khi nâng cấp database cũ.
-- Kiểm tra các cột contact_* trong information_schema trước khi chạy lại.
ALTER TABLE dat_phong
  ADD COLUMN contact_name VARCHAR(120) NULL,
  ADD COLUMN contact_phone VARCHAR(20) NULL,
  ADD COLUMN contact_email VARCHAR(150) NULL;
