@echo off
setlocal EnableExtensions

rem Start from the script directory and walk upwards until .gitignore is found.
set "SEARCH_DIR=%~dp0"

:find_repo_root
if exist "%SEARCH_DIR%.gitignore" (
    set "REPO_ROOT=%SEARCH_DIR%"
    goto repo_root_found
)

for %%I in ("%SEARCH_DIR%..") do set "PARENT_DIR=%%~fI\"
if /I "%PARENT_DIR%"=="%SEARCH_DIR%" goto repo_root_not_found
set "SEARCH_DIR=%PARENT_DIR%"
goto find_repo_root

:repo_root_not_found
echo ERROR: .gitignore could not be found in this directory or any parent directory.
echo No files or directories were removed.
exit /b 1

:repo_root_found

echo ============================================================
echo AlgoTrade - Build Output Cleanup
echo Repository: %REPO_ROOT%
echo ============================================================

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference = 'Stop';" ^
  "$root = [System.IO.Path]::GetFullPath('%REPO_ROOT%');" ^
  "$rootWithSlash = $root.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar;" ^
  "$directoryNames = @('bin', 'obj', 'TestResults', '__pycache__', '.tmp', '.temp');" ^
  "$isAllowed = { param($path) $segments = $path -split '[\\/]'; return ($segments -notcontains '.venv') -and ($segments -notcontains '.git') };" ^
  "$targets = @(Get-ChildItem -LiteralPath $root -Directory -Recurse -Force -ErrorAction SilentlyContinue | Where-Object { ($directoryNames -contains $_.Name) -and (& $isAllowed $_.FullName) });" ^
  "$pythonFiles = @(Get-ChildItem -LiteralPath $root -File -Recurse -Force -ErrorAction SilentlyContinue | Where-Object { ($_.Extension -in '.pyc', '.pyo') -and (& $isAllowed $_.FullName) });" ^
  "$rootTargets = @('.vs', 'artifacts') | ForEach-Object { Join-Path $root $_ } | Where-Object { Test-Path -LiteralPath $_ -PathType Container };" ^
  "$allTargets = @($targets.FullName) + @($pythonFiles.FullName) + @($rootTargets) | Select-Object -Unique | Sort-Object { $_.Length } -Descending;" ^
  "foreach ($target in $allTargets) {" ^
  "  $resolved = [System.IO.Path]::GetFullPath($target);" ^
  "  if (-not $resolved.StartsWith($rootWithSlash, [System.StringComparison]::OrdinalIgnoreCase)) { throw ('Unsafe cleanup target: ' + $resolved) };" ^
  "  Write-Host ('Removing: ' + $resolved);" ^
  "  Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction Stop;" ^
  "}" ^
  "Write-Host ('Cleanup complete. Removed ' + $allTargets.Count + ' directories.')"

if errorlevel 1 (
    echo.
    echo Cleanup failed. Source files were not targeted.
    exit /b 1
)

echo.
echo Build outputs cleaned successfully.
exit /b 0
