-- Tùy chọn: chỉ dùng database demo đã chạy seed_demo.sql. Có thể chạy lặp.
SET time_zone = '+00:00';
START TRANSACTION;
SET @ops_employee = (SELECT n.id FROM nhan_vien n JOIN tai_khoan t ON t.id=n.tai_khoan_id WHERE t.email='admin.com' LIMIT 1);
SET @ops_assignee = (SELECT n.id FROM nhan_vien n JOIN tai_khoan t ON t.id=n.tai_khoan_id WHERE t.email='buongphong@hotel.com' LIMIT 1);
SET @ops_warehouse = (SELECT id FROM kho WHERE trang_thai='HOAT_DONG' ORDER BY id LIMIT 1);
SET @ops_supplier = (SELECT id FROM nha_cung_cap WHERE trang_thai='HOAT_DONG' ORDER BY id LIMIT 1);
INSERT INTO cong_viec_buong_phong (ma_cong_viec,phong_id,loai_cong_viec,nhan_vien_duoc_giao_id,nhan_vien_phan_cong_id,muc_do_uu_tien,trang_thai,ghi_chu)
SELECT CONCAT('DEMO-OPS-HK-',p.id),p.id,'KIEM_TRA',@ops_assignee,@ops_employee,'BINH_THUONG','CHO_XU_LY','Công việc kiểm tra demo'
FROM phong p WHERE p.id IN (SELECT id FROM (SELECT id FROM phong ORDER BY id LIMIT 3) rooms)
AND NOT EXISTS (SELECT 1 FROM cong_viec_buong_phong c WHERE c.ma_cong_viec=CONCAT('DEMO-OPS-HK-',p.id));
INSERT INTO danh_muc_hang_hoa (ma_hang,ten_hang,loai_hang,don_vi_tinh_id,gia_von_tham_chieu,muc_ton_toi_thieu,theo_doi_lo,theo_doi_han_su_dung,theo_doi_ca_the,trang_thai)
SELECT 'DEMO-OPS-VT','Vật tư thực hành nhập xuất','VAT_TU',d.id,10000,2,0,0,0,'HOAT_DONG'
FROM don_vi_tinh d WHERE NOT EXISTS (SELECT 1 FROM danh_muc_hang_hoa WHERE ma_hang='DEMO-OPS-VT') ORDER BY d.id LIMIT 1;
SET @ops_item = (SELECT id FROM danh_muc_hang_hoa WHERE ma_hang='DEMO-OPS-VT');
INSERT INTO phieu_nhap (ma_phieu_nhap,nha_cung_cap_id,kho_id,nhan_vien_tao_id,nhan_vien_xac_nhan_id,xac_nhan_luc,tong_tien_hang,tong_thanh_toan,trang_thai,ghi_chu)
SELECT 'DEMO-OPS-PN',@ops_supplier,@ops_warehouse,@ops_employee,@ops_employee,UTC_TIMESTAMP(),100000,100000,'DA_XAC_NHAN','Phiếu demo 10 vật tư'
WHERE NOT EXISTS (SELECT 1 FROM phieu_nhap WHERE ma_phieu_nhap='DEMO-OPS-PN');
SET @ops_receipt = (SELECT id FROM phieu_nhap WHERE ma_phieu_nhap='DEMO-OPS-PN');
INSERT INTO chi_tiet_phieu_nhap (phieu_nhap_id,danh_muc_hang_hoa_id,so_luong,don_gia_nhap,thanh_tien)
SELECT @ops_receipt,@ops_item,10,10000,100000 WHERE NOT EXISTS (SELECT 1 FROM chi_tiet_phieu_nhap WHERE phieu_nhap_id=@ops_receipt);
INSERT INTO giao_dich_kho (ma_giao_dich,kho_id,danh_muc_hang_hoa_id,phieu_nhap_id,chi_tiet_phieu_nhap_id,nhan_vien_id,loai_giao_dich,so_luong_thay_doi,thoi_gian)
SELECT 'DEMO-OPS-NK',@ops_warehouse,@ops_item,@ops_receipt,c.id,@ops_employee,'NHAP',10,UTC_TIMESTAMP()
FROM chi_tiet_phieu_nhap c WHERE c.phieu_nhap_id=@ops_receipt AND NOT EXISTS (SELECT 1 FROM giao_dich_kho WHERE ma_giao_dich='DEMO-OPS-NK');
COMMIT;
