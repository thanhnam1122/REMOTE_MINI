@echo off
chcp 65001 > nul
echo ========================================================
echo   COMPILING SERVER WINFORMS (.NET / C#)
echo ========================================================
echo.

cd /d "%~dp0Server_WinForms"

set CSC_PATH="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"

if not exist %CSC_PATH% (
    set CSC_PATH="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
)

echo Dang bien dich voi C# Compiler at: %CSC_PATH%
%CSC_PATH% /target:winexe /out:RemoteDesktopServer.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll Program.cs MainForm.cs MainForm.Designer.cs Models\PacketProtocol.cs Services\TcpServerService.cs Helpers\CoordinateMapper.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [THANH CONG] Da bien dich thanh cong RemoteDesktopServer.exe!
) else (
    echo.
    echo [LOI] Bien dich that bai! Vui long kiem tra lai moi truong.
)

pause
