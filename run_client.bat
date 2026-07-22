@echo off
chcp 65001 > nul
echo ========================================================
echo   KHOI CHAY REMOTE DESKTOP CLIENT (MAY BI DIEU KHIEN)
echo ========================================================
echo.

cd /d "%~dp0Client_Python"
python main.py

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Co loi khi chay Python Client. Dang cai dat thu vien thieu...
    pip install -r requirements.txt
    python main.py
)

pause
