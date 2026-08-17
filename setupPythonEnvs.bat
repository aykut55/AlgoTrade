@echo off
REM ============================================================
REM  AlgoTrade - Python venv bootstrap
REM  Projedeki 3 ayri Python alt-projesi icin .venv'leri
REM  olusturur ve requirements.txt'lerini kurar. Tekrar
REM  calistirilabilir.
REM
REM  DELETE_EXISTING_VENV=true  -> var olan .venv'ler once silinip
REM  sifirdan kurulur (surum/paket karisikligi olmasin diye - bkz.
REM  imgui-bundle 1.92.5 vs 1.92.801/1.92.900 API farki). false
REM  yaparsan var olan .venv'ler atlanir (eskisi gibi).
REM ============================================================
setlocal
set "ROOT=%~dp0"
set "DELETE_EXISTING_VENV=true"

echo === AlgoTrade Python venv setup ^(DELETE_EXISTING_VENV=%DELETE_EXISTING_VENV%^) ===
echo.

call :setup_venv "%ROOT%inputs\python" 3.12
call :setup_venv "%ROOT%src\DearPyGuiDataPlotter" 3
call :setup_venv "%ROOT%src\DearImGuiBundleDataPlotter" 3

echo.
echo === Bitti ===
pause
exit /b 0

REM ------------------------------------------------------------
REM  %1 = proje dizini (tam yol)   %2 = py launcher versiyonu (orn. 3.12)
:setup_venv
setlocal
set "DIR=%~1"
set "PYVER=%~2"

echo --- %DIR% ---

if not exist "%DIR%" (
    echo   HATA: dizin bulunamadi, atlaniyor.
    goto :setup_venv_end
)

if /i "%DELETE_EXISTING_VENV%"=="true" if exist "%DIR%\.venv" (
    echo   .venv siliniyor ^(DELETE_EXISTING_VENV=true^)...
    rmdir /s /q "%DIR%\.venv"
)

if exist "%DIR%\.venv\Scripts\python.exe" (
    echo   .venv zaten var, atlaniyor.
) else (
    echo   .venv olusturuluyor ^(py -%PYVER%^)...
    py -%PYVER% -m venv "%DIR%\.venv"
    if errorlevel 1 (
        echo   HATA: venv olusturulamadi. Python %PYVER% kurulu mu?  ^(py -%PYVER% --version^)
        goto :setup_venv_end
    )
)

if not exist "%DIR%\requirements.txt" (
    echo   requirements.txt yok, paket kurulumu atlaniyor.
    goto :setup_venv_end
)

echo   pip + requirements kuruluyor...
"%DIR%\.venv\Scripts\python.exe" -m pip install --upgrade pip
"%DIR%\.venv\Scripts\python.exe" -m pip install -r "%DIR%\requirements.txt"
if errorlevel 1 (
    echo   HATA: paket kurulumu basarisiz.
    goto :setup_venv_end
)

echo   OK.

:setup_venv_end
echo.
endlocal
goto :eof
