@echo off
REM ============================================================
REM   Xray VPN - One-Click Setup Launcher
REM   This batch file just launches PowerShell with admin rights
REM   and runs the real installer script (install.ps1).
REM ============================================================

setlocal
set "SCRIPT_DIR=%~dp0"
set "PS1=%SCRIPT_DIR%install.ps1"

REM Check if install.ps1 exists
if not exist "%PS1%" (
    echo [ERROR] install.ps1 not found in:
    echo   %SCRIPT_DIR%
    echo.
    echo Please make sure both setup.bat and install.ps1 are in the same folder.
    pause
    exit /b 1
)

REM Detect PowerShell
where pwsh >nul 2>&1 && set "PS=pwsh" || set "PS=powershell"
where %PS% >nul 2>&1 || (
    echo [ERROR] PowerShell not found. Please install Windows Management Framework 5.0+
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
%PS% -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
pause
