@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM Asil deploy ikinci calismada log dosyasina yonlendirilir. Ilk calisma
REM ciktisini hem anlik ekrana verir hem deploy.log dosyasina yazar.
REM Tamamlaninca basari/hata fark etmeksizin kullaniciyi bekler.
if /I not "%~1"=="--logged" (
    set "DEPLOY_SCRIPT=%~f0"
    set "DEPLOY_LOG=%~dp0deploy.log"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$cmd='call '+[char]34+$env:DEPLOY_SCRIPT+[char]34+' --logged'; & $env:ComSpec /d /c $cmd 2>&1 | Tee-Object -FilePath $env:DEPLOY_LOG; exit $LASTEXITCODE"
    set "DEPLOY_EXIT=!ERRORLEVEL!"
    echo.
    echo Ayrintili cikti: %~dp0deploy.log
    echo Kapatmak icin bir tusa basin...
    pause >nul
    exit /b !DEPLOY_EXIT!
)

REM ================================================================
REM  AlgoTrade runtime deployment
REM
REM  Hedefi sadece asagidaki satirdan degistirin. Bu klasor yalnizca
REM  deploy icin kullanilmalidir; kaynak proje klasoru verilmemelidir.
REM ================================================================
set "DESTINATION=D:\Aykut\Deploy\AlgoTrade"

set "ROOT=%~dp0"
set "PROJECT=%ROOT%AlgoTrade.Console\AlgoTrade.Console.csproj"
set "FRAMEWORK=net8.0"
set "APP_OUTPUT=%DESTINATION%\AlgoTrade.Console\bin\Release\%FRAMEWORK%"

echo.
echo === AlgoTrade deploy ===
echo Kaynak : %ROOT%
echo Hedef  : %DESTINATION%
echo.

if not exist "%PROJECT%" (
    echo HATA: Console projesi bulunamadi:
    echo   %PROJECT%
    exit /b 1
)

