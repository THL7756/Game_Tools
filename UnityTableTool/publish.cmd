@echo off
setlocal
set "BUILD_ROOT=%TEMP%\UnityTableTool-build"
set "DOTNET_CLI_HOME=%BUILD_ROOT%\.dotnet-home"
set "NUGET_PACKAGES=%BUILD_ROOT%\.nuget\packages"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"

dotnet restore "%~dp0UnityTableTool.sln" --configfile "%~dp0NuGet.Config"
if errorlevel 1 exit /b %errorlevel%
dotnet test "%~dp0UnityTableTool.sln" -c Release --no-restore
if errorlevel 1 exit /b %errorlevel%
dotnet publish "%~dp0src\TableTool.Gui\TableTool.Gui.csproj" -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false -o "%~dp0release\UnityTableTool-2.4.0"
if errorlevel 1 exit /b %errorlevel%
powershell -NoProfile -Command "Compress-Archive -Path '%~dp0release\UnityTableTool-2.4.0\*' -DestinationPath '%~dp0release\UnityTableTool-2.4.0-win-x64.zip' -Force"
if errorlevel 1 exit /b %errorlevel%
echo Published to %~dp0release\UnityTableTool-2.4.0
echo Packaged to %~dp0release\UnityTableTool-2.4.0-win-x64.zip
exit /b 0
