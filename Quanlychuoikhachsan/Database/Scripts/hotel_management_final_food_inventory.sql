-- ============================================================
-- HOTEL MANAGEMENT DATABASE - BASED ON LATEST REQUIREMENTS
-- MySQL 8.x / MariaDB 10.4+
-- IMPORTANT: No promotion/coupon module. Seasonal/holiday/weekend
-- price changes are handled only by CHINH_SACH_GIA.
-- ============================================================

CREATE DATABASE IF NOT EXISTS hotel_management
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE hotel_management;
SET NAMES utf8mb4;
SET time_zone = '+07:00';

-- 1. AUTH / CUSTOMER / STAFF --------------------------------------------------
CREATE TABLE tai_khoan (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  email VARCHAR(150) NULL,
  so_dien_thoai VARCHAR(20) NULL,
  mat_khau_hash VARCHAR(255) NOT NULL,
  loai_tai_khoan VARCHAR(20) NOT NULL, -- KHACH_HANG/NHAN_VIEN
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'HOAT_DONG',
  email_da_xac_thuc TINYINT(1) NOT NULL DEFAULT 0,
  lan_dang_nhap_cuoi DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uq_tk_email(email),
  UNIQUE KEY uq_tk_sdt(so_dien_thoai),
  CHECK (email IS NOT NULL OR so_dien_thoai IS NOT NULL)
) ENGINE=InnoDB;

CREATE TABLE ma_xac_thuc (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  tai_khoan_id BIGINT NOT NULL,
  loai VARCHAR(30) NOT NULL, -- QUEN_MAT_KHAU/XAC_THUC_EMAIL
  ma_hash VARCHAR(255) NOT NULL,
  het_han_luc DATETIME NOT NULL,
  da_su_dung TINYINT(1) NOT NULL DEFAULT 0,
  su_dung_luc DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (tai_khoan_id) REFERENCES tai_khoan(id) ON DELETE CASCADE,
  INDEX idx_mxt_tk(tai_khoan_id, loai, het_han_luc)
) ENGINE=InnoDB;

CREATE TABLE vai_tro (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_vai_tro VARCHAR(50) NOT NULL UNIQUE,
  ten_vai_tro VARCHAR(100) NOT NULL,
  mo_ta VARCHAR(500) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG'
) ENGINE=InnoDB;

CREATE TABLE quyen (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_quyen VARCHAR(80) NOT NULL UNIQUE,
  ten_quyen VARCHAR(150) NOT NULL,
  nhom_quyen VARCHAR(80) NULL,
  mo_ta VARCHAR(500) NULL
) ENGINE=InnoDB;

