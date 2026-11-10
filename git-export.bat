@echo off
setlocal EnableExtensions DisableDelayedExpansion

REM ============================================================================
REM  EDIT THESE SETTINGS FOR DOUBLE-CLICK USE
REM ============================================================================
set "GIT_URL=https://github.com/aykut55/AlgoTrade.git"
set "DESTINATION=D:\Deploy\Project"

REM Branch, tag, or commit SHA. This repository's default branch is main.
set "REQUESTED_REF=main"

REM 1 = wait for a key before closing, 0 = close immediately.
set "PAUSE_AT_END=1"

REM ============================================================================
REM  Optional command-line usage:
REM    git-export.bat GIT_URL DESTINATION [COMMIT_OR_REF] [MODE]
REM
REM  MODE 1 = clean export without .git
REM  MODE 2 = shallow clone with .git and origin
REM ============================================================================

REM Command-line arguments, when supplied, override the settings above.
if not "%~1"=="" set "GIT_URL=%~1"
if not "%~2"=="" set "DESTINATION=%~f2"
if not "%~3"=="" set "REQUESTED_REF=%~3"
set "SELECTED_MODE=%~4"

if not defined GIT_URL goto :settings_error
if not defined DESTINATION goto :settings_error
if not defined REQUESTED_REF set "REQUESTED_REF=main"

where git.exe >nul 2>nul
if errorlevel 1 goto :git_missing

if not defined SELECTED_MODE goto :show_menu
if "%SELECTED_MODE%"=="1" goto :clean_export
if "%SELECTED_MODE%"=="2" goto :shallow_clone
goto :invalid_mode

:show_menu
cls
echo ============================================================
echo Git deployment
echo ============================================================
echo Repository  : %GIT_URL%
echo Ref         : %REQUESTED_REF%
echo Destination : %DESTINATION%
echo.
echo   [1] Export source files ^(without .git^)
echo   [2] Clone repository    ^(with .git, shallow^)
echo.
set "SELECTED_MODE=1"
set /p "SELECTED_MODE=Selection : 1"
if "%SELECTED_MODE%"=="1" goto :clean_export
if "%SELECTED_MODE%"=="2" goto :shallow_clone
goto :invalid_mode

:clean_export
where tar.exe >nul 2>nul
if errorlevel 1 goto :tar_missing
call :ensure_empty_destination
if errorlevel 1 goto :destination_not_empty

set "TEMP_ROOT=%TEMP%\git-export-%RANDOM%-%RANDOM%"
set "TEMP_REPO=%TEMP_ROOT%\repo"
set "TEMP_ARCHIVE=%TEMP_ROOT%\export.tar"
mkdir "%TEMP_ROOT%" >nul 2>nul
if errorlevel 1 goto :temp_error

echo.
echo [1/3] Fetching selected revision...
call :create_shallow_clone "%TEMP_REPO%"
if errorlevel 1 goto :clean_fetch_error

for /f "delims=" %%C in ('git -C "%TEMP_REPO%" rev-parse HEAD') do set "DEPLOYED_COMMIT=%%C"

echo [2/3] Creating clean archive...
git -C "%TEMP_REPO%" archive --format=tar --output="%TEMP_ARCHIVE%" HEAD
if errorlevel 1 goto :archive_error

if not exist "%DESTINATION%" mkdir "%DESTINATION%"
if errorlevel 1 goto :destination_create_error

echo [3/3] Extracting source files...
tar.exe -xf "%TEMP_ARCHIVE%" -C "%DESTINATION%"
if errorlevel 1 goto :extract_error

rmdir /s /q "%TEMP_ROOT%" >nul 2>nul
echo.
echo Kod dosyalari indirildi.
echo Commit     : %DEPLOYED_COMMIT%
echo Destination: %DESTINATION%
echo Git data   : not included
call :finish 0
exit /b 0

:shallow_clone
call :ensure_empty_destination
if errorlevel 1 goto :destination_not_empty

echo.
echo [1/1] Creating shallow clone...
call :create_shallow_clone "%DESTINATION%"
if errorlevel 1 goto :clone_error

for /f "delims=" %%C in ('git -C "%DESTINATION%" rev-parse HEAD') do set "DEPLOYED_COMMIT=%%C"
echo.
echo Git clone tamamlandi.
echo Commit     : %DEPLOYED_COMMIT%
echo Destination: %DESTINATION%
echo Git data   : %DESTINATION%\.git
echo History    : shallow (depth 1)
call :finish 0
exit /b 0

:create_shallow_clone
set "CLONE_DESTINATION=%~1"
git clone --quiet --no-local --depth 1 --no-checkout "%GIT_URL%" "%CLONE_DESTINATION%"
if errorlevel 1 exit /b 1

REM Fetching the requested ref separately also supports a commit SHA that is
REM different from the remote's default branch.
git -C "%CLONE_DESTINATION%" fetch --quiet --depth 1 origin "%REQUESTED_REF%"
if errorlevel 1 exit /b 1

git -C "%CLONE_DESTINATION%" checkout --quiet "%REQUESTED_REF%" 2>nul
if not errorlevel 1 exit /b 0
git -C "%CLONE_DESTINATION%" checkout --quiet --detach FETCH_HEAD
exit /b %ERRORLEVEL%

:ensure_empty_destination
if exist "%DESTINATION%\NUL" (
    dir /a /b "%DESTINATION%" 2>nul | findstr . >nul
    if not errorlevel 1 exit /b 1
    exit /b 0
)
if exist "%DESTINATION%" exit /b 1
exit /b 0

:destination_not_empty
echo.
echo ERROR: Destination must be an empty directory or a new path:
echo   %DESTINATION%
goto :failed

:clean_fetch_error
echo.
echo ERROR: Selected revision could not be fetched for clean export.
echo Temporary files were preserved at:
echo   %TEMP_ROOT%
goto :failed

:archive_error
echo.
echo ERROR: Clean source archive could not be created.
goto :preserve_temp

:extract_error
echo.
echo ERROR: Clean source archive could not be extracted.
goto :preserve_temp

:destination_create_error
echo.
echo ERROR: Destination could not be created: %DESTINATION%
goto :preserve_temp

:preserve_temp
echo Temporary files were preserved at:
echo   %TEMP_ROOT%
goto :failed

:clone_error
echo.
echo ERROR: Shallow clone or checkout failed for: %REQUESTED_REF%
echo A partial clone may remain at:
echo   %DESTINATION%
goto :failed

:temp_error
echo ERROR: Temporary directory could not be created: %TEMP_ROOT%
goto :failed

:git_missing
echo ERROR: git.exe was not found in PATH.
goto :failed

:tar_missing
echo ERROR: tar.exe was not found in PATH.
goto :failed

:invalid_mode
echo ERROR: Invalid selection. Enter 1 or 2.
goto :failed

:settings_error
echo ERROR: GIT_URL and DESTINATION must be configured at the top of this file.
goto :failed

:failed
call :finish 1
exit /b 1

:finish
set "FINISH_CODE=%~1"
if "%PAUSE_AT_END%"=="1" (
    echo.
    echo Press any key to close...
    pause >nul
)
exit /b %FINISH_CODE%
