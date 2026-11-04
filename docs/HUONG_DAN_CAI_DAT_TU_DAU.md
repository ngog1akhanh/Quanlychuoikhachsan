# Hướng dẫn cài đặt và chạy hệ thống từ đầu

Dành cho thành viên **mới clone/pull source về, máy chưa có database**. Làm lần lượt từ bước 1 đến bước 7, mỗi bước có phần "Kiểm tra" để biết đã làm đúng chưa. Toàn bộ mất khoảng 30–45 phút (phần lớn là thời gian cài phần mềm).

> Đã có sẵn database và chỉ cần chạy lại? Nhảy tới [Bước 6](#bước-6--chạy-hệ-thống).

---

## Mục lục

0. [Hệ thống gồm những gì](#bước-0--hệ-thống-gồm-những-gì)
1. [Cài phần mềm](#bước-1--cài-phần-mềm)
2. [Lấy source code](#bước-2--lấy-source-code)
3. [Tạo database](#bước-3--tạo-database)
4. [Tạo tài khoản MySQL cho ứng dụng](#bước-4--tạo-tài-khoản-mysql-cho-ứng-dụng)
5. [(Tùy chọn) Dùng mật khẩu/tài khoản MySQL khác](#bước-5--tùy-chọn-dùng-mật-khẩutài-khoản-mysql-khác)
6. [Chạy hệ thống](#bước-6--chạy-hệ-thống)
7. [Đăng nhập và kiểm tra](#bước-7--đăng-nhập-và-kiểm-tra)
8. [Lỗi thường gặp](#lỗi-thường-gặp)
9. [Dựng lại database / khi có script mới](#dựng-lại-database--khi-có-script-mới)
10. [Phụ lục: cấu trúc thư mục](#phụ-lục-cấu-trúc-thư-mục)

---

## Bước 0 – Hệ thống gồm những gì

```
 Website khách hàng (QuanLyKhachSan.Web)  ─┐
   http://localhost:5100                    │  gọi HTTP
                                            ├──────────►  API backend (Quanlychuoikhachsan)  ──►  MySQL
 App quản lý WinForms (HotelAdmin.WinForms)─┘              http://localhost:5005                 database
   (cửa sổ Windows)                                         Swagger: /swagger                    hotel_management
```

- **Chỉ API kết nối MySQL.** Website và App quản lý lấy dữ liệu qua API, không cần cấu hình database.
- Vì vậy thứ tự luôn là: **MySQL chạy → API chạy → mới mở Website / App**.

---

## Bước 1 – Cài phần mềm

| Phần mềm | Phiên bản | Ghi chú khi cài |
|---|---|---|
| **.NET SDK** | **9.x** (hoặc 10.x) | Tải tại https://dotnet.microsoft.com/download/dotnet/9.0 → mục *SDK* → Windows x64. Cả 4 project dùng `net9.0`; máy đã có .NET 10 SDK thì dùng luôn, không cần cài thêm |
| **Visual Studio** | **2022 bản 17.12 trở lên** hoặc 2026 (Community là đủ) | Chọn 2 workload: **ASP.NET and web development** và **.NET desktop development** |
| **MySQL Server** | **8.0.16 trở lên** | Dùng *MySQL Installer for Windows*, chọn kiểu *Custom* hoặc *Developer Default*, tích **MySQL Server** và **MySQL Workbench** |
| **MySQL Workbench** | 8.x | Cài cùng MySQL Installer |
| **Git** | bất kỳ | Để clone/pull source |

Khi cài **MySQL Server**:
- Port giữ mặc định **3306**.
- *Authentication Method*: chọn **Use Strong Password Encryption** (mặc định).
- Đặt **mật khẩu cho `root`** và **ghi lại** – bước 3 và 5 cần dùng.
- *Windows Service*: để tích **Start the MySQL Server at System Startup** (MySQL tự chạy khi bật máy).

Không cần cài Python (chỉ dùng khi sửa script nguồn database), không cần cài MariaDB nếu đã có MySQL.

**Kiểm tra:** mở *Command Prompt* hoặc *PowerShell*, gõ:

```powershell
dotnet --list-sdks
```

Phải thấy một dòng bắt đầu bằng `9.` (hoặc `10.`). Không thấy → cài .NET 9 SDK rồi mở lại cửa sổ lệnh.

---

## Bước 2 – Lấy source code

```powershell
git clone <địa chỉ repo của nhóm>
cd <thư mục repo>
git checkout <nhánh nhóm đang dùng>
```

Đã clone từ trước thì chỉ cần `git pull`.

**Kiểm tra:** trong thư mục repo có file `HeThongQuanLyKhachSan.sln` và file
`Quanlychuoikhachsan/Database/Install/hotel_management_full.sql`.

---

## Bước 3 – Tạo database

Chỉ cần chạy **một file duy nhất**: `Quanlychuoikhachsan/Database/Install/hotel_management_full.sql`.
File này tạo database **`hotel_management`** gồm toàn bộ bảng, quyền và **dữ liệu demo** (tài khoản, 24 phòng, booking, hóa đơn lịch sử cho báo cáo, kho, việc buồng phòng).

1. Mở **MySQL Workbench** → bấm vào kết nối **Local instance MySQL80** (hoặc tạo kết nối: Hostname `127.0.0.1`, Port `3306`, Username `root`) → nhập mật khẩu root.
2. Menu **File → Open SQL Script…** → chọn file
   `…\Quanlychuoikhachsan\Database\Install\hotel_management_full.sql`.
3. Bấm nút **⚡ (Execute)** đầu tiên trên thanh công cụ (chạy *toàn bộ* script).
   **Không** dùng nút ⚡ có con trỏ (chỉ chạy một câu).
4. Đợi khoảng 10–30 giây. Ô kết quả cuối cùng hiện:

   | database_name | installation_status |
   |---|---|
   | hotel_management | **READY_WITH_DEMO_DATA** |

**Kiểm tra:** mở tab Query mới, chạy:

```sql
SELECT (SELECT COUNT(*) FROM hotel_management.tai_khoan) AS tai_khoan,
       (SELECT COUNT(*) FROM hotel_management.phong)     AS phong,
       (SELECT COUNT(*) FROM hotel_management.dat_phong) AS dat_phong;
```

Kết quả khoảng `10 | 24 | 400+`.

> **Không chạy thêm** file nào trong `Database/Scripts` sau bước này – file full đã gồm hết.
> Dữ liệu demo tính theo **ngày chạy script** (khách đến hôm nay, lô hàng sắp hết hạn…).

---

## Bước 4 – Tạo tài khoản MySQL cho ứng dụng

File cấu hình mẫu của API (`Quanlychuoikhachsan/appsettings.json`) đã trỏ sẵn tới database **`hotel_management`** bằng tài khoản MySQL **`hotel_app`**, mật khẩu **`change-me`**. Chỉ cần tạo đúng tài khoản này là API chạy được, **không phải cấu hình gì thêm**. Chạy trong Workbench:

```sql
CREATE USER IF NOT EXISTS 'hotel_app'@'localhost' IDENTIFIED BY 'change-me';
GRANT ALL PRIVILEGES ON hotel_management.* TO 'hotel_app'@'localhost';
FLUSH PRIVILEGES;
```

**Kiểm tra:** Workbench → bấm dấu ⊕ cạnh *MySQL Connections* → Username `hotel_app`, bấm *Test Connection*, nhập `change-me` → báo *Successfully made the MySQL connection*.

> `change-me` chỉ dùng cho máy cá nhân khi học/demo. Muốn dùng mật khẩu khác hoặc dùng `root`, làm thêm bước 5.

---

## Bước 5 – (Tùy chọn) Dùng mật khẩu/tài khoản MySQL khác

Bỏ qua bước này nếu đã làm bước 4 đúng như trên.

API đọc chuỗi kết nối theo thứ tự `appsettings.json` → **User Secrets** (ghi đè). User Secrets lưu riêng trên từng máy (ngoài thư mục repo), không bị đưa lên Git, nên dùng khi muốn đổi tài khoản/mật khẩu mà không sửa file chung.

### Cách 1 – Bằng Visual Studio

1. Mở `HeThongQuanLyKhachSan.sln` bằng Visual Studio.
2. Ở **Solution Explorer**, chuột phải project **`Quanlychuoikhachsan`** → **Manage User Secrets**.
3. Dán vào file `secrets.json` (thay `MAT_KHAU_MYSQL`), rồi **Ctrl + S**:

```json
{
  "ConnectionStrings:HotelManagement": "Server=localhost;Port=3306;Database=hotel_management;User=root;Password=MAT_KHAU_MYSQL;CharSet=utf8mb4;Allow User Variables=true",
  "Jwt:SigningKey": "doi-thanh-chuoi-bi-mat-rieng-dai-toi-thieu-32-ky-tu-123456"
}
```

   `Jwt:SigningKey` là chuỗi bất kỳ **từ 32 ký tự trở lên** (không bắt buộc; nếu bỏ dòng này API dùng khóa mẫu trong `appsettings.json`).

### Cách 2 – Bằng dòng lệnh (tại thư mục repo)

```powershell
dotnet user-secrets set "ConnectionStrings:HotelManagement" "Server=localhost;Port=3306;Database=hotel_management;User=root;Password=MAT_KHAU_MYSQL;CharSet=utf8mb4;Allow User Variables=true" --project Quanlychuoikhachsan
```

**Kiểm tra:** `dotnet user-secrets list --project Quanlychuoikhachsan` thấy dòng vừa đặt. Muốn quay lại dùng `appsettings.json`: `dotnet user-secrets clear --project Quanlychuoikhachsan`.

> Dùng MariaDB thay MySQL: thêm một dòng `"Database:ServerVersion": "10.11.0-mariadb"` (ghi đúng phiên bản). MySQL 8 thì không cần.

---

## Bước 6 – Chạy hệ thống

### Cách 1 – Visual Studio (khuyến nghị, có debug)

1. Lần đầu: menu **Build → Build Solution** (Ctrl + Shift + B). Lần đầu cần Internet để tải các gói NuGet. Cửa sổ **Error List** phải báo **0 Errors**.
2. Trên thanh công cụ, ở ô chọn project chạy (cạnh nút ▶), chọn **Chay ca API + Web + App**.
   - Không thấy mục này: chuột phải **Solution** → **Configure Startup Projects…** → **Multiple startup projects** → đặt `Quanlychuoikhachsan`, `QuanLyKhachSan.Web`, `HotelAdmin.WinForms` là **Start** (API ở trên cùng), với API và Web chọn profile **http**.
3. Bấm **▶ Start** (F5).

### Cách 2 – Không cần mở Visual Studio

Nhấp đúp file **`chay-he-thong.bat`** ở thư mục gốc repo. File này lần lượt:
1. mở cửa sổ **API – cổng 5005**,
2. đợi API sẵn sàng rồi mở cửa sổ **Web – cổng 5100**,
3. mở **App quản lý** và trình duyệt tới website.

Đóng các cửa sổ dòng lệnh đó để tắt hệ thống.

> Luôn chạy API bằng profile **http** (cổng 5005). App và website đều được cấu hình gọi `http://localhost:5005`.

**Kiểm tra API:** mở trình duyệt vào **http://localhost:5005/swagger** – phải thấy trang Swagger liệt kê các API `/v1/...`.

---

## Bước 7 – Đăng nhập và kiểm tra

Mật khẩu chung của mọi tài khoản demo: **`12345`** (danh sách đầy đủ trong `tai khoan test demo.txt`).

| Nơi đăng nhập | Tài khoản | Vai trò |
|---|---|---|
| App quản lý | `admin.com` | Quản trị (toàn quyền) |
| App quản lý | `quanly@hotel.com` | Quản lý |
| App quản lý | `letan@hotel.com` | Lễ tân |
| App quản lý | `buongphong@hotel.com` | Buồng phòng |
| App quản lý | `dichvu@hotel.com` | Dịch vụ |
| App quản lý | `kho@hotel.com` | Kho |
| Website (http://localhost:5100) | `an.nguyen@example.com` | Khách hàng (hoặc tự đăng ký tài khoản mới) |

**Danh sách kiểm tra nhanh:**

- [ ] App: đăng nhập `admin.com` → trang **Tổng quan** có số liệu, biểu đồ doanh thu.
- [ ] App: **Nhận / trả phòng** có danh sách khách đến hôm nay.
- [ ] App: **Kho & tài sản** có phiếu nhập, tồn kho, lô hàng sắp hết hạn.
- [ ] App: chuông thông báo (góc phải) hiện số thông báo sau khoảng 1 phút.
- [ ] Website: xem danh sách phòng, đăng nhập khách, đặt thử một phòng.

---

## Lỗi thường gặp

| Hiện tượng | Nguyên nhân | Cách xử lý |
|---|---|---|
| Workbench: `Can't create database 'hotel_management'; database exists` | Database đã tạo từ trước | Muốn dựng lại: xem [mục dưới](#dựng-lại-database--khi-có-script-mới). Không thì bỏ qua bước 3 |
| Workbench chạy rất nhiều lỗi `Table ... already exists` | Đang bật *Continue SQL script execution on errors* nên chạy tiếp vào database cũ | **Edit → Preferences → SQL Editor** → bỏ tích tùy chọn đó, dựng lại database |
| API báo `Access denied for user 'hotel_app'...` | Chưa tạo user `hotel_app` / sai mật khẩu, hoặc User Secrets ghi đè sai | Làm lại bước 4; kiểm tra `dotnet user-secrets list --project Quanlychuoikhachsan` |
| API báo `Unknown database 'hotel_management'` | Chưa chạy bước 3 | Làm bước 3 |
| API báo `Unable to connect to any of the specified MySQL hosts` | MySQL chưa chạy / sai cổng | Windows: mở **Services** → **MySQL80** → **Start** |
| API báo `Jwt:SigningKey is not configured` hoặc lỗi khóa quá ngắn | Thiếu/khóa JWT dưới 32 ký tự | Đặt lại `Jwt:SigningKey` ở bước 5 |
| Build báo `NETSDK1045` / không hỗ trợ `net9.0` | Chưa cài .NET 9 SDK hoặc Visual Studio quá cũ | Cài .NET 9 SDK, dùng Visual Studio 2022 bản 17.12 trở lên (hoặc 2026) |
| Build báo không tải được package (NU1101, NU1301) | Mất mạng / chặn nuget.org | Kết nối Internet rồi build lại |
| App/Website báo *Không kết nối được máy chủ* | API chưa chạy hoặc không ở cổng 5005 | Mở http://localhost:5005/swagger để kiểm tra; chạy API bằng profile **http** |
| App báo *Không tìm thấy dữ liệu (hoặc API chưa được triển khai)* | App bản mới nhưng API đang chạy bản cũ | Tắt API, **Build Solution**, chạy lại |
| Đăng nhập báo sai mật khẩu | Database `hotel_management` cũ không phải bản dựng từ file full, hoặc User Secrets trỏ nhầm database | Dựng lại database (mục dưới); kiểm tra `dotnet user-secrets list` |
| Tiếng Việt trong dữ liệu bị lỗi font | Database không tạo bằng file full (thiếu `utf8mb4`) | Dựng lại bằng `hotel_management_full.sql` |

---

## Dựng lại database / khi có script mới

Khi nhóm báo có thay đổi database (file `hotel_management_full.sql` được cập nhật) hoặc muốn làm sạch dữ liệu demo:

1. (Nếu cần giữ dữ liệu đang có) **Server → Data Export** để sao lưu.
2. Tắt API.
3. Trong Workbench chạy:
   ```sql
   DROP DATABASE hotel_management;
   ```
   **Lệnh này xóa toàn bộ dữ liệu trong database đó.**
4. `git pull` rồi làm lại **bước 3**.
5. Chạy lại API. (Tài khoản `hotel_app` và User Secrets giữ nguyên, không cần tạo lại.)

File `hotel_management_full.sql` **không bao giờ tự xóa hay ghi đè** database đã có: nếu database đã tồn tại, script dừng ngay ở dòng đầu.

**Người sửa script database** (thêm bảng/cột, sửa dữ liệu demo): sửa file nguồn trong `Quanlychuoikhachsan/Database/Scripts`, rồi tại thư mục repo chạy (cần Python 3):

```powershell
python scripts/build_database.py          # sinh lại hotel_management_full.sql
python scripts/build_database.py --check  # kiểm tra file đã khớp script nguồn chưa
```

Commit cả script nguồn lẫn `Quanlychuoikhachsan/Database/Install/hotel_management_full.sql`. Không sửa tay các file trong `Install`.

---

## Phụ lục: cấu trúc thư mục

```
HeThongQuanLyKhachSan.sln          Solution chung (mở bằng Visual Studio)
HeThongQuanLyKhachSan.slnLaunch    Cấu hình chạy cùng lúc API + Web + App
chay-he-thong.bat                  Chạy cả hệ thống không cần Visual Studio
tai khoan test demo.txt            Tài khoản demo (mật khẩu 12345)

Quanlychuoikhachsan/               API backend (ASP.NET Core, cổng 5005)
  Api/Controllers/                   Đường dẫn HTTP /v1/...
  Application/                       Nghiệp vụ (đặt phòng, lễ tân, hóa đơn, kho, thông báo...)
  Domain/Entities/                   Lớp ánh xạ bảng database
  Infrastructure/                    Truy cập database, xác thực JWT, VietQR, middleware
  Database/Install/                  File cài database
    hotel_management_full.sql          ★ chạy file này để dựng database + dữ liệu demo
  Database/Scripts/                  Script nguồn (schema, quyền, từng bộ dữ liệu demo)
  appsettings.json                   Cấu hình: database hotel_management, user hotel_app / change-me

QuanLyKhachSan.Web/                Website khách hàng (Blazor, cổng 5100)
HotelAdmin.WinForms/               App quản lý cho nhân viên (Windows)
Quanlychuoikhachsan.Api.Tests/     Kiểm thử tự động (dotnet test)
scripts/                           Công cụ Python: build_database.py, xuất tài liệu API
tools/WinFormsSmoke/               Công cụ kiểm thử nhanh giao diện WinForms
docs/                              Tài liệu: HUONG_DAN_BACKEND.md, API_ENDPOINTS.md, ...
```

Tài liệu liên quan: [HUONG_DAN_BACKEND.md](HUONG_DAN_BACKEND.md) (cấu hình nâng cao, quy tắc nghiệp vụ, vai trò – quyền), [API_ENDPOINTS.md](API_ENDPOINTS.md), [API_WORKFLOWS.md](API_WORKFLOWS.md).
