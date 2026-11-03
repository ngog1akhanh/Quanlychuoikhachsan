from pathlib import Path
import re
root = Path(__file__).resolve().parents[1]
codes=set()
for p in (root/'Quanlychuoikhachsan/Api/Controllers').glob('*.cs'):
    codes.update(re.findall(r'RequirePermission\("([A-Z_]+)"\)',p.read_text(encoding='utf-8-sig')))
roles={
 'MANAGER': sorted(codes - {'ACCOUNT_MANAGE','ROLE_WRITE','STAFF_WRITE','VIETQR_PAYMENT_STATUS_WRITE'}),
 'RECEPTIONIST': ['BOOKING_READ','BOOKING_WRITE','CUSTOMER_READ','CUSTOMER_WRITE','ROOM_READ','ROOM_TYPE_READ','BILLING_READ','BILLING_WRITE','BILLING_PAYMENT_WRITE','HOUSEKEEPING_READ','HOUSEKEEPING_WRITE','SERVICE_READ','SERVICE_WRITE'],
 'HOUSEKEEPING': ['ROOM_READ','HOUSEKEEPING_READ','HOUSEKEEPING_WRITE','INVENTORY_READ'],
 'SERVICE': ['SERVICE_READ','SERVICE_WRITE','BILLING_READ','BILLING_PAYMENT_WRITE'],
 'WAREHOUSE': ['INVENTORY_READ','INVENTORY_WRITE','PURCHASE_WRITE','PURCHASE_CONFIRM','STOCK_ISSUE_WRITE','STOCK_ISSUE_CONFIRM','STOCK_ADJUST','ASSET_WRITE'],
}
lines=['-- Chạy trên database hotel_management sau schema chính. Có thể chạy lại.', '-- Không tạo tài khoản hoặc mật khẩu mặc định. Gán tài khoản đầu tiên vào ADMIN bằng quy trình quản trị hiện có.', 'START TRANSACTION;', 'INSERT INTO quyen (ma_quyen, ten_quyen, nhom_quyen) VALUES']
lines += ["  ('%s', '%s', '%s')%s"%(c,c,c.split('_')[0],',' if i<len(codes)-1 else '') for i,c in enumerate(sorted(codes))]
lines += ['ON DUPLICATE KEY UPDATE ten_quyen = VALUES(ten_quyen), nhom_quyen = VALUES(nhom_quyen);', '', 'INSERT IGNORE INTO vai_tro (ma_vai_tro, ten_vai_tro, trang_thai) VALUES']
allroles=['ADMIN']+list(roles)
lines += ["  ('%s', '%s', 'HOAT_DONG')%s"%(r,r,',' if i<len(allroles)-1 else ';') for i,r in enumerate(allroles)]
legacy_roles = {'LE_TAN': 'RECEPTIONIST', 'BUONG_PHONG': 'HOUSEKEEPING', 'DICH_VU': 'SERVICE', 'KHO': 'WAREHOUSE'}
# Bổ sung quyền cho vai trò cũ nếu đã tồn tại; giữ nguyên liên kết tài khoản.
grants = {'ADMIN': sorted(codes), **roles, **{old: roles[new] for old, new in legacy_roles.items()}}
for role,perms in grants.items():
    lines += ['', 'INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)', 'SELECT r.id, p.id FROM vai_tro r JOIN quyen p ON p.ma_quyen IN (', '  '+', '.join("'%s'"%c for c in perms)+')', "WHERE r.ma_vai_tro = '%s';"%role]
lines += ['COMMIT;', '']
(root/'Quanlychuoikhachsan/Database/Scripts/seed_api_permissions.sql').write_text('\n'.join(lines),encoding='utf-8')
print(f'{len(codes)} permissions, {len(allroles)} roles')
