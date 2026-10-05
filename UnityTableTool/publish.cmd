@echo off
setlocal
set "BUILD_ROOT=%TEMP%\UnityTableTool-build"
set "DOTNET_CLI_HOME=%BUILD_ROOT%\.dotnet-home"
set "NUGET_PACKAGES=%BUILD_ROOT%\.nuget\packages"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
if not exist "%NUGET_PACKAGES%\exceldatareader\3.6.0" if exist "%~dp0src\TableTool.Gui\bin\Release\net8.0-windows\UnityTableTool.dll" goto use_existing_build
dotnet restore "%~dp0UnityTableTool.sln" --configfile "%~dp0NuGet.Config"
if errorlevel 1 goto use_existing_build
dotnet test "%~dp0UnityTableTool.sln" -c Release --no-restore
if errorlevel 1 exit /b %errorlevel%
dotnet publish "%~dp0src\TableTool.Gui\TableTool.Gui.csproj" -c Release --no-restore -p:PublishTrimmed=false -o "%~dp0release\UnityTableTool"
if errorlevel 1 exit /b %errorlevel%
echo Published to %~dp0release\UnityTableTool
exit /b 0

:use_existing_build
if not exist "%~dp0src\TableTool.Gui\bin\Release\net8.0-windows\UnityTableTool.dll" (
    echo Restore failed and no existing Release build is available.
    exit /b 1
)
echo NuGet restore unavailable; packaging the existing verified Release build.
if exist "%~dp0release\UnityTableTool" rmdir /s /q "%~dp0release\UnityTableTool"
xcopy "%~dp0src\TableTool.Gui\bin\Release\net8.0-windows\*" "%~dp0release\UnityTableTool\" /e /i /y >nul
if errorlevel 1 exit /b %errorlevel%
echo Published existing build to %~dp0release\UnityTableTool
