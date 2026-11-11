@echo off
REM Proje kokundeki ortak .venv ile ConfigEditor'u calistirir (setupPythonEnvs.bat ile kurulur).
cd /d "%~dp0"
".venv\Scripts\python.exe" "src\ConfigEditor\main.py"
pause
