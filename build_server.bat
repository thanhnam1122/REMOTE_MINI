@echo off
chcp 65001 > nul
echo ========================================================
echo   BIEN DICH STAGE: REMOTE DESKTOP SERVER (WPF .NET 8)
echo ========================================================
echo.

cd /d "%~dp0"

dotnet build Server_WPF\RemoteDesktopServer.csproj --configuration Debug --nologo

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [LOI] Bien dich WPF Server THAT BAI!
    pause
    exit /b 1
)

echo.
echo [THANH CONG] Da bien dich xong WPF Server!
pause
