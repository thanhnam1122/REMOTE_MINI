@echo off
chcp 65001 > nul
echo ========================================================
echo   BIEN DICH STAGE: REMOTE DESKTOP CLIENT (WPF .NET 9)
echo ========================================================
echo.

cd /d "%~dp0"

dotnet build Client_WPF\RemoteDesktopClient.csproj --configuration Debug --nologo

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [LOI] Bien dich WPF Client THAT BAI!
    pause
    exit /b 1
)

echo.
echo [THANH CONG] Da bien dich xong WPF Client!
pause
