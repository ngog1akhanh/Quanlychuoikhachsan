-- Chọn database cần chuyển mã trước khi chạy. Dừng API trong lúc chuyển đổi.
-- Chạy sau khi import toàn bộ seed demo; không chạy seed demo lại sau chuyển mã
-- vì các seed cũ dùng mã DEMO/TK để nhận diện bản ghi.
-- Lưu kết quả SELECT bên dưới để đối chiếu mã cũ khi cần.
SET @booking_previous_safe_updates = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;
START TRANSACTION;
SELECT id, ma_dat_phong AS ma_cu,
       CONCAT('DP-', LPAD(CAST(id AS CHAR), GREATEST(4, CHAR_LENGTH(CAST(id AS CHAR))), '0')) AS ma_moi
FROM dat_phong ORDER BY id FOR UPDATE;
-- Mã tạm tránh va chạm unique trong lúc đổi các mã hiện có.
UPDATE dat_phong SET ma_dat_phong = CONCAT('MIG', REPLACE(UUID(), '-', ''));
UPDATE dat_phong
SET ma_dat_phong = CONCAT('DP-', LPAD(CAST(id AS CHAR), GREATEST(4, CHAR_LENGTH(CAST(id AS CHAR))), '0'));
COMMIT;
SET SQL_SAFE_UPDATES = @booking_previous_safe_updates;