CREATE TABLE tai_khoan_vai_tro (
  tai_khoan_id BIGINT NOT NULL,
  vai_tro_id BIGINT NOT NULL,
  PRIMARY KEY (tai_khoan_id, vai_tro_id),
  FOREIGN KEY (tai_khoan_id) REFERENCES tai_khoan(id) ON DELETE CASCADE,
  FOREIGN KEY (vai_tro_id) REFERENCES vai_tro(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE vai_tro_quyen (
  vai_tro_id BIGINT NOT NULL,
  quyen_id BIGINT NOT NULL,
  PRIMARY KEY (vai_tro_id, quyen_id),
  FOREIGN KEY (vai_tro_id) REFERENCES vai_tro(id) ON DELETE CASCADE,
  FOREIGN KEY (quyen_id) REFERENCES quyen(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE chuc_vu (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_chuc_vu VARCHAR(30) NOT NULL UNIQUE,
  ten_chuc_vu VARCHAR(100) NOT NULL,
  mo_ta VARCHAR(500) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG'
) ENGINE=InnoDB;

CREATE TABLE nhan_vien (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_nhan_vien VARCHAR(30) NOT NULL UNIQUE,
  tai_khoan_id BIGINT NOT NULL UNIQUE,
  chuc_vu_id BIGINT NOT NULL,
  ho_ten VARCHAR(120) NOT NULL,
  ngay_sinh DATE NULL,
  gioi_tinh VARCHAR(20) NULL,
  so_dien_thoai VARCHAR(20) NULL,
  email VARCHAR(150) NULL,
  cccd_ho_chieu VARCHAR(40) NULL,
  dia_chi VARCHAR(255) NULL,
  ngay_vao_lam DATE NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'DANG_LAM',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (tai_khoan_id) REFERENCES tai_khoan(id) ON DELETE RESTRICT,
  FOREIGN KEY (chuc_vu_id) REFERENCES chuc_vu(id) ON DELETE RESTRICT,
  INDEX idx_nv_cv(chuc_vu_id)
) ENGINE=InnoDB;

CREATE TABLE khach_hang (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_khach_hang VARCHAR(30) NOT NULL UNIQUE,
  tai_khoan_id BIGINT NULL UNIQUE, -- NULL = walk-in
  ho_ten VARCHAR(120) NOT NULL,
  ngay_sinh DATE NULL,
  gioi_tinh VARCHAR(20) NULL,
  so_dien_thoai VARCHAR(20) NULL,
  email VARCHAR(150) NULL,
  cccd_ho_chieu VARCHAR(40) NULL,
  dia_chi VARCHAR(255) NULL,
  ghi_chu VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  FOREIGN KEY (tai_khoan_id) REFERENCES tai_khoan(id) ON DELETE SET NULL,
  INDEX idx_kh_sdt(so_dien_thoai),
  INDEX idx_kh_email(email)
) ENGINE=InnoDB;

CREATE TABLE nhat_ky_he_thong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  tai_khoan_id BIGINT NULL,
  hanh_dong VARCHAR(100) NOT NULL,
  doi_tuong VARCHAR(100) NULL,
  doi_tuong_id VARCHAR(100) NULL,
  du_lieu_cu LONGTEXT NULL,
  du_lieu_moi LONGTEXT NULL,
  dia_chi_ip VARCHAR(64) NULL,
  user_agent VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (tai_khoan_id) REFERENCES tai_khoan(id) ON DELETE SET NULL,
  INDEX idx_nk_tk(tai_khoan_id),
  INDEX idx_nk_dt(doi_tuong, doi_tuong_id)
) ENGINE=InnoDB;

-- 2. HOTEL / ROOM / PRICE POLICIES ------------------------------------------
CREATE TABLE khach_san (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ten_khach_san VARCHAR(150) NOT NULL,
  dia_chi VARCHAR(255) NOT NULL,
  so_dien_thoai VARCHAR(30) NULL,
  email VARCHAR(150) NULL,
  website VARCHAR(255) NULL,
  gio_nhan_phong TIME NULL,
  gio_tra_phong TIME NULL,
  mo_ta TEXT NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG'
) ENGINE=InnoDB;

CREATE TABLE loai_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_loai_phong VARCHAR(30) NOT NULL UNIQUE,
  ten_loai_phong VARCHAR(100) NOT NULL,
  so_nguoi_lon_toi_da INT NOT NULL DEFAULT 2,
  so_tre_em_toi_da INT NOT NULL DEFAULT 0,
  dien_tich_m2 DECIMAL(8,2) NULL,
  gia_co_ban_dem DECIMAL(15,2) NOT NULL,
  mo_ta TEXT NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG',
  CHECK (gia_co_ban_dem >= 0)
) ENGINE=InnoDB;

CREATE TABLE phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_phong VARCHAR(30) NOT NULL UNIQUE,
  loai_phong_id BIGINT NOT NULL,
  so_phong VARCHAR(20) NOT NULL UNIQUE,
  tang VARCHAR(20) NULL,
  huong_phong VARCHAR(80) NULL,
  loai_view VARCHAR(100) NULL,
  vi_tri VARCHAR(150) NULL,
  phu_thu_dem_mac_dinh DECIMAL(15,2) NOT NULL DEFAULT 0,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'SAN_SANG', -- SAN_SANG/DANG_O/CAN_DON/DANG_DON/BAO_TRI/NGUNG_SU_DUNG
  mo_ta VARCHAR(1000) NULL,
  ghi_chu VARCHAR(500) NULL,
  FOREIGN KEY (loai_phong_id) REFERENCES loai_phong(id) ON DELETE RESTRICT,
  INDEX idx_phong_lp(loai_phong_id),
  INDEX idx_phong_tt(trang_thai),
  CHECK (phu_thu_dem_mac_dinh >= 0)
) ENGINE=InnoDB;

CREATE TABLE tien_nghi (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_tien_nghi VARCHAR(30) NOT NULL UNIQUE,
  ten_tien_nghi VARCHAR(120) NOT NULL,
  icon VARCHAR(255) NULL,
  mo_ta VARCHAR(500) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG'
) ENGINE=InnoDB;

CREATE TABLE loai_phong_tien_nghi (
  loai_phong_id BIGINT NOT NULL,
  tien_nghi_id BIGINT NOT NULL,
  PRIMARY KEY(loai_phong_id, tien_nghi_id),
  FOREIGN KEY (loai_phong_id) REFERENCES loai_phong(id) ON DELETE CASCADE,
  FOREIGN KEY (tien_nghi_id) REFERENCES tien_nghi(id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE hinh_anh_loai_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  loai_phong_id BIGINT NOT NULL,
  url VARCHAR(1000) NOT NULL,
  alt_text VARCHAR(255) NULL,
  la_anh_dai_dien TINYINT(1) NOT NULL DEFAULT 0,
  thu_tu INT NOT NULL DEFAULT 0,
  FOREIGN KEY (loai_phong_id) REFERENCES loai_phong(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE hinh_anh_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  phong_id BIGINT NOT NULL,
  url VARCHAR(1000) NOT NULL,
  alt_text VARCHAR(255) NULL,
  la_anh_dai_dien TINYINT(1) NOT NULL DEFAULT 0,
  thu_tu INT NOT NULL DEFAULT 0,
  FOREIGN KEY (phong_id) REFERENCES phong(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE chinh_sach_gia (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_chinh_sach VARCHAR(40) NOT NULL UNIQUE,
  ten_chinh_sach VARCHAR(150) NOT NULL,
  tu_ngay DATE NOT NULL,
  den_ngay DATE NOT NULL,
  muc_uu_tien INT NOT NULL DEFAULT 0,
  mo_ta VARCHAR(1000) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG',
  CHECK (den_ngay >= tu_ngay)
) ENGINE=InnoDB;

CREATE TABLE chinh_sach_gia_thu (
  chinh_sach_gia_id BIGINT NOT NULL,
  thu TINYINT NOT NULL, -- 1=Mon..7=Sun
  PRIMARY KEY(chinh_sach_gia_id, thu),
  FOREIGN KEY (chinh_sach_gia_id) REFERENCES chinh_sach_gia(id) ON DELETE CASCADE,
  CHECK (thu BETWEEN 1 AND 7)
) ENGINE=InnoDB;

CREATE TABLE chi_tiet_chinh_sach_gia (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  chinh_sach_gia_id BIGINT NOT NULL,
  loai_phong_id BIGINT NOT NULL,
  kieu_dieu_chinh VARCHAR(30) NOT NULL, -- GIA_CO_DINH/TANG_%/GIAM_%/TANG_TIEN/GIAM_TIEN
  gia_tri DECIMAL(15,2) NOT NULL,
  FOREIGN KEY (chinh_sach_gia_id) REFERENCES chinh_sach_gia(id) ON DELETE CASCADE,
  FOREIGN KEY (loai_phong_id) REFERENCES loai_phong(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_ctcsg(chinh_sach_gia_id, loai_phong_id),
  CHECK (gia_tri >= 0)
) ENGINE=InnoDB;

CREATE TABLE chinh_sach_coc (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ten_chinh_sach VARCHAR(150) NOT NULL,
  kieu_tinh VARCHAR(30) NOT NULL, -- PHAN_TRAM/DEM_DAU/SO_TIEN_CO_DINH
  gia_tri DECIMAL(15,2) NOT NULL,
  tu_ngay DATE NULL,
  den_ngay DATE NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG',
  CHECK (gia_tri >= 0)
) ENGINE=InnoDB;

CREATE TABLE chinh_sach_huy (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ten_chinh_sach VARCHAR(150) NOT NULL,
  mo_ta VARCHAR(1000) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG'
) ENGINE=InnoDB;

CREATE TABLE chi_tiet_chinh_sach_huy (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  chinh_sach_huy_id BIGINT NOT NULL,
  so_gio_truoc_toi_thieu INT NOT NULL,
  phan_tram_hoan DECIMAL(5,2) NOT NULL DEFAULT 0,
  phi_huy_co_dinh DECIMAL(15,2) NOT NULL DEFAULT 0,
  FOREIGN KEY (chinh_sach_huy_id) REFERENCES chinh_sach_huy(id) ON DELETE CASCADE,
  CHECK (so_gio_truoc_toi_thieu >= 0 AND phan_tram_hoan BETWEEN 0 AND 100 AND phi_huy_co_dinh >= 0)
) ENGINE=InnoDB;

CREATE TABLE chinh_sach_phu_thu (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  loai VARCHAR(30) NOT NULL, -- NHAN_SOM/TRA_TRE
  ten_chinh_sach VARCHAR(150) NOT NULL,
  tu_phut INT NOT NULL DEFAULT 0,
  den_phut INT NULL,
  kieu_tinh VARCHAR(30) NOT NULL, -- PHAN_TRAM/SO_TIEN_CO_DINH
  gia_tri DECIMAL(15,2) NOT NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG',
  CHECK (gia_tri >= 0)
) ENGINE=InnoDB;

-- 3. BOOKING / STAY -----------------------------------------------------------
CREATE TABLE dat_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_dat_phong VARCHAR(40) NOT NULL UNIQUE,
  khach_hang_id BIGINT NOT NULL,
  tai_khoan_tao_id BIGINT NULL,
  chinh_sach_coc_id BIGINT NULL,
  chinh_sach_huy_id BIGINT NULL,
  ngay_dat DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  du_kien_nhan DATETIME NOT NULL,
  du_kien_tra DATETIME NOT NULL,
  so_nguoi_lon INT NOT NULL DEFAULT 1,
  so_tre_em INT NOT NULL DEFAULT 0,
  kenh_dat VARCHAR(30) NOT NULL DEFAULT 'WEB', -- WEB/QUAY/DIEN_THOAI
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'CHO_COC',
  yeu_cau_dac_biet VARCHAR(1000) NULL,
  tong_tien_phong_goc DECIMAL(15,2) NOT NULL DEFAULT 0,
  tong_tien_du_kien DECIMAL(15,2) NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  FOREIGN KEY (khach_hang_id) REFERENCES khach_hang(id) ON DELETE RESTRICT,
  FOREIGN KEY (tai_khoan_tao_id) REFERENCES tai_khoan(id) ON DELETE SET NULL,
  FOREIGN KEY (chinh_sach_coc_id) REFERENCES chinh_sach_coc(id) ON DELETE SET NULL,
  FOREIGN KEY (chinh_sach_huy_id) REFERENCES chinh_sach_huy(id) ON DELETE SET NULL,
  CHECK (du_kien_tra > du_kien_nhan),
  INDEX idx_dp_time(du_kien_nhan, du_kien_tra),
  INDEX idx_dp_tt(trang_thai)
) ENGINE=InnoDB;

CREATE TABLE chi_tiet_dat_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  dat_phong_id BIGINT NOT NULL,
  phong_id BIGINT NOT NULL,
  so_nguoi_lon INT NOT NULL DEFAULT 1,
  so_tre_em INT NOT NULL DEFAULT 0,
  tien_phong_du_kien DECIMAL(15,2) NOT NULL DEFAULT 0,
  nhan_phong_luc DATETIME NULL,
  tra_phong_luc DATETIME NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'CHO_COC',
  ghi_chu VARCHAR(500) NULL,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE CASCADE,
  FOREIGN KEY (phong_id) REFERENCES phong(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_booking_room(dat_phong_id, phong_id),
  INDEX idx_ctdp_phong(phong_id)
) ENGINE=InnoDB;

CREATE TABLE khoa_lich_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  chi_tiet_dat_phong_id BIGINT NOT NULL,
  phong_id BIGINT NOT NULL,
  ngay_luu_tru DATE NOT NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOLD', -- HOLD/BOOKED/OCCUPIED
  het_han_giu_cho DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (chi_tiet_dat_phong_id) REFERENCES chi_tiet_dat_phong(id) ON DELETE CASCADE,
  FOREIGN KEY (phong_id) REFERENCES phong(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_room_day(phong_id, ngay_luu_tru),
  UNIQUE KEY uq_detail_day(chi_tiet_dat_phong_id, ngay_luu_tru),
  INDEX idx_klp_date(ngay_luu_tru, trang_thai)
) ENGINE=InnoDB;

CREATE TABLE gia_dem_dat_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  chi_tiet_dat_phong_id BIGINT NOT NULL,
  ngay_luu_tru DATE NOT NULL,
  gia_co_ban DECIMAL(15,2) NOT NULL,
  phu_thu_phong DECIMAL(15,2) NOT NULL DEFAULT 0,
  chinh_sach_gia_id BIGINT NULL,
  tien_dieu_chinh DECIMAL(15,2) NOT NULL DEFAULT 0,
  gia_sau_dieu_chinh DECIMAL(15,2) NOT NULL,
  FOREIGN KEY (chi_tiet_dat_phong_id) REFERENCES chi_tiet_dat_phong(id) ON DELETE CASCADE,
  FOREIGN KEY (chinh_sach_gia_id) REFERENCES chinh_sach_gia(id) ON DELETE SET NULL,
  UNIQUE KEY uq_price_night(chi_tiet_dat_phong_id, ngay_luu_tru)
) ENGINE=InnoDB;

CREATE TABLE lich_su_trang_thai_dat_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  dat_phong_id BIGINT NOT NULL,
  trang_thai_cu VARCHAR(30) NULL,
  trang_thai_moi VARCHAR(30) NOT NULL,
  tai_khoan_thuc_hien_id BIGINT NULL,
  ly_do VARCHAR(1000) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE CASCADE,
  FOREIGN KEY (tai_khoan_thuc_hien_id) REFERENCES tai_khoan(id) ON DELETE SET NULL,
  INDEX idx_lstt_booking(dat_phong_id, created_at)
) ENGINE=InnoDB;

CREATE TABLE yeu_cau_thay_doi_dat_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  dat_phong_id BIGINT NOT NULL,
  khach_hang_id BIGINT NOT NULL,
  loai_yeu_cau VARCHAR(30) NOT NULL, -- DOI_NGAY/DOI_PHONG/GIA_HAN
  noi_dung VARCHAR(1500) NOT NULL,
  du_kien_nhan_moi DATETIME NULL,
  du_kien_tra_moi DATETIME NULL,
  phong_moi_id BIGINT NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'CHO_XU_LY',
  nhan_vien_xu_ly_id BIGINT NULL,
  ket_qua VARCHAR(1500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  xu_ly_luc DATETIME NULL,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (khach_hang_id) REFERENCES khach_hang(id) ON DELETE RESTRICT,
  FOREIGN KEY (phong_moi_id) REFERENCES phong(id) ON DELETE SET NULL,
  FOREIGN KEY (nhan_vien_xu_ly_id) REFERENCES nhan_vien(id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE lich_su_doi_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  dat_phong_id BIGINT NOT NULL,
  chi_tiet_dat_phong_id BIGINT NOT NULL,
  phong_cu_id BIGINT NOT NULL,
  phong_moi_id BIGINT NOT NULL,
  nhan_vien_id BIGINT NOT NULL,
  ly_do VARCHAR(1000) NULL,
  doi_luc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (chi_tiet_dat_phong_id) REFERENCES chi_tiet_dat_phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (phong_cu_id) REFERENCES phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (phong_moi_id) REFERENCES phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (nhan_vien_id) REFERENCES nhan_vien(id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE yeu_cau_huy_dat_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_yeu_cau VARCHAR(40) NOT NULL UNIQUE,
  dat_phong_id BIGINT NOT NULL,
  khach_hang_id BIGINT NOT NULL,
  ly_do VARCHAR(1500) NOT NULL,
  thoi_gian_yeu_cau DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  so_tien_hoan_du_kien DECIMAL(15,2) NOT NULL DEFAULT 0,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'CHO_DUYET',
  nhan_vien_xu_ly_id BIGINT NULL,
  thoi_gian_xu_ly DATETIME NULL,
  so_tien_hoan_duyet DECIMAL(15,2) NOT NULL DEFAULT 0,
  ghi_chu_xu_ly VARCHAR(1500) NULL,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (khach_hang_id) REFERENCES khach_hang(id) ON DELETE RESTRICT,
  FOREIGN KEY (nhan_vien_xu_ly_id) REFERENCES nhan_vien(id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE khach_luu_tru (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  chi_tiet_dat_phong_id BIGINT NOT NULL,
  ho_ten VARCHAR(120) NOT NULL,
  ngay_sinh DATE NULL,
  gioi_tinh VARCHAR(20) NULL,
  loai_giay_to VARCHAR(30) NULL,
  so_giay_to VARCHAR(50) NULL,
  quoc_tich VARCHAR(80) NULL,
  la_khach_dai_dien TINYINT(1) NOT NULL DEFAULT 0,
  FOREIGN KEY (chi_tiet_dat_phong_id) REFERENCES chi_tiet_dat_phong(id) ON DELETE CASCADE,
  INDEX idx_klt_ctdp(chi_tiet_dat_phong_id)
) ENGINE=InnoDB;

-- 4. PAYMENTS / INVOICES ------------------------------------------------------
CREATE TABLE phuong_thuc_thanh_toan (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_phuong_thuc VARCHAR(30) NOT NULL UNIQUE,
  ten_phuong_thuc VARCHAR(100) NOT NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG'
) ENGINE=InnoDB;

CREATE TABLE ca_lam_viec (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  nhan_vien_id BIGINT NOT NULL,
  bat_dau_luc DATETIME NOT NULL,
  ket_thuc_luc DATETIME NULL,
  tien_dau_ca DECIMAL(15,2) NOT NULL DEFAULT 0,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'DANG_MO',
  FOREIGN KEY (nhan_vien_id) REFERENCES nhan_vien(id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE phieu_coc (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_phieu_coc VARCHAR(40) NOT NULL UNIQUE,
  dat_phong_id BIGINT NOT NULL,
  tai_khoan_thanh_toan_id BIGINT NULL,
  nhan_vien_xac_nhan_id BIGINT NULL,
  phuong_thuc_id BIGINT NOT NULL,
  ca_lam_viec_id BIGINT NULL,
  so_tien DECIMAL(15,2) NOT NULL,
  cong_thanh_toan VARCHAR(80) NULL,
  ma_giao_dich VARCHAR(150) NULL,
  thoi_gian_coc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'CHO_XU_LY',
  ghi_chu VARCHAR(500) NULL,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (tai_khoan_thanh_toan_id) REFERENCES tai_khoan(id) ON DELETE SET NULL,
  FOREIGN KEY (nhan_vien_xac_nhan_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,
  FOREIGN KEY (phuong_thuc_id) REFERENCES phuong_thuc_thanh_toan(id) ON DELETE RESTRICT,
  FOREIGN KEY (ca_lam_viec_id) REFERENCES ca_lam_viec(id) ON DELETE SET NULL,
  UNIQUE KEY uq_pc_gateway(cong_thanh_toan, ma_giao_dich),
  CHECK (so_tien > 0)
) ENGINE=InnoDB;

CREATE TABLE hoa_don_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_hoa_don VARCHAR(40) NOT NULL UNIQUE,
  dat_phong_id BIGINT NOT NULL,
  hoa_don_thay_the_id BIGINT NULL,
  nhan_vien_lap_id BIGINT NULL,
  ngay_lap DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  tien_phong DECIMAL(15,2) NOT NULL DEFAULT 0,
  tien_phu_thu DECIMAL(15,2) NOT NULL DEFAULT 0,
  tien_boi_thuong DECIMAL(15,2) NOT NULL DEFAULT 0,
  tien_thue DECIMAL(15,2) NOT NULL DEFAULT 0,
  tong_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'NHAP', -- NHAP/DA_PHAT_HANH/TT_MOT_PHAN/DA_THANH_TOAN/DA_HUY
  ly_do_huy VARCHAR(1000) NULL,
  so_hoa_don_dien_tu VARCHAR(100) NULL,
  url_hoa_don_dien_tu VARCHAR(1000) NULL,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (hoa_don_thay_the_id) REFERENCES hoa_don_phong(id) ON DELETE SET NULL,
  FOREIGN KEY (nhan_vien_lap_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,
  INDEX idx_hdp_dp(dat_phong_id)
) ENGINE=InnoDB;

CREATE TABLE chi_tiet_hoa_don_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  hoa_don_phong_id BIGINT NOT NULL,
  chi_tiet_dat_phong_id BIGINT NULL,
  tinh_trang_phong_id BIGINT NULL, -- FK added later
  loai_dong VARCHAR(30) NOT NULL, -- TIEN_PHONG/PHU_THU/BOI_THUONG/THUE/KHAC
  noi_dung VARCHAR(255) NOT NULL,
  ngay_ap_dung DATE NULL,
  so_luong DECIMAL(12,2) NOT NULL DEFAULT 1,
  don_gia DECIMAL(15,2) NOT NULL,
  thanh_tien DECIMAL(15,2) NOT NULL,
  FOREIGN KEY (hoa_don_phong_id) REFERENCES hoa_don_phong(id) ON DELETE CASCADE,
  FOREIGN KEY (chi_tiet_dat_phong_id) REFERENCES chi_tiet_dat_phong(id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE ap_dung_coc_hoa_don (
  hoa_don_phong_id BIGINT NOT NULL,
  phieu_coc_id BIGINT NOT NULL,
  so_tien_ap_dung DECIMAL(15,2) NOT NULL,
  PRIMARY KEY (hoa_don_phong_id, phieu_coc_id),
  FOREIGN KEY (hoa_don_phong_id) REFERENCES hoa_don_phong(id) ON DELETE CASCADE,
  FOREIGN KEY (phieu_coc_id) REFERENCES phieu_coc(id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE thanh_toan_hoa_don_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_thanh_toan VARCHAR(40) NOT NULL UNIQUE,
  hoa_don_phong_id BIGINT NOT NULL,
  nhan_vien_thu_id BIGINT NULL,
  phuong_thuc_id BIGINT NOT NULL,
  ca_lam_viec_id BIGINT NULL,
  so_tien DECIMAL(15,2) NOT NULL,
  thoi_gian_thanh_toan DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  cong_thanh_toan VARCHAR(80) NULL,
  ma_giao_dich VARCHAR(150) NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'THANH_CONG',
  FOREIGN KEY (hoa_don_phong_id) REFERENCES hoa_don_phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (nhan_vien_thu_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,
  FOREIGN KEY (phuong_thuc_id) REFERENCES phuong_thuc_thanh_toan(id) ON DELETE RESTRICT,
  FOREIGN KEY (ca_lam_viec_id) REFERENCES ca_lam_viec(id) ON DELETE SET NULL,
  CHECK (so_tien > 0)
) ENGINE=InnoDB;

-- 5. SERVICES -----------------------------------------------------------------
CREATE TABLE dich_vu (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_dich_vu VARCHAR(30) NOT NULL UNIQUE,
  ten_dich_vu VARCHAR(120) NOT NULL,
  nhom_dich_vu VARCHAR(80) NULL,
  don_vi_tinh VARCHAR(30) NOT NULL,
  don_gia DECIMAL(15,2) NOT NULL,
  mo_ta VARCHAR(1000) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG'
) ENGINE=InnoDB;

CREATE TABLE su_dung_dich_vu (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_su_dung VARCHAR(40) NOT NULL UNIQUE,
  dat_phong_id BIGINT NOT NULL,
  phong_id BIGINT NULL,
  dich_vu_id BIGINT NOT NULL,
  tai_khoan_yeu_cau_id BIGINT NULL,
  thoi_gian_yeu_cau DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  thoi_gian_phuc_vu DATETIME NULL,
  so_luong DECIMAL(12,2) NOT NULL DEFAULT 1,
  don_gia DECIMAL(15,2) NOT NULL,
  thanh_tien DECIMAL(15,2) NOT NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'CHO_XU_LY',
  ghi_chu VARCHAR(500) NULL,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (phong_id) REFERENCES phong(id) ON DELETE SET NULL,
  FOREIGN KEY (dich_vu_id) REFERENCES dich_vu(id) ON DELETE RESTRICT,
  FOREIGN KEY (tai_khoan_yeu_cau_id) REFERENCES tai_khoan(id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE hoa_don_dich_vu (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_hoa_don VARCHAR(40) NOT NULL UNIQUE,
  dat_phong_id BIGINT NOT NULL,
  nhan_vien_lap_id BIGINT NULL,
  ngay_lap DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  tong_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'NHAP',
  so_hoa_don_dien_tu VARCHAR(100) NULL,
  url_hoa_don_dien_tu VARCHAR(1000) NULL,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (nhan_vien_lap_id) REFERENCES nhan_vien(id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE chi_tiet_hoa_don_dich_vu (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  hoa_don_dich_vu_id BIGINT NOT NULL,
  su_dung_dich_vu_id BIGINT NOT NULL UNIQUE,
  thanh_tien DECIMAL(15,2) NOT NULL,
  FOREIGN KEY (hoa_don_dich_vu_id) REFERENCES hoa_don_dich_vu(id) ON DELETE CASCADE,
  FOREIGN KEY (su_dung_dich_vu_id) REFERENCES su_dung_dich_vu(id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE thanh_toan_dich_vu (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_thanh_toan VARCHAR(40) NOT NULL UNIQUE,
  hoa_don_dich_vu_id BIGINT NOT NULL,
  nhan_vien_thu_id BIGINT NULL,
  phuong_thuc_id BIGINT NOT NULL,
  ca_lam_viec_id BIGINT NULL,
  so_tien DECIMAL(15,2) NOT NULL,
  thoi_gian_thanh_toan DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  ma_giao_dich VARCHAR(150) NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'THANH_CONG',
  FOREIGN KEY (hoa_don_dich_vu_id) REFERENCES hoa_don_dich_vu(id) ON DELETE RESTRICT,
  FOREIGN KEY (nhan_vien_thu_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,
  FOREIGN KEY (phuong_thuc_id) REFERENCES phuong_thuc_thanh_toan(id) ON DELETE RESTRICT,
  FOREIGN KEY (ca_lam_viec_id) REFERENCES ca_lam_viec(id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE phieu_hoan_tien (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_phieu_hoan VARCHAR(40) NOT NULL UNIQUE,
  phieu_coc_id BIGINT NULL,
  thanh_toan_hdp_id BIGINT NULL,
  thanh_toan_dv_id BIGINT NULL,
  yeu_cau_huy_id BIGINT NULL,
  nhan_vien_id BIGINT NULL,
  phuong_thuc_id BIGINT NOT NULL,
  so_tien DECIMAL(15,2) NOT NULL,
  ly_do VARCHAR(1000) NOT NULL,
  thoi_gian_hoan DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  ma_giao_dich VARCHAR(150) NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'CHO_XU_LY',
  FOREIGN KEY (phieu_coc_id) REFERENCES phieu_coc(id) ON DELETE SET NULL,
  FOREIGN KEY (thanh_toan_hdp_id) REFERENCES thanh_toan_hoa_don_phong(id) ON DELETE SET NULL,
  FOREIGN KEY (thanh_toan_dv_id) REFERENCES thanh_toan_dich_vu(id) ON DELETE SET NULL,
  FOREIGN KEY (yeu_cau_huy_id) REFERENCES yeu_cau_huy_dat_phong(id) ON DELETE SET NULL,
  FOREIGN KEY (nhan_vien_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,
  FOREIGN KEY (phuong_thuc_id) REFERENCES phuong_thuc_thanh_toan(id) ON DELETE RESTRICT,
  CHECK (so_tien > 0)
) ENGINE=InnoDB;

CREATE TABLE doi_soat_ca (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ca_lam_viec_id BIGINT NOT NULL,
  phuong_thuc_id BIGINT NOT NULL,
  tong_he_thong DECIMAL(15,2) NOT NULL DEFAULT 0,
  tong_thuc_te DECIMAL(15,2) NOT NULL DEFAULT 0,
  chenh_lech DECIMAL(15,2) NOT NULL DEFAULT 0,
  ghi_chu VARCHAR(500) NULL,
  doi_soat_luc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (ca_lam_viec_id) REFERENCES ca_lam_viec(id) ON DELETE CASCADE,
  FOREIGN KEY (phuong_thuc_id) REFERENCES phuong_thuc_thanh_toan(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_dsc(ca_lam_viec_id, phuong_thuc_id)
) ENGINE=InnoDB;

-- 6. INVENTORY / FOOD / SUPPLIES / ASSETS ------------------------------------
-- Một danh mục hàng hóa chung cho:
--   THUC_PHAM / DO_UONG / HANG_MINIBAR / VAT_TU / DO_VAI / TAI_SAN / KHAC
--
-- Không tách VẬT TƯ và TÀI SẢN thành hai bảng danh mục độc lập.
-- Tài sản chỉ có thêm TAI_SAN_CA_THE khi cần quản lý từng chiếc/serial.
-- Thực phẩm/đồ uống có thể được theo dõi theo LO_HANG và HẠN SỬ DỤNG.
-- Tồn kho được tính từ GIAO_DICH_KHO, không lưu thêm một cột tồn có thể bị lệch.

CREATE TABLE nha_cung_cap (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_nha_cung_cap VARCHAR(30) NOT NULL UNIQUE,
  ten_nha_cung_cap VARCHAR(150) NOT NULL,
  nguoi_lien_he VARCHAR(120) NULL,
  so_dien_thoai VARCHAR(20) NULL,
  email VARCHAR(150) NULL,
  dia_chi VARCHAR(255) NULL,
  ma_so_thue VARCHAR(50) NULL,
  ghi_chu VARCHAR(500) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE kho (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_kho VARCHAR(30) NOT NULL UNIQUE,
  ten_kho VARCHAR(100) NOT NULL,
  loai_kho VARCHAR(30) NOT NULL DEFAULT 'KHO_CHINH',
  -- KHO_CHINH/KHO_THUC_PHAM/KHO_MAT/KHO_DONG/KHO_DO_UONG/KHO_BUONG_PHONG/KHO_TAI_SAN
  vi_tri VARCHAR(255) NULL,
  nhiet_do_tu DECIMAL(5,2) NULL,
  nhiet_do_den DECIMAL(5,2) NULL,
  ghi_chu VARCHAR(500) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  CHECK (nhiet_do_den IS NULL OR nhiet_do_tu IS NULL OR nhiet_do_den >= nhiet_do_tu)
) ENGINE=InnoDB;

CREATE TABLE nhom_hang (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_nhom VARCHAR(30) NOT NULL UNIQUE,
  ten_nhom VARCHAR(100) NOT NULL,
  mo_ta VARCHAR(500) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG'
) ENGINE=InnoDB;

CREATE TABLE don_vi_tinh (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_don_vi VARCHAR(30) NOT NULL UNIQUE,
  ten_don_vi VARCHAR(80) NOT NULL,
  mo_ta VARCHAR(255) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG'
) ENGINE=InnoDB;

CREATE TABLE danh_muc_hang_hoa (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_hang VARCHAR(40) NOT NULL UNIQUE,
  ten_hang VARCHAR(150) NOT NULL,
  loai_hang VARCHAR(30) NOT NULL,
  -- THUC_PHAM/DO_UONG/HANG_MINIBAR/VAT_TU/DO_VAI/TAI_SAN/KHAC
  nhom_hang_id BIGINT NULL,
  don_vi_tinh_id BIGINT NOT NULL,
  gia_von_tham_chieu DECIMAL(15,2) NOT NULL DEFAULT 0,
  muc_ton_toi_thieu DECIMAL(15,2) NOT NULL DEFAULT 0,
  theo_doi_lo TINYINT(1) NOT NULL DEFAULT 0,
  theo_doi_han_su_dung TINYINT(1) NOT NULL DEFAULT 0,
  theo_doi_ca_the TINYINT(1) NOT NULL DEFAULT 0,
  dieu_kien_bao_quan VARCHAR(255) NULL,
  mo_ta VARCHAR(700) NULL,
  trang_thai VARCHAR(20) NOT NULL DEFAULT 'HOAT_DONG',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  FOREIGN KEY (nhom_hang_id) REFERENCES nhom_hang(id) ON DELETE SET NULL,
  FOREIGN KEY (don_vi_tinh_id) REFERENCES don_vi_tinh(id) ON DELETE RESTRICT,

  CHECK (gia_von_tham_chieu >= 0),
  CHECK (muc_ton_toi_thieu >= 0),
  CHECK (theo_doi_han_su_dung = 0 OR theo_doi_lo = 1),
  CHECK (theo_doi_ca_the = 0 OR loai_hang = 'TAI_SAN'),

  INDEX idx_dmhh_loai(loai_hang),
  INDEX idx_dmhh_nhom(nhom_hang_id),
  INDEX idx_dmhh_donvi(don_vi_tinh_id)
) ENGINE=InnoDB;

CREATE TABLE phieu_nhap (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_phieu_nhap VARCHAR(40) NOT NULL UNIQUE,
  nha_cung_cap_id BIGINT NOT NULL,
  kho_id BIGINT NOT NULL,
  nhan_vien_tao_id BIGINT NOT NULL,
  nhan_vien_xac_nhan_id BIGINT NULL,
  ngay_nhap DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  xac_nhan_luc DATETIME NULL,
  so_hoa_don_ncc VARCHAR(100) NULL,
  ngay_hoa_don_ncc DATE NULL,
  tong_tien_hang DECIMAL(15,2) NOT NULL DEFAULT 0,
  chiet_khau DECIMAL(15,2) NOT NULL DEFAULT 0,
  tien_thue DECIMAL(15,2) NOT NULL DEFAULT 0,
  tong_thanh_toan DECIMAL(15,2) NOT NULL DEFAULT 0,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'NHAP', -- NHAP/DA_XAC_NHAN/DA_HUY
  ghi_chu VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  FOREIGN KEY (nha_cung_cap_id) REFERENCES nha_cung_cap(id) ON DELETE RESTRICT,
  FOREIGN KEY (kho_id) REFERENCES kho(id) ON DELETE RESTRICT,
  FOREIGN KEY (nhan_vien_tao_id) REFERENCES nhan_vien(id) ON DELETE RESTRICT,
  FOREIGN KEY (nhan_vien_xac_nhan_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,

  CHECK (tong_tien_hang >= 0),
  CHECK (chiet_khau >= 0),
  CHECK (tien_thue >= 0),
  CHECK (tong_thanh_toan >= 0),

  INDEX idx_pn_ncc(nha_cung_cap_id),
  INDEX idx_pn_kho(kho_id),
  INDEX idx_pn_ngay(ngay_nhap),
  INDEX idx_pn_status(trang_thai)
) ENGINE=InnoDB;

CREATE TABLE chi_tiet_phieu_nhap (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  phieu_nhap_id BIGINT NOT NULL,
  danh_muc_hang_hoa_id BIGINT NOT NULL,
  so_luong DECIMAL(15,3) NOT NULL,
  don_gia_nhap DECIMAL(15,2) NOT NULL,
  thanh_tien DECIMAL(15,2) NOT NULL,
  ghi_chu VARCHAR(255) NULL,

  FOREIGN KEY (phieu_nhap_id) REFERENCES phieu_nhap(id) ON DELETE CASCADE,
  FOREIGN KEY (danh_muc_hang_hoa_id) REFERENCES danh_muc_hang_hoa(id) ON DELETE RESTRICT,

  CHECK (so_luong > 0),
  CHECK (don_gia_nhap >= 0),
  CHECK (thanh_tien >= 0),

  INDEX idx_ctpn_phieu(phieu_nhap_id),
  INDEX idx_ctpn_hang(danh_muc_hang_hoa_id)
) ENGINE=InnoDB;

-- Chỉ dùng khi mặt hàng cần theo dõi lô/hạn sử dụng.
-- Không lưu "số lượng còn" trực tiếp; tồn theo lô được tính từ GIAO_DICH_KHO.
CREATE TABLE lo_hang (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_lo VARCHAR(80) NOT NULL,
  danh_muc_hang_hoa_id BIGINT NOT NULL,
  chi_tiet_phieu_nhap_id BIGINT NOT NULL,
  kho_id BIGINT NOT NULL,
  ngay_san_xuat DATE NULL,
  han_su_dung DATE NULL,
  so_luong_ban_dau DECIMAL(15,3) NOT NULL,
  don_gia_nhap DECIMAL(15,2) NOT NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'CON_HAN',
  -- CON_HAN/SAP_HET_HAN/HET_HAN/DA_HET/NGUNG_SU_DUNG
  ghi_chu VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

  FOREIGN KEY (danh_muc_hang_hoa_id) REFERENCES danh_muc_hang_hoa(id) ON DELETE RESTRICT,
  FOREIGN KEY (chi_tiet_phieu_nhap_id) REFERENCES chi_tiet_phieu_nhap(id) ON DELETE RESTRICT,
  FOREIGN KEY (kho_id) REFERENCES kho(id) ON DELETE RESTRICT,

  CHECK (so_luong_ban_dau > 0),
  CHECK (don_gia_nhap >= 0),
  CHECK (han_su_dung IS NULL OR ngay_san_xuat IS NULL OR han_su_dung >= ngay_san_xuat),

  UNIQUE KEY uq_lo_hang_kho_hang (kho_id, danh_muc_hang_hoa_id, ma_lo),
  INDEX idx_lo_hsd(han_su_dung),
  INDEX idx_lo_hang(danh_muc_hang_hoa_id)
) ENGINE=InnoDB;

CREATE TABLE phieu_xuat (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_phieu_xuat VARCHAR(40) NOT NULL UNIQUE,
  kho_id BIGINT NOT NULL,
  nhan_vien_tao_id BIGINT NOT NULL,
  nhan_vien_xac_nhan_id BIGINT NULL,
  nguoi_nhan VARCHAR(150) NULL,
  muc_dich VARCHAR(50) NOT NULL,
  -- BUONG_PHONG/MINIBAR/DICH_VU/BEP/SUA_CHUA/KHAC
  ly_do VARCHAR(500) NULL,
  ngay_xuat DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  xac_nhan_luc DATETIME NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'NHAP', -- NHAP/DA_XAC_NHAN/DA_HUY
  ghi_chu VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  FOREIGN KEY (kho_id) REFERENCES kho(id) ON DELETE RESTRICT,
  FOREIGN KEY (nhan_vien_tao_id) REFERENCES nhan_vien(id) ON DELETE RESTRICT,
  FOREIGN KEY (nhan_vien_xac_nhan_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,

  INDEX idx_px_kho(kho_id),
  INDEX idx_px_ngay(ngay_xuat),
  INDEX idx_px_status(trang_thai)
) ENGINE=InnoDB;

CREATE TABLE chi_tiet_phieu_xuat (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  phieu_xuat_id BIGINT NOT NULL,
  danh_muc_hang_hoa_id BIGINT NOT NULL,
  so_luong DECIMAL(15,3) NOT NULL,
  ghi_chu VARCHAR(255) NULL,

  FOREIGN KEY (phieu_xuat_id) REFERENCES phieu_xuat(id) ON DELETE CASCADE,
  FOREIGN KEY (danh_muc_hang_hoa_id) REFERENCES danh_muc_hang_hoa(id) ON DELETE RESTRICT,

  CHECK (so_luong > 0),

  INDEX idx_ctpx_phieu(phieu_xuat_id),
  INDEX idx_ctpx_hang(danh_muc_hang_hoa_id)
) ENGINE=InnoDB;

-- Sổ kho là nguồn sự thật của tồn kho.
-- NHAP dương; XUAT/HONG_MAT/THANH_LY âm; DIEU_CHINH có thể +/-.
-- lo_hang_id dùng cho thực phẩm/đồ uống cần truy lô; NULL cho mặt hàng không theo lô.
CREATE TABLE giao_dich_kho (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_giao_dich VARCHAR(50) NOT NULL UNIQUE,
  kho_id BIGINT NOT NULL,
  danh_muc_hang_hoa_id BIGINT NOT NULL,
  lo_hang_id BIGINT NULL,
  phieu_nhap_id BIGINT NULL,
  chi_tiet_phieu_nhap_id BIGINT NULL,
  phieu_xuat_id BIGINT NULL,
  chi_tiet_phieu_xuat_id BIGINT NULL,
  nhan_vien_id BIGINT NULL,
  loai_giao_dich VARCHAR(30) NOT NULL,
  -- NHAP/XUAT/DIEU_CHINH/HONG_MAT/THANH_LY
  so_luong_thay_doi DECIMAL(15,3) NOT NULL,
  thoi_gian DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  tham_chieu VARCHAR(100) NULL,
  ghi_chu VARCHAR(500) NULL,

  FOREIGN KEY (kho_id) REFERENCES kho(id) ON DELETE RESTRICT,
  FOREIGN KEY (danh_muc_hang_hoa_id) REFERENCES danh_muc_hang_hoa(id) ON DELETE RESTRICT,
  FOREIGN KEY (lo_hang_id) REFERENCES lo_hang(id) ON DELETE RESTRICT,
  FOREIGN KEY (phieu_nhap_id) REFERENCES phieu_nhap(id) ON DELETE SET NULL,
  FOREIGN KEY (chi_tiet_phieu_nhap_id) REFERENCES chi_tiet_phieu_nhap(id) ON DELETE SET NULL,
  FOREIGN KEY (phieu_xuat_id) REFERENCES phieu_xuat(id) ON DELETE SET NULL,
  FOREIGN KEY (chi_tiet_phieu_xuat_id) REFERENCES chi_tiet_phieu_xuat(id) ON DELETE SET NULL,
  FOREIGN KEY (nhan_vien_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,

  CHECK (so_luong_thay_doi <> 0),

  INDEX idx_gdk_kho_hang(kho_id, danh_muc_hang_hoa_id),
  INDEX idx_gdk_lo(lo_hang_id),
  INDEX idx_gdk_time(thoi_gian),
  INDEX idx_gdk_loai(loai_giao_dich)
) ENGINE=InnoDB;

-- Chỉ tạo cho các dòng DANH_MUC_HANG_HOA có theo_doi_ca_the = 1.
CREATE TABLE tai_san_ca_the (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_tai_san VARCHAR(40) NOT NULL UNIQUE,
  danh_muc_hang_hoa_id BIGINT NOT NULL,
  chi_tiet_phieu_nhap_id BIGINT NULL,
  phong_id BIGINT NULL,
  kho_id BIGINT NULL,
  so_serial VARCHAR(100) NULL,
  ngay_mua DATE NULL,
  gia_mua DECIMAL(15,2) NOT NULL DEFAULT 0,
  han_bao_hanh DATE NULL,
  tinh_trang VARCHAR(30) NOT NULL DEFAULT 'TOT',
  -- TOT/HONG/MAT/BAO_TRI/THANH_LY
  trang_thai_su_dung VARCHAR(30) NOT NULL DEFAULT 'TRONG_KHO',
  -- TRONG_KHO/DANG_SU_DUNG/BAO_TRI/THANH_LY/MAT
  vi_tri_khac VARCHAR(255) NULL,
  ghi_chu VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  FOREIGN KEY (danh_muc_hang_hoa_id) REFERENCES danh_muc_hang_hoa(id) ON DELETE RESTRICT,
  FOREIGN KEY (chi_tiet_phieu_nhap_id) REFERENCES chi_tiet_phieu_nhap(id) ON DELETE SET NULL,
  FOREIGN KEY (phong_id) REFERENCES phong(id) ON DELETE SET NULL,
  FOREIGN KEY (kho_id) REFERENCES kho(id) ON DELETE SET NULL,

  UNIQUE KEY uq_tsct_serial(so_serial),
  CHECK (gia_mua >= 0),

  INDEX idx_tsct_phong(phong_id),
  INDEX idx_tsct_kho(kho_id),
  INDEX idx_tsct_hang(danh_muc_hang_hoa_id),
  INDEX idx_tsct_tinh_trang(tinh_trang)
) ENGINE=InnoDB;

-- 7. HOUSEKEEPING / ROOM CONDITION -------------------------------------------
CREATE TABLE tinh_trang_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  phong_id BIGINT NOT NULL,
  dat_phong_id BIGINT NULL,
  nhan_vien_ghi_nhan_id BIGINT NULL,
  tai_san_ca_the_id BIGINT NULL,
  loai_tinh_trang VARCHAR(40) NOT NULL, -- HONG_HOC/THIEU_TAI_SAN/MAT_TAI_SAN/BAO_TRI/KHAC
  muc_do VARCHAR(20) NOT NULL DEFAULT 'NHE',
  mo_ta VARCHAR(1500) NOT NULL,
  anh_minh_chung VARCHAR(1000) NULL,
  chi_phi_du_kien DECIMAL(15,2) NOT NULL DEFAULT 0,
  chi_phi_thuc_te DECIMAL(15,2) NOT NULL DEFAULT 0,
  do_khach_gay_ra TINYINT(1) NOT NULL DEFAULT 0,
  trang_thai_xu_ly VARCHAR(30) NOT NULL DEFAULT 'MOI_GHI_NHAN',
  ghi_nhan_luc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  xu_ly_xong_luc DATETIME NULL,
  FOREIGN KEY (phong_id) REFERENCES phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE SET NULL,
  FOREIGN KEY (nhan_vien_ghi_nhan_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,
  FOREIGN KEY (tai_san_ca_the_id) REFERENCES tai_san_ca_the(id) ON DELETE SET NULL
) ENGINE=InnoDB;

ALTER TABLE chi_tiet_hoa_don_phong
  ADD CONSTRAINT fk_cthdp_ttp FOREIGN KEY (tinh_trang_phong_id) REFERENCES tinh_trang_phong(id) ON DELETE SET NULL;

CREATE TABLE cong_viec_buong_phong (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_cong_viec VARCHAR(40) NOT NULL UNIQUE,
  phong_id BIGINT NOT NULL,
  dat_phong_id BIGINT NULL,
  loai_cong_viec VARCHAR(40) NOT NULL, -- DON_CHECKOUT/DON_HANG_NGAY/KIEM_TRA/CHUAN_BI_PHONG
  nhan_vien_duoc_giao_id BIGINT NULL,
  nhan_vien_phan_cong_id BIGINT NULL,
  muc_do_uu_tien VARCHAR(20) NOT NULL DEFAULT 'BINH_THUONG',
  bat_dau_du_kien DATETIME NULL,
  bat_dau_thuc_te DATETIME NULL,
  hoan_thanh_luc DATETIME NULL,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'CHO_XU_LY',
  ghi_chu VARCHAR(1000) NULL,
  FOREIGN KEY (phong_id) REFERENCES phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE SET NULL,
  FOREIGN KEY (nhan_vien_duoc_giao_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,
  FOREIGN KEY (nhan_vien_phan_cong_id) REFERENCES nhan_vien(id) ON DELETE SET NULL
) ENGINE=InnoDB;

-- 8. NOTIFICATIONS / REVIEWS --------------------------------------------------
CREATE TABLE thong_bao (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  loai_thong_bao VARCHAR(50) NOT NULL,
  tieu_de VARCHAR(255) NOT NULL,
  noi_dung VARCHAR(3000) NOT NULL,
  dat_phong_id BIGINT NULL,
  check_in_luc DATETIME NULL,
  check_out_luc DATETIME NULL,
  muc_do VARCHAR(20) NOT NULL DEFAULT 'THONG_THUONG',
  duong_dan VARCHAR(1000) NULL,
  tai_khoan_tao_id BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE SET NULL,
  FOREIGN KEY (tai_khoan_tao_id) REFERENCES tai_khoan(id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE thong_bao_tai_khoan (
  thong_bao_id BIGINT NOT NULL,
  tai_khoan_id BIGINT NOT NULL,
  kenh_gui VARCHAR(30) NOT NULL DEFAULT 'APP', -- APP/WEB/PUSH/EMAIL
  da_doc TINYINT(1) NOT NULL DEFAULT 0,
  da_doc_luc DATETIME NULL,
  trang_thai_gui VARCHAR(30) NOT NULL DEFAULT 'CHO_GUI',
  gui_luc DATETIME NULL,
  PRIMARY KEY(thong_bao_id, tai_khoan_id, kenh_gui),
  FOREIGN KEY (thong_bao_id) REFERENCES thong_bao(id) ON DELETE CASCADE,
  FOREIGN KEY (tai_khoan_id) REFERENCES tai_khoan(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE cai_dat_thong_bao (
  tai_khoan_id BIGINT PRIMARY KEY,
  nhan_booking TINYINT(1) NOT NULL DEFAULT 1,
  nhan_thanh_toan TINYINT(1) NOT NULL DEFAULT 1,
  nhan_van_hanh TINYINT(1) NOT NULL DEFAULT 1,
  cho_phep_email TINYINT(1) NOT NULL DEFAULT 1,
  cho_phep_push TINYINT(1) NOT NULL DEFAULT 1,
  FOREIGN KEY (tai_khoan_id) REFERENCES tai_khoan(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE danh_gia (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  ma_danh_gia VARCHAR(40) NOT NULL UNIQUE,
  dat_phong_id BIGINT NOT NULL UNIQUE,
  khach_hang_id BIGINT NOT NULL,
  diem_tong TINYINT NOT NULL,
  diem_phong TINYINT NULL,
  diem_dich_vu TINYINT NULL,
  noi_dung VARCHAR(3000) NULL,
  ngay_danh_gia DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  trang_thai VARCHAR(30) NOT NULL DEFAULT 'HIEN_THI',
  phan_hoi_khach_san VARCHAR(3000) NULL,
  nhan_vien_phan_hoi_id BIGINT NULL,
  ngay_phan_hoi DATETIME NULL,
  FOREIGN KEY (dat_phong_id) REFERENCES dat_phong(id) ON DELETE RESTRICT,
  FOREIGN KEY (khach_hang_id) REFERENCES khach_hang(id) ON DELETE RESTRICT,
  FOREIGN KEY (nhan_vien_phan_hoi_id) REFERENCES nhan_vien(id) ON DELETE SET NULL,
  CHECK (diem_tong BETWEEN 1 AND 5)
) ENGINE=InnoDB;

-- BUSINESS RULES CHO KHO / THỰC PHẨM / TÀI SẢN ------------------------------
-- K1. DANH_MUC_HANG_HOA là danh mục chung. Không tạo bảng danh mục riêng cho
--     THUC_PHAM, VAT_TU và TAI_SAN.
--
-- K2. Khi PHIEU_NHAP chuyển sang DA_XAC_NHAN, backend phải trong CÙNG transaction:
--     (a) khóa phiếu nhập để chống xác nhận lặp,
--     (b) tính/kiểm tra lại tổng tiền từ CHI_TIET_PHIEU_NHAP,
--     (c) với hàng theo dõi lô: tạo LO_HANG,
--     (d) tạo GIAO_DICH_KHO dương cho từng dòng/lô,
--     (e) với hàng theo_doi_ca_the=1: tạo TAI_SAN_CA_THE tương ứng.
--
-- K3. Không xác nhận phiếu nhập rỗng. Không sửa/xóa vật lý phiếu đã DA_XAC_NHAN.
--     Nếu sai, lập giao dịch điều chỉnh/hủy nghiệp vụ và ghi NHAT_KY_HE_THONG.
--
-- K4. Với hàng theo_doi_han_su_dung=1, bắt buộc giao dịch nhập phải gắn LO_HANG
--     có hạn sử dụng. Backend cần cảnh báo lô sắp hết hạn/hết hạn.
--
-- K5. Khi xuất thực phẩm/đồ uống theo lô, ưu tiên FEFO:
--     lô có HẠN SỬ DỤNG gần nhất nhưng còn hiệu lực được xuất trước.
--
-- K6. Khi PHIEU_XUAT chuyển sang DA_XAC_NHAN, backend phải kiểm tra tồn khả dụng
--     rồi tạo GIAO_DICH_KHO âm trong CÙNG transaction.
--
-- K7. TAI_SAN_CA_THE chỉ dùng cho tài sản cần quản lý từng chiếc/serial.
--     Vật tư tiêu hao, thực phẩm và đồ uống không tạo cá thể.
--
-- K8. Tồn kho tổng và tồn theo lô đều được tính từ GIAO_DICH_KHO.
--     Không duy trì thêm một cột "so_luong_ton" mutable trong danh mục hàng.

-- REPORTING VIEWS --------------------------------------------------------------

CREATE OR REPLACE VIEW vw_ton_kho AS
SELECT
    k.id AS kho_id,
    k.ma_kho,
    k.ten_kho,
    h.id AS danh_muc_hang_hoa_id,
    h.ma_hang,
    h.ten_hang,
    h.loai_hang,
    dvt.ten_don_vi,
    COALESCE(SUM(gd.so_luong_thay_doi), 0) AS so_luong_ton,
    h.muc_ton_toi_thieu
FROM kho k
CROSS JOIN danh_muc_hang_hoa h
JOIN don_vi_tinh dvt ON dvt.id = h.don_vi_tinh_id
LEFT JOIN giao_dich_kho gd
       ON gd.kho_id = k.id
      AND gd.danh_muc_hang_hoa_id = h.id
GROUP BY
    k.id, k.ma_kho, k.ten_kho,
    h.id, h.ma_hang, h.ten_hang, h.loai_hang,
    dvt.ten_don_vi, h.muc_ton_toi_thieu;

CREATE OR REPLACE VIEW vw_ton_kho_theo_lo AS
SELECT
    lh.id AS lo_hang_id,
    lh.ma_lo,
    lh.kho_id,
    k.ma_kho,
    lh.danh_muc_hang_hoa_id,
    h.ma_hang,
    h.ten_hang,
    lh.ngay_san_xuat,
    lh.han_su_dung,
    COALESCE(SUM(gd.so_luong_thay_doi), 0) AS so_luong_con
FROM lo_hang lh
JOIN kho k ON k.id = lh.kho_id
JOIN danh_muc_hang_hoa h ON h.id = lh.danh_muc_hang_hoa_id
LEFT JOIN giao_dich_kho gd ON gd.lo_hang_id = lh.id
GROUP BY
    lh.id, lh.ma_lo, lh.kho_id, k.ma_kho,
    lh.danh_muc_hang_hoa_id, h.ma_hang, h.ten_hang,
    lh.ngay_san_xuat, lh.han_su_dung;

CREATE OR REPLACE VIEW vw_hang_sap_het_han AS
SELECT
    lh.id AS lo_hang_id,
    lh.ma_lo,
    h.ma_hang,
    h.ten_hang,
    k.ma_kho,
    lh.han_su_dung,
    DATEDIFF(lh.han_su_dung, CURDATE()) AS so_ngay_con_lai,
    tk.so_luong_con
FROM lo_hang lh
JOIN danh_muc_hang_hoa h ON h.id = lh.danh_muc_hang_hoa_id
JOIN kho k ON k.id = lh.kho_id
JOIN vw_ton_kho_theo_lo tk ON tk.lo_hang_id = lh.id
WHERE lh.han_su_dung IS NOT NULL
  AND tk.so_luong_con > 0
  AND DATEDIFF(lh.han_su_dung, CURDATE()) <= 7;

CREATE OR REPLACE VIEW vw_cong_no_hoa_don_phong AS
SELECT hd.id hoa_don_phong_id, hd.ma_hoa_don, hd.dat_phong_id, hd.tong_tien,
       COALESCE(c.tien_coc_ap_dung,0) tien_coc_ap_dung,
       COALESCE(t.tien_da_thanh_toan,0) tien_da_thanh_toan,
       GREATEST(hd.tong_tien-COALESCE(c.tien_coc_ap_dung,0)-COALESCE(t.tien_da_thanh_toan,0),0) con_phai_thu
FROM hoa_don_phong hd
LEFT JOIN (SELECT hoa_don_phong_id,SUM(so_tien_ap_dung) tien_coc_ap_dung FROM ap_dung_coc_hoa_don GROUP BY hoa_don_phong_id) c ON c.hoa_don_phong_id=hd.id
LEFT JOIN (SELECT hoa_don_phong_id,SUM(so_tien) tien_da_thanh_toan FROM thanh_toan_hoa_don_phong WHERE trang_thai='THANH_CONG' GROUP BY hoa_don_phong_id) t ON t.hoa_don_phong_id=hd.id;

CREATE OR REPLACE VIEW vw_cong_no_hoa_don_dich_vu AS
SELECT hd.id hoa_don_dich_vu_id, hd.ma_hoa_don, hd.dat_phong_id, hd.tong_tien,
       COALESCE(t.tien_da_thanh_toan,0) tien_da_thanh_toan,
       GREATEST(hd.tong_tien-COALESCE(t.tien_da_thanh_toan,0),0) con_phai_thu
FROM hoa_don_dich_vu hd
LEFT JOIN (SELECT hoa_don_dich_vu_id,SUM(so_tien) tien_da_thanh_toan FROM thanh_toan_dich_vu WHERE trang_thai='THANH_CONG' GROUP BY hoa_don_dich_vu_id) t ON t.hoa_don_dich_vu_id=hd.id;


-- STARTER MASTER DATA CHO KHO -------------------------------------------------
INSERT IGNORE INTO don_vi_tinh (ma_don_vi, ten_don_vi) VALUES
('CAI', 'Cái'),
('CHAI', 'Chai'),
('LON', 'Lon'),
('HOP', 'Hộp'),
('GOI', 'Gói'),
('KG', 'Kilogram'),
('G', 'Gram'),
('LIT', 'Lít'),
('BO', 'Bộ'),
('CUON', 'Cuộn');

INSERT IGNORE INTO nhom_hang (ma_nhom, ten_nhom) VALUES
('THUC_PHAM', 'Thực phẩm'),
('DO_UONG', 'Đồ uống'),
('MINIBAR', 'Hàng minibar'),
('VAT_TU_BUONG', 'Vật tư buồng phòng'),
('DO_VAI', 'Đồ vải'),
('VE_SINH', 'Hóa chất và vật tư vệ sinh'),
('TAI_SAN_PHONG', 'Tài sản trong phòng'),
('TAI_SAN_KHAC', 'Tài sản khác');

INSERT IGNORE INTO kho (ma_kho, ten_kho, loai_kho, vi_tri) VALUES
('KHO_CHINH', 'Kho chính', 'KHO_CHINH', 'Khu vực kho chính');

-- Critical rule: KHOA_LICH_PHONG.UNIQUE(phong_id, ngay_luu_tru) prevents two
-- bookings from occupying the same physical room on the same stay date.
-- There is intentionally NO KHUYEN_MAI / MA_KHUYEN_MAI table. Seasonal/holiday
-- pricing is represented only by CHINH_SACH_GIA and snapshotted in GIA_DEM_DAT_PHONG.
-- Food, drinks, supplies, linen and assets share DANH_MUC_HANG_HOA. Food/drinks may
-- use LO_HANG for expiry tracking; individually managed assets use TAI_SAN_CA_THE.
