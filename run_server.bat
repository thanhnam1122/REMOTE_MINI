@echo off
chcp 65001 > nul
echo ========================================================
echo   KHOI CHAY REMOTE DESKTOP SERVER (WPF .NET 8)
echo ========================================================
echo.

cd /d "%~dp0"

if exist "Server_WPF\bin\Debug\net8.0-windows\RemoteDesktopServer.exe" (
    start "" "Server_WPF\bin\Debug\net8.0-windows\RemoteDesktopServer.exe"
) else (
    dotnet run --project Server_WPF\RemoteDesktopServer.csproj
)
