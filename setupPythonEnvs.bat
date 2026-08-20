@echo off
REM ============================================================
REM  AlgoTrade - Python venv bootstrap
REM  Tum Python alt-projeleri (inputs/python, src/DearPyGuiDataPlotter,
REM  src/DearImGuiBundleDataPlotter) artik tek, ortak .venv kullanir:
REM  D:\SageProjects\AlgoTrade\.venv
REM
REM  DELETE_EXISTING_VENV=true  -> var olan .venv once silinip
REM  sifirdan kurulur (surum/paket karisikligi olmasin diye - bkz.
REM  imgui-bundle 1.92.5 vs 1.92.801 API farki, artik tek surume
REM  sabitlendi: 1.92.801). false yaparsan var olan .venv atlanir.
REM ============================================================
setlocal
set "ROOT=%~dp0"
set "VENV_DIR=%ROOT%.venv"
set "PYVER=3.14"
set "DELETE_EXISTING_VENV=true"

echo === AlgoTrade Python venv setup ^(DELETE_EXISTING_VENV=%DELETE_EXISTING_VENV%^) ===
echo.

if /i "%DELETE_EXISTING_VENV%"=="true" if exist "%VENV_DIR%" (
    echo   .venv siliniyor ^(DELETE_EXISTING_VENV=true^)...
    rmdir /s /q "%VENV_DIR%"
)

if exist "%VENV_DIR%\Scripts\python.exe" (
    echo   .venv zaten var, atlaniyor.
) else (
    echo   .venv olusturuluyor ^(py -%PYVER%^)...
    py -%PYVER% -m venv "%VENV_DIR%"
    if errorlevel 1 (
        echo   HATA: venv olusturulamadi. Python %PYVER% kurulu mu?  ^(py -%PYVER% --version^)
        goto :end
    )
)

if not exist "%ROOT%requirements.txt" (
    echo   HATA: %ROOT%requirements.txt bulunamadi.
    goto :end
)

echo   pip + requirements kuruluyor...
"%VENV_DIR%\Scripts\python.exe" -m pip install --upgrade pip
"%VENV_DIR%\Scripts\python.exe" -m pip install -r "%ROOT%requirements.txt"
if errorlevel 1 (
    echo   HATA: paket kurulumu basarisiz.
    goto :end
)

echo   OK.

:end
echo.
echo === Bitti ===
pause
exit /b 0
