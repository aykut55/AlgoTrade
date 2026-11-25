@echo off
set SCRIPT_DIR=%~dp0
set DEFAULT_IN=%SCRIPT_DIR%..\..\outputs\logs\SingleTraderPerformans.csv
set DEFAULT_OUT=%SCRIPT_DIR%..\..\outputs\logs\SingleTraderPerformans_filtered.csv
python "%SCRIPT_DIR%filter_performans.py" -i "%DEFAULT_IN%" -o "%DEFAULT_OUT%" %*
pause
