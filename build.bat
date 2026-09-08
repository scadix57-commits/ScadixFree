@echo off
setlocal
cd /d "%~dp0"
echo --- Scadix Designer Unified Build Launcher ---
echo.
powershell -ExecutionPolicy Bypass -File "build.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed! Check the output above.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo [SUCCESS] Everything built and deployed.
pause
