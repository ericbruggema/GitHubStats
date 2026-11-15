@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Build GitHub Stats

rem ---------------------------------------------------------------------------
rem  build.bat          -> dist\GitHubStats.exe  one portable Windows program, nothing to install
rem                        (the .NET runtime is built in, about 70-150 MB)
rem  build.bat small    -> dist\GitHubStats.exe  smaller, but the PC that runs it needs the
rem                        ".NET Desktop Runtime" (same major version as the SDK you build with)
rem ---------------------------------------------------------------------------

set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_NOLOGO=1

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] The .NET SDK was not found. Install the .NET SDK 8 or newer from https://dotnet.microsoft.com/download
    goto :fail
)

set SDKMAJOR=
for /f "tokens=1 delims=." %%v in ('dotnet --version') do set SDKMAJOR=%%v
if not defined SDKMAJOR (
    echo [ERROR] Could not determine the .NET SDK version. Run "dotnet --version" to see what is wrong.
    goto :fail
)
if %SDKMAJOR% LSS 8 (
    echo [ERROR] .NET SDK %SDKMAJOR% found, but version 8 or newer is needed.
    goto :fail
)

rem A .NET 11 WPF build crashes at start (BadImageFormatException), so newer SDKs target .NET 10.
if %SDKMAJOR% GTR 10 set SDKMAJOR=10

set SELFCONTAINED=true
set EXTRA=-p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
if /i "%~1"=="small" (
    set SELFCONTAINED=false
    set EXTRA=
)

if exist dist rmdir /s /q dist
for %%d in (Core UI) do (
    if exist %%d\bin rmdir /s /q %%d\bin
    if exist %%d\obj rmdir /s /q %%d\obj
)

set COMMON=-c Release -r win-x64 --self-contained %SELFCONTAINED% -p:PublishSingleFile=true %EXTRA% -p:DebugType=none -p:DebugSymbols=false -o dist

echo Building GitHub Stats (net%SDKMAJOR%.0-windows, WPF, self-contained=%SELFCONTAINED%) ...
echo.
dotnet publish UI\GitHubStats.csproj %COMMON% -p:Tfm=net%SDKMAJOR%.0-windows -p:CoreTfm=net%SDKMAJOR%.0
if not errorlevel 1 goto :built

if "%SDKMAJOR%"=="8" goto :failed
echo.
echo [NOTICE] The build for .NET %SDKMAJOR% failed (see above). Trying .NET 8 instead ...
echo.
if exist dist rmdir /s /q dist
for %%d in (Core UI) do (
    if exist %%d\bin rmdir /s /q %%d\bin
    if exist %%d\obj rmdir /s /q %%d\obj
)
dotnet publish UI\GitHubStats.csproj %COMMON% -p:Tfm=net8.0-windows -p:CoreTfm=net8.0
if not errorlevel 1 goto :built

:failed
echo.
echo [ERROR] The build failed, see the messages above.
echo         The first build needs internet access to download NuGet packages ^(runtime pack, Windows desktop pack^).
echo         Check your connection or proxy and run build.bat again. If it still fails, send me the first error line.
goto :fail

:built
if not exist dist\GitHubStats.exe (
    echo [ERROR] dist\GitHubStats.exe was not created.
    goto :fail
)
if exist dist\GitHubStats.pdb del /q dist\GitHubStats.pdb

echo.
echo ============================================================
echo  Done:  %~dp0dist\GitHubStats.exe
echo.
echo  Copy that one file to any folder or USB stick and start it.
echo  On first start it creates config.json and a data folder
echo  next to the .exe. Everything can be set in the Settings screen.
echo ============================================================
echo.
if not defined NOPAUSE pause
exit /b 0

:fail
echo.
if not defined NOPAUSE pause
exit /b 1
