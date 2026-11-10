@echo off
setlocal EnableExtensions DisableDelayedExpansion

REM ============================================================================
REM  EDIT THESE SETTINGS FOR DOUBLE-CLICK USE
REM ============================================================================
set "GIT_URL=https://github.com/aykut55/AlgoTrade.git"
set "DESTINATION=D:\Deploy\Project"

REM Leave empty to export the latest master. A commit SHA, tag, or branch may
REM also be entered, for example: 742d013  or  v1.2.0  or  develop
set "REQUESTED_REF=main"

REM 1 = wait for a key before closing, 0 = close immediately.
set "PAUSE_AT_END=1"

REM ============================================================================
REM  Git repository export
REM
REM  Usage:
REM    git-export.bat GIT_URL DESTINATION [REF]
REM
REM  Examples:
REM    git-export.bat https://github.com/company/project.git D:\Deploy\Project
REM    git-export.bat https://github.com/company/project.git D:\Deploy\Project 742d013
REM    git-export.bat https://github.com/company/project.git D:\Deploy\Project v1.2.0
REM
REM  REF is optional. When omitted, the latest origin/master is exported.
REM  Only tracked files are exported; the .git directory is not copied.
REM ============================================================================

REM Command-line arguments, when supplied, override the settings above.
if not "%~1"=="" set "GIT_URL=%~1"
if not "%~2"=="" set "DESTINATION=%~f2"
if not "%~3"=="" set "REQUESTED_REF=%~3"

if not defined GIT_URL goto :settings_error
if not defined DESTINATION goto :settings_error
if not defined REQUESTED_REF set "REQUESTED_REF=master"

where git.exe >nul 2>nul
if errorlevel 1 (
    echo ERROR: git.exe was not found in PATH.
    call :finish 1
    exit /b 1
)

where tar.exe >nul 2>nul
if errorlevel 1 (
    echo ERROR: tar.exe was not found in PATH.
    call :finish 1
    exit /b 1
)

REM Do not merge an export into a directory containing unrelated or stale files.
if exist "%DESTINATION%\NUL" (
    dir /a /b "%DESTINATION%" 2>nul | findstr . >nul
    if not errorlevel 1 (
        echo ERROR: Destination directory is not empty:
        echo   %DESTINATION%
        echo Use an empty directory or a new destination path.
        call :finish 1
        exit /b 1
    )
) else if exist "%DESTINATION%" (
    echo ERROR: Destination points to a file:
    echo   %DESTINATION%
    call :finish 1
    exit /b 1
)

set "TEMP_ROOT=%TEMP%\git-export-%RANDOM%-%RANDOM%"
set "TEMP_REPO=%TEMP_ROOT%\repo"
set "TEMP_ARCHIVE=%TEMP_ROOT%\export.tar"

mkdir "%TEMP_REPO%" >nul 2>nul
if errorlevel 1 (
    echo ERROR: Temporary directory could not be created:
    echo   %TEMP_REPO%
    call :finish 1
    exit /b 1
)

echo.
echo === Git export ===
echo Repository  : %GIT_URL%
echo Ref         : %REQUESTED_REF%
echo Destination : %DESTINATION%
echo.

pushd "%TEMP_REPO%" >nul
git init --quiet
if errorlevel 1 goto :git_error

git remote add origin "%GIT_URL%"
if errorlevel 1 goto :git_error

echo [1/3] Fetching %REQUESTED_REF%...
git fetch --quiet --depth=1 origin "%REQUESTED_REF%"
if errorlevel 1 (
    echo Shallow fetch failed; retrying with full repository metadata...
    git fetch --quiet origin
    if errorlevel 1 goto :git_error
    git rev-parse --verify "%REQUESTED_REF%^{commit}" >nul 2>nul
    if errorlevel 1 (
        git rev-parse --verify "origin/%REQUESTED_REF%^{commit}" >nul 2>nul
        if errorlevel 1 goto :ref_error
        set "CHECKOUT_REF=origin/%REQUESTED_REF%"
    ) else (
        set "CHECKOUT_REF=%REQUESTED_REF%"
    )
) else (
    set "CHECKOUT_REF=FETCH_HEAD"
)

call git checkout --quiet --detach "%%CHECKOUT_REF%%"
if errorlevel 1 goto :ref_error

for /f "delims=" %%C in ('git rev-parse HEAD') do set "EXPORTED_COMMIT=%%C"

echo [2/3] Creating clean source archive...
git archive --format=tar --output="%TEMP_ARCHIVE%" HEAD
if errorlevel 1 goto :git_error
popd >nul

if not exist "%DESTINATION%" mkdir "%DESTINATION%"
if errorlevel 1 goto :destination_error

echo [3/3] Extracting files...
tar.exe -xf "%TEMP_ARCHIVE%" -C "%DESTINATION%"
if errorlevel 1 goto :extract_error

rmdir /s /q "%TEMP_ROOT%" >nul 2>nul

echo.
echo Export completed.
echo Commit     : %EXPORTED_COMMIT%
echo Destination: %DESTINATION%
call :finish 0
exit /b 0

:git_error
popd >nul 2>nul
echo.
echo ERROR: Git operation failed.
goto :failed

:ref_error
popd >nul 2>nul
echo.
echo ERROR: Commit, tag, or branch could not be resolved: %REQUESTED_REF%
goto :failed

:destination_error
echo.
echo ERROR: Destination directory could not be created: %DESTINATION%
goto :failed

:extract_error
echo.
echo ERROR: Archive could not be extracted to: %DESTINATION%
goto :failed

:failed
echo Temporary files were preserved for inspection:
echo   %TEMP_ROOT%
call :finish 1
exit /b 1

:settings_error
echo ERROR: GIT_URL and DESTINATION must be configured at the top of this file.
echo.
echo Optional command-line usage:
echo   %~nx0 GIT_URL DESTINATION [COMMIT_OR_REF]
call :finish 2
exit /b 2

:finish
set "FINISH_CODE=%~1"
if "%PAUSE_AT_END%"=="1" (
    echo.
    echo Press any key to close...
    pause >nul
)
exit /b %FINISH_CODE%
