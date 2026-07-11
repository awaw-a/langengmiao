@echo off
setlocal
pushd "%~dp0"

set "DOTNET_ROOT=%CD%\.dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
set "DOTNET_CLI_HOME=%CD%\appdata\dotnet"
set "APPDATA=%CD%\appdata\roaming"
set "LOCALAPPDATA=%CD%\appdata\local"

if not exist "%DOTNET_ROOT%\dotnet.exe" (
    echo [Lanmian] Missing local .NET 8 runtime: %DOTNET_ROOT%\dotnet.exe
    echo Please install .NET 8 SDK or restore the project's .dotnet directory.
    pause
    popd
    exit /b 1
)

set "GODOT=%CD%\.godot-sdk\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe"
if not exist "%GODOT%" (
    echo [Lanmian] Missing Godot .NET executable: %GODOT%
    echo Please install Godot 4.6.1 .NET or restore the project's .godot-sdk directory.
    pause
    popd
    exit /b 1
)

if not exist "%APPDATA%" mkdir "%APPDATA%"
if not exist "%LOCALAPPDATA%" mkdir "%LOCALAPPDATA%"
if not exist "%DOTNET_CLI_HOME%" mkdir "%DOTNET_CLI_HOME%"

start "Lanmian" "%GODOT%" --path "%CD%"
popd

