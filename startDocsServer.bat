@echo off
REM ============================================================
REM  AlgoTrade - MkDocs canli onizleme sunucusu
REM  docs/ altindaki markdown dosyalarini http://127.0.0.1:8000
REM  adresinde tarayici uzerinden gezinilebilir hale getirir.
REM  Dosya kaydettikce sayfa otomatik yenilenir. Durdurmak icin
REM  bu pencerede Ctrl+C.
REM
REM  Bagimliliklar (mkdocs, mkdocs-material) projenin ortak
REM  .venv'ine kurulu - bkz. setupPythonEnvs.bat.
REM ============================================================
setlocal
set "ROOT=%~dp0"
set "VENV_PY=%ROOT%.venv\Scripts\python.exe"

if not exist "%VENV_PY%" (
    echo   HATA: .venv bulunamadi ^(%VENV_PY%^).
    echo   Once setupPythonEnvs.bat calistirip venv'i kurun, sonra:
    echo     %VENV_PY% -m pip install mkdocs mkdocs-material
    goto :end
)

echo === AlgoTrade Docs sunucusu baslatiliyor ===
echo   http://127.0.0.1:8000
echo   Durdurmak icin Ctrl+C
echo.

"%VENV_PY%" -m mkdocs serve -a 127.0.0.1:8000

:end
pause
