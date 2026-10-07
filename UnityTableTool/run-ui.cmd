@echo on
rem Launch the UnityTableTool WPF UI directly for development and UI tuning.
rem Date: 2026-10-08
setlocal
dotnet --version
dotnet restore "%~dp0src\TableTool.Gui\TableTool.Gui.csproj" --configfile "%~dp0NuGet.Config" -p:RestoreUseSkipNonexistentTargets=false
if errorlevel 1 goto :failed
dotnet run --project "%~dp0src\TableTool.Gui\TableTool.Gui.csproj" --configuration Debug --no-restore -- %*
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" goto :failed
exit /b 0

:failed
set "EXIT_CODE=%ERRORLEVEL%"
echo.
echo UI launch failed with exit code %EXIT_CODE%.
pause
exit /b %EXIT_CODE%
