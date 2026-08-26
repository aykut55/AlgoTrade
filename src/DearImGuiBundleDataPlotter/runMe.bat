@echo off
REM Proje kokundeki ortak .venv ile main.py'yi calistirir (setupPythonEnvs.bat ile kurulur).
cd /d "%~dp0"
"%~dp0..\..\.venv\Scripts\python.exe" "%~dp0main.py"
pause
