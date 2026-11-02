@echo off
rem ============================================================
rem  One-click Release packaging for VerticalCornerBoost.
rem
rem  Usage:
rem      build-release.bat            build + package (Release)
rem      build-release.bat --no-zip   compile only (Debug, no zip)
rem
rem  Output: VerticalCornerBoost\VerticalCornerBoost.zip
rem
rem  NOTE: kept pure ASCII on purpose. cmd.exe decodes batch files
rem  using the OEM codepage (GBK on Chinese Windows), so non-ASCII
rem  text here would be misparsed.
rem ============================================================
setlocal enabledelayedexpansion

cd /d "%~dp0"

rem ---- 1. Read the Celeste install dir from Celeste.txt --------
set "CELESTE_DIR="
if not exist "Celeste.txt" goto :no_celeste_txt

rem Celeste.txt holds a single line: the Celeste directory.
set /p CELESTE_DIR=<Celeste.txt

rem Trim surrounding spaces.
for /f "tokens=* delims= " %%A in ("!CELESTE_DIR!") do set "CELESTE_DIR=%%A"

if not defined CELESTE_DIR goto :no_celeste_txt
if not exist "!CELESTE_DIR!\Celeste.dll" goto :bad_celeste_dir

echo [VCB] Celeste dir : !CELESTE_DIR!

rem ---- 2. Pick configuration -----------------------------------
if /I "%~1"=="--no-zip" (
    set "CONFIG=Debug"
    echo [VCB] Mode       : compile only ^(Debug, no zip^)
) else (
    set "CONFIG=Release"
    echo [VCB] Mode       : Release packaging
)
echo.

rem ---- 3. Build (publish, not build: deps must be copied) ------
dotnet publish "VerticalCornerBoost\Source\VerticalCornerBoost.csproj" -c %CONFIG% -p:CelestePrefix="!CELESTE_DIR!"
if errorlevel 1 goto :build_failed

rem ---- 4. Verify output ----------------------------------------
if /I "%CONFIG%"=="Release" (
    if not exist "VerticalCornerBoost\VerticalCornerBoost.zip" goto :no_zip
    echo.
    echo [VCB] Package    : VerticalCornerBoost\VerticalCornerBoost.zip
    for %%F in ("VerticalCornerBoost\VerticalCornerBoost.zip") do echo [VCB] Size       : %%~zF bytes
    echo [VCB] Contents:
    powershell -NoProfile -Command "Add-Type -A System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::OpenRead((Resolve-Path 'VerticalCornerBoost\VerticalCornerBoost.zip')).Entries | ForEach-Object { '      ' + $_.FullName }"
) else (
    echo.
    echo [VCB] Compiled   : VerticalCornerBoost\bin\VerticalCornerBoost.dll
)

endlocal
exit /b 0

:no_celeste_txt
echo [VCB][ERROR] Celeste.txt not found next to this script.
echo               It must contain one line: the Celeste install directory.
endlocal
exit /b 1

:bad_celeste_dir
echo [VCB][ERROR] Directory in Celeste.txt is invalid or has no Celeste.dll:
echo               !CELESTE_DIR!
endlocal
exit /b 1

:build_failed
echo.
echo [VCB][ERROR] Build failed. See the errors above.
endlocal
exit /b 1

:no_zip
echo.
echo [VCB][ERROR] Build succeeded but no zip was produced.
echo               Check the PackageMod target in the csproj.
endlocal
exit /b 1
