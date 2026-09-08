@echo off
setlocal enabledelayedexpansion

echo ===================================================
echo   SCADIX LOCAL NUGET SETUP UTILITY
echo   تهيئة مستودع NuGet المحلي للمشروع
echo ===================================================
echo.

:: 1. Check if nuget.config exists, if not create it
if not exist nuget.config (
    echo [INFO] nuget.config not found. Creating a new one...
    echo ^<?xml version="1.0" encoding="utf-8"?^> > nuget.config
    echo ^<configuration^> >> nuget.config
    echo   ^<config^> >> nuget.config
    echo     ^<add key="globalPackagesFolder" value="packages" /^> >> nuget.config
    echo   ^</config^> >> nuget.config
    echo ^</configuration^> >> nuget.config
    echo [SUCCESS] nuget.config created successfully.
) else (
    echo [INFO] nuget.config already exists.
)

:: 2. Find solution or project files to restore
echo.
echo [INFO] Scanning for C# solution or project files...

set "TARGET="
if exist *.slnx (
    for %%f in (*.slnx) do set "TARGET=%%f"
) else if exist *.sln (
    for %%f in (*.sln) do set "TARGET=%%f"
) else if exist *.csproj (
    for %%f in (*.csproj) do set "TARGET=%%f"
)

if "%TARGET%"=="" (
    echo [WARNING] No .slnx, .sln, or .csproj found in current directory.
    echo Running general dotnet restore for all projects...
    dotnet restore
) else (
    echo [INFO] Found target: %TARGET%
    echo Restoring packages locally to the 'packages' directory...
    dotnet restore "%TARGET%"
)

if %ERRORLEVEL% equ 0 (
    echo.
    echo ===================================================
    echo   [SUCCESS] NuGet packages restored successfully!
    echo   All packages are now stored in the local 'packages' folder.
    echo   تم استرجاع وحفظ كافة المكتبات محلياً في مجلد packages بنجاح!
    echo ===================================================
) else (
    echo.
    echo [ERROR] Restore failed. Please check internet connection or project files.
)

pause
