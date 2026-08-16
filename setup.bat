@echo off
REM ============================================================
REM   Xray VPN - One-Click Setup Launcher v2.0
REM   - Auto-elevates to Administrator
REM   - Keeps window open on error
REM   - Logs to %USERPROFILE%\Desktop\xray-vpn-install.log
REM ============================================================

setlocal
set "SCRIPT_DIR=%~dp0"
set "PS1=%SCRIPT_DIR%install.ps1"
set "LOG=%USERPROFILE%\Desktop\xray-vpn-install.log"

REM Check if install.ps1 exists
if not exist "%PS1%" (
    echo [ERROR] install.ps1 not found in:
    echo   %SCRIPT_DIR%
    echo.
    echo Please make sure both setup.bat and install.ps1 are in the same folder.
    echo.
    pause
    exit /b 1
)

REM Detect PowerShell (prefer PowerShell 7 if available)
where pwsh >nul 2>&1 && set "PS=pwsh" || set "PS=powershell"
where %PS% >nul 2>&1 || (
    echo [ERROR] PowerShell not found. Please install Windows Management Framework 5.0+
    echo.
    pause
    exit /b 1
)

REM Self-elevate to Administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Requesting Administrator privileges...
    echo.
    powershell -Command "Start-Process -FilePath '%PS%' -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%PS1%\"' -Verb RunAs"
    exit /b
)

REM Already admin - run directly
echo.
echo Running installer...
echo Log will be saved to: %LOG%
echo.

%PS% -NoProfile -ExecutionPolicy Bypass -File "%PS1%"

REM If we got here and the script didn't pause itself, keep window open
if %errorLevel% neq 0 (
    echo.
    echo ============================================================
    echo  INSTALLATION FAILED with exit code %errorLevel%
    echo ============================================================
    echo.
    echo Check the log file: %LOG%
    echo.
)

pause
