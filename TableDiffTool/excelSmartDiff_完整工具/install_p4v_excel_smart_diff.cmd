@echo off
setlocal

set SCRIPT_DIR=%~dp0
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%install_p4v_excel_smart_diff.ps1" %*
set EXITCODE=%ERRORLEVEL%

echo.
if "%EXITCODE%"=="0" (
    echo Excel Smart Diff P4V settings installed.
) else (
    echo Excel Smart Diff P4V settings failed with exit code %EXITCODE%.
)

if "%EXCELSMARTDIFF_NO_PAUSE%"=="" pause
exit /b %EXITCODE%
