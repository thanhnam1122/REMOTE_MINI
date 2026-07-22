@echo off
chcp 65001 > nul
echo ========================================================
echo   KHOI CHAY REMOTE DESKTOP SERVER (MAY DIEU KHIEN)
echo ========================================================
echo.

cd /d "%~dp0Server_WinForms"

if not exist "RemoteDesktopServer.exe" (
    echo Dang bien dich Server...
    call ..\build_server.bat
)

start "" "RemoteDesktopServer.exe"
echo Server da duoc khoi chay!
