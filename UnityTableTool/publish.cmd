@echo on
rem Restore, publish, and package UnityTableTool.
rem Date: 2026-10-08
rem SDK policy: global.json prefers the installed .NET 8 SDK and rolls forward when needed.
rem The window stays open on success or failure so the result is visible.
setlocal
set "BUILD_ROOT=%~dp0.build"
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
dotnet restore "%~dp0UnityTableTool.sln" --configfile "%~dp0NuGet.Config" -p:RestoreUseSkipNonexistentTargets=false
if errorlevel 1 goto :failed
if exist "%~dp0release\UnityTableTool-2.4.0" rmdir /s /q "%~dp0release\UnityTableTool-2.4.0"
if exist "%~dp0release\UnityTableTool-2.4.0-win-x64.zip" del /q "%~dp0release\UnityTableTool-2.4.0-win-x64.zip"
dotnet publish "%~dp0src\TableTool.Gui\TableTool.Gui.csproj" -c Release -r win-x64 --self-contained true --no-restore -p:PublishTrimmed=false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o "%~dp0release\UnityTableTool-2.4.0"
if errorlevel 1 goto :failed
powershell -NoProfile -Command "Compress-Archive -Path '%~dp0release\UnityTableTool-2.4.0\*' -DestinationPath '%~dp0release\UnityTableTool-2.4.0-win-x64.zip' -Force"
if errorlevel 1 goto :failed
echo Published to %~dp0release\UnityTableTool-2.4.0
echo Packaged to %~dp0release\UnityTableTool-2.4.0-win-x64.zip
if "%SDK_FALLBACK%"=="1" popd
pause
exit /b 0

:failed
set "EXIT_CODE=%ERRORLEVEL%"
if "%SDK_FALLBACK%"=="1" popd
echo.
echo Publish failed with exit code %EXIT_CODE%.
pause
exit /b %EXIT_CODE%
