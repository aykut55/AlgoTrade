@echo off
REM ============================================================
REM  AlgoTrade - Python surumu kurulumu
REM  Asagidaki PYVER'i istedigin surume gore degistirip calistir.
REM  setupPythonEnvs.bat bu surumu "py -%PYVER%" ile ortak .venv'i
REM  olusturmak icin kullanir - ikisindeki PYVER ayni olmali.
REM
REM  "py" komutu artik Python Install Manager (pymanager) uzerinden
REM  calisiyor (bkz. https://github.com/python/pymanager). "py" hic
REM  kurulu degilse bu script once winget ile onu kurmayi dener.
REM ============================================================
setlocal
set "PYVER=3.14"

echo === Python %PYVER% kurulum kontrolu ===
echo.

where py >nul 2>&1
if errorlevel 1 (
    echo   'py' komutu bulunamadi, Python Install Manager kuruluyor ^(winget^)...
    winget install --id 9NQ7512CXL7T --accept-package-agreements --accept-source-agreements
    if errorlevel 1 (
        echo   HATA: Python Install Manager otomatik kurulamadi.
        echo   Elle kurmak icin: https://www.python.org/downloads/windows/
        goto :end
    )
    echo.
)

py -%PYVER% --version >nul 2>&1
if not errorlevel 1 (
    echo   Python %PYVER% zaten kurulu:
    py -%PYVER% --version
    goto :end
)

echo   Python %PYVER% kuruluyor...
py install -y %PYVER%
if errorlevel 1 (
    echo   HATA: Python %PYVER% kurulamadi.
    goto :end
)

echo.
echo   OK. Kurulu Python surumleri:
py list

:end
echo.
echo === Bitti ===
pause
exit /b 0
