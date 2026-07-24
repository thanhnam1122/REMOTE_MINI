@echo off
chcp 65001 > nul
echo ========================================================
echo   KHOI CHAY REMOTE DESKTOP CLIENT (WPF .NET 9)
echo ========================================================
echo.

cd /d "%~dp0"

if exist "Client_WPF\bin\Debug\net8.0-windows10.0.19041.0\RemoteDesktopClient.exe" (
    start "" "Client_WPF\bin\Debug\net8.0-windows10.0.19041.0\RemoteDesktopClient.exe"
) else (
    dotnet run --project Client_WPF\RemoteDesktopClient.csproj
)
