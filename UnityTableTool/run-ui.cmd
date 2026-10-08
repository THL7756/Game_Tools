@echo on
rem Launch the UnityTableTool WPF UI directly for development and UI tuning.
rem Date: 2026-10-08
rem SDK policy: global.json prefers the installed .NET 8 SDK and rolls forward when needed.
setlocal
set "DOTNET_CLI_HOME=%~dp0.dotnet-home"
set "NUGET_PACKAGES=%~dp0.nuget\packages"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "SDK_FALLBACK=0"
dotnet --version >nul 2>&1
if errorlevel 1 (
    set "SDK_FALLBACK=1"
    pushd "%TEMP%"
)
dotnet --version
dotnet restore "%~dp0src\TableTool.Gui\TableTool.Gui.csproj" --configfile "%~dp0NuGet.Config" -p:RestoreUseSkipNonexistentTargets=false
set "RESTORE_EXIT_CODE=%ERRORLEVEL%"
if not "%RESTORE_EXIT_CODE%"=="0" goto :restore_failed

dotnet build "%~dp0src\TableTool.Gui\TableTool.Gui.csproj" --configuration Debug --no-restore
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
if not "%BUILD_EXIT_CODE%"=="0" goto :build_failed

set "APP_EXE=%~dp0src\TableTool.Gui\bin\Debug\net8.0-windows\UnityTableTool.exe"
if not exist "%APP_EXE%" goto :missing_app
"%APP_EXE%" %*
set "RUN_EXIT_CODE=%ERRORLEVEL%"
if "%SDK_FALLBACK%"=="1" popd
if not "%RUN_EXIT_CODE%"=="0" goto :run_failed
exit /b 0

:restore_failed
set "EXIT_CODE=%RESTORE_EXIT_CODE%"
goto :failed

:build_failed
set "EXIT_CODE=%BUILD_EXIT_CODE%"
goto :failed

:missing_app
set "EXIT_CODE=2"
goto :failed

:run_failed
set "EXIT_CODE=%RUN_EXIT_CODE%"

:failed
if "%SDK_FALLBACK%"=="1" popd
echo.
echo UI launch failed with exit code %EXIT_CODE%.
pause
exit /b %EXIT_CODE%
