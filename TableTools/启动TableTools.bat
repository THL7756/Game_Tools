@echo off
rem Purpose: create/reuse the project environment and launch TableTools with visible diagnostics.
rem Last modified: 2026-10-07
rem Author: Codex
setlocal EnableExtensions
chcp 65001 >nul
pushd "%~dp0" || goto :failed_directory
set "PYTHONDONTWRITEBYTECODE=1"
set "PYTHONUTF8=1"
rem Clear inherited Qt overrides so a previous development session cannot force an offscreen backend.
set "QT_QPA_PLATFORM="
set "QT_PLUGIN_PATH="
set "QT_QPA_PLATFORM_PLUGIN_PATH="
set "QML2_IMPORT_PATH="
set "VENV_PYTHON=%CD%\.venv\Scripts\python.exe"
set "BOOTSTRAP="
set "PROJECT_STARTUP_LOG=%CD%\tabletools_startup.log"
set "STARTUP_LOG=%TEMP%\TableToolsStartup_%RANDOM%_%RANDOM%.log"

> "%STARTUP_LOG%" echo TableTools launcher started at %DATE% %TIME%
copy /Y "%STARTUP_LOG%" "%PROJECT_STARTUP_LOG%" >nul 2>&1

if exist "%VENV_PYTHON%" goto :check_environment

where py >nul 2>&1
if not errorlevel 1 py -3 -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 11) else 1)" >nul 2>&1
if not errorlevel 1 set "BOOTSTRAP=py -3"

if not defined BOOTSTRAP (
    where python >nul 2>&1
    if not errorlevel 1 python -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 11) else 1)" >nul 2>&1
    if not errorlevel 1 set "BOOTSTRAP=python"
)

if not defined BOOTSTRAP goto :missing_python

echo Creating the local Python environment...
echo Creating virtual environment...>> "%STARTUP_LOG%"
%BOOTSTRAP% -m venv ".venv" >> "%STARTUP_LOG%" 2>&1
if errorlevel 1 goto :failed_setup

:check_environment
if not exist "%VENV_PYTHON%" goto :failed_setup
"%VENV_PYTHON%" -m pip --version >> "%STARTUP_LOG%" 2>&1
if errorlevel 1 goto :failed_setup

"%VENV_PYTHON%" -c "import PySide6, openpyxl, xlrd" >nul 2>&1
if errorlevel 1 (
    echo Installing missing dependencies...
    echo Installing dependencies...>> "%STARTUP_LOG%"
    "%VENV_PYTHON%" -m pip install --disable-pip-version-check -r requirements.txt >> "%STARTUP_LOG%" 2>&1
    if errorlevel 1 goto :failed_setup
)

echo Starting TableTools...
echo Starting application...>> "%STARTUP_LOG%"
echo Python: %VENV_PYTHON%>> "%STARTUP_LOG%"
"%VENV_PYTHON%" --version>> "%STARTUP_LOG%" 2>&1
"%VENV_PYTHON%" run.py >> "%STARTUP_LOG%" 2>&1
set "EXIT_CODE=%ERRORLEVEL%"
copy /Y "%STARTUP_LOG%" "%PROJECT_STARTUP_LOG%" >nul 2>&1
if "%EXIT_CODE%"=="0" exit /b 0

echo.
echo TableTools failed to start or exited with code %EXIT_CODE%.
echo The complete startup log is %PROJECT_STARTUP_LOG%:
type "%STARTUP_LOG%"
pause
exit /b %EXIT_CODE%

:missing_python
echo Python 3.11 or newer was not found. Install Python from:
echo https://www.python.org/downloads/windows/
start "" "https://www.python.org/downloads/windows/"
pause
exit /b 1

:failed_directory
echo Could not open the TableTools project directory.
pause
exit /b 1

:failed_setup
echo.
echo TableTools setup failed. The complete setup log is:
type "%STARTUP_LOG%"
pause
exit /b 1
