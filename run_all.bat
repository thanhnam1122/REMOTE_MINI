@echo off
chcp 65001 > nul
echo ========================================================
echo   KHOI CHAY DEMO: SERVER + CLIENT
echo ========================================================
echo.

cd /d "%~dp0"

echo 1. Dang khoi chay Server (.NET WinForms)...
start "" "run_server.bat"

timeout /t 2 > nul

echo 2. Dang khoi chay Client (Python)...
start "" "run_client.bat"

echo.
echo Da khoi chay xong ca 2 ung dung!