REM Yanlis bir ayarla kaynak agacinin ustune yazilmasini engelle.
for %%I in ("%ROOT%.") do set "ROOT_FULL=%%~fI"
for %%I in ("%DESTINATION%") do set "DEST_FULL=%%~fI"
if /I "%ROOT_FULL%"=="%DEST_FULL%" (
    echo HATA: DESTINATION kaynak proje klasoru olamaz.
    exit /b 1
)
echo(%DEST_FULL%| findstr /I /B /L /C:"%ROOT_FULL%\" >nul
if not errorlevel 1 (
    echo HATA: DESTINATION kaynak proje agacinin icinde olamaz.
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo HATA: dotnet komutu bulunamadi. .NET 8 SDK kurulmalidir.
    exit /b 1
)

if not exist "%DESTINATION%" mkdir "%DESTINATION%"
if errorlevel 1 (
    echo DEPLOY BASARISIZ: Hedef klasor olusturulamadi.
    exit /b 1
)

REM Sadece uygulama binary klasoru temizlenir. Deploy icinde uygulamanin
REM olusturdugu outputs ve kullanicinin degistirdigi configler korunur.
if exist "%APP_OUTPUT%" rmdir /s /q "%APP_OUTPUT%"

echo [1/6] Menu uygulamasi yayinlaniyor...
dotnet publish "%PROJECT%" -c Release -f "%FRAMEWORK%" --self-contained false -o "%APP_OUTPUT%"
if errorlevel 1 (
    echo DEPLOY BASARISIZ: Uygulama yayinlanamadi.
    exit /b 1
)

echo [2/6] Config ve C# scriptleri kopyalaniyor...
robocopy "%ROOT%inputs\configs" "%DESTINATION%\inputs\configs" /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP /XD "__pycache__" /XF "*.pyc" "*.pyo"
if errorlevel 8 (
    echo DEPLOY BASARISIZ: Config dosyalari kopyalanamadi.
    exit /b 1
)
robocopy "%ROOT%inputs\scripts" "%DESTINATION%\inputs\scripts" /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP /XD "__pycache__" /XF "*.pyc" "*.pyo"
if errorlevel 8 (
    echo DEPLOY BASARISIZ: Script dosyalari kopyalanamadi.
    exit /b 1
)

REM Runtime veri klasorlerinin iskeleti ve kalici JSON ayarlari kopyalanir.
REM Uretilmis NPZ/view bundle'lari yeni ortamda uygulama tarafindan uretilir.
robocopy "%ROOT%inputs\python" "%DESTINATION%\inputs\python" /E /PURGE /R:2 /W:1 /NFL /NDL /NJH /NJS /NP /XD ".venv" "__pycache__" "samples" /XF "*.npz" "*.view.json" "*.pyc" "*.pyo"
if errorlevel 8 (
    echo DEPLOY BASARISIZ: Python runtime ayarlari kopyalanamadi.
    exit /b 1
)

echo [3/6] Python plotter kaynaklari kopyalaniyor...
robocopy "%ROOT%src\PythonPlotter" "%DESTINATION%\src\PythonPlotter" /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP /XD ".venv" "__pycache__" "docs" "runtime_commands" /XF "*.pyc" "*.pyo" "*.npz" "*.view.json" "setup.bat"
if errorlevel 8 exit /b 1
robocopy "%ROOT%src\DearPyGuiDataPlotter" "%DESTINATION%\src\DearPyGuiDataPlotter" /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP /XD ".venv" "__pycache__" "docs" "runtime_commands" /XF "*.pyc" "*.pyo" "*.npz" "*.view.json" "setup.bat"
if errorlevel 8 exit /b 1
robocopy "%ROOT%src\DearImGuiBundleDataPlotter" "%DESTINATION%\src\DearImGuiBundleDataPlotter" /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP /XD ".venv" "__pycache__" "docs" "runtime_commands" /XF "*.pyc" "*.pyo" "*.npz" "*.view.json" "setup.bat"
if errorlevel 8 (
    echo DEPLOY BASARISIZ: Python plotter kaynaklari kopyalanamadi.
    exit /b 1
)
robocopy "%ROOT%src\ConfigEditor" "%DESTINATION%\src\ConfigEditor" /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP /XD ".venv" "__pycache__"
if errorlevel 8 (
    echo DEPLOY BASARISIZ: ConfigEditor kaynaklari kopyalanamadi.
    exit /b 1
)
copy /Y "%ROOT%runConfigEditor.bat" "%DESTINATION%\runConfigEditor.bat" >nul

echo [4/6] Ortak Python sanal ortami kopyalaniyor...
if not exist "%ROOT%.venv\Scripts\python.exe" (
    echo HATA: Kok .venv bulunamadi. Once setupPythonEnvs.bat calistirilmalidir.
    exit /b 1
)
robocopy "%ROOT%.venv" "%DESTINATION%\.venv" /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP /XD "__pycache__" /XF "*.pyc" "*.pyo"
if errorlevel 8 (
    echo DEPLOY BASARISIZ: Python sanal ortami kopyalanamadi.
    exit /b 1
)

echo [5/6] Runtime klasorleri hazirlaniyor...
for %%D in ("%DESTINATION%\outputs" "%DESTINATION%\outputs\logs" "%DESTINATION%\outputs\opt" "%DESTINATION%\outputs\scan" "%DESTINATION%\inputs\python\dearPyGuiDataPlotter" "%DESTINATION%\inputs\python\dearImGuiBundleDataPlotter" "%DESTINATION%\inputs\python\pythonPlotter" "%DESTINATION%\inputs\python\offlineReplay") do if not exist "%%~D" mkdir "%%~D"

echo [6/6] Baslatma dosyasi yaziliyor...
>"%DESTINATION%\RunAlgoTrade.bat" echo @echo off
>>"%DESTINATION%\RunAlgoTrade.bat" echo cd /d "%%~dp0"
>>"%DESTINATION%\RunAlgoTrade.bat" echo "%%~dp0AlgoTrade.Console\bin\Release\%FRAMEWORK%\AlgoTrade.Console.exe"

echo.
echo Deploy tamamlandi.
echo Calistirmak icin:
echo   %DESTINATION%\RunAlgoTrade.bat
echo.
exit /b 0
