@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo [1/3] Khoi dong API (http://localhost:5005)...
start "API - cong 5005" cmd /k dotnet run --project Quanlychuoikhachsan --launch-profile http

echo Dang doi API san sang...
:wait
powershell -NoProfile -Command "try{$c=New-Object Net.Sockets.TcpClient;$c.Connect('localhost',5005);$c.Close();exit 0}catch{exit 1}"
if errorlevel 1 (
  timeout /t 2 >nul
  goto wait
)

echo [2/3] Khoi dong Web (http://localhost:5100)...
start "Web - cong 5100" cmd /k dotnet run --project QuanLyKhachSan.Web --launch-profile http

echo [3/3] Khoi dong App quan ly (WinForms)...
start "App quan ly" dotnet run --project HotelAdmin.WinForms

timeout /t 6 >nul
start http://localhost:5100
