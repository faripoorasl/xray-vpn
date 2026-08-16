#Requires -Version 5.0
<#
.SYNOPSIS
    One-click installer for Xray VPN with robust error checking.

.DESCRIPTION
    Each step checks its own success. If a step fails, the script stops,
    shows a clear error message, and writes a detailed log file to:
      %USERPROFILE%\Desktop\xray-vpn-install.log

    Run via setup.bat (auto-elevates to Administrator).
#>

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# ============================================================
# Setup log file
# ============================================================
$LogFile = "$env:USERPROFILE\Desktop\xray-vpn-install.log"
"=== Xray VPN Installation Log ===" | Out-File $LogFile
"Started: $(Get-Date)" | Out-File $LogFile -Append
"User: $env:USERNAME" | Out-File $LogFile -Append
"OS: $((Get-CimInstance Win32_OperatingSystem).Caption) Build $([System.Environment]::OSVersion.Version.Build)" | Out-File $LogFile -Append
"" | Out-File $LogFile -Append

function Write-Log {
    param([string]$msg)
    $stamp = Get-Date -Format "HH:mm:ss"
    $line = "[$stamp] $msg"
    Write-Host $line
    $line | Out-File $LogFile -Append
}

function Write-Step  { param([string]$msg) Write-Host "`n>>> $msg" -ForegroundColor Cyan; ">>> $msg" | Out-File $LogFile -Append }
function Write-OK    { param([string]$msg) Write-Host "    [OK] $msg" -ForegroundColor Green; "    [OK] $msg" | Out-File $LogFile -Append }
function Write-Warn2 { param([string]$msg) Write-Host "    [!!] $msg" -ForegroundColor Yellow; "    [!!] $msg" | Out-File $LogFile -Append }
function Write-Err2  { param([string]$msg) Write-Host "    [XX] $msg" -ForegroundColor Red; "    [XX] $msg" | Out-File $LogFile -Append }

function Die {
    param(
        [string]$msg,
        [string]$hint = ""
    )
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host "  INSTALLATION FAILED" -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Error: $msg" -ForegroundColor Red
    Write-Host ""
    if ($hint) {
        Write-Host "  How to fix:" -ForegroundColor Yellow
        Write-Host "  $hint" -ForegroundColor Yellow
        Write-Host ""
    }
    Write-Host "  Full log saved to: $LogFile" -ForegroundColor Yellow
    Write-Host "  Please send this file to get help." -ForegroundColor Yellow
    Write-Host ""
    "FATAL: $msg" | Out-File $LogFile -Append
    if ($hint) { "HINT: $hint" | Out-File $LogFile -Append }
    Read-Host "Press Enter to close"
    exit 1
}

function Test-Command {
    param([string]$name)
    return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

function Refresh-Path {
    $env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path', 'User')
}

# ============================================================
# Banner
# ============================================================
$host.UI.RawUI.WindowTitle = "Xray VPN Installer"
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "          Xray VPN - One-Click Installer v2.0" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  This script will:"
Write-Host "    1. Verify Windows version"
Write-Host "    2. Install prerequisites (.NET 8 SDK, Git, Inno Setup)"
Write-Host "    3. Clone or update the xray-vpn repository"
Write-Host "    4. Download dependencies (xray.exe, wintun.dll, geoip)"
Write-Host "    5. Build the app as a single-file EXE"
Write-Host "    6. Create shortcuts on Desktop and Start Menu"
Write-Host "    7. Offer to launch the app"
Write-Host ""
Write-Host "  Each step is verified. If anything fails, the script"
Write-Host "  stops with a clear error message and a log file."
Write-Host ""
Write-Host "  Press Ctrl+C at any time to abort."
Write-Host ""
$answer = Read-Host "Continue? (Y/n)"
if ($answer -match '^[nN]') { exit 0 }

# ============================================================
# Step 1: Verify Windows version
# ============================================================
Write-Step 'Step 1/9 - Verifying Windows version...'
$os = Get-CimInstance Win32_OperatingSystem
$build = [int]$os.BuildNumber
$caption = $os.Caption
Write-Log "OS: $caption (Build $build)"

if ($build -lt 19045 -and $build -lt 22000) {
    Die "Windows 10 22H2 (build 19045) or Windows 11 (build 22000+) required. Your build: $build" `
        "Upgrade Windows from Settings > Update & Security > Windows Update."
}
Write-OK "Windows version OK (Build $build)"

$arch = $env:PROCESSOR_ARCHITECTURE
if ($arch -ne 'AMD64') {
    Die "Only x64 architecture is supported. Detected: $arch" `
        "Use a 64-bit Windows installation."
}
Write-OK "Architecture: x64"

# ============================================================
# Step 2: Install .NET 8 SDK
# ============================================================
Write-Step 'Step 2/9 - Checking .NET 8 SDK...'

$dotnetOk = $false
if (Test-Command 'dotnet') {
    $sdks = & dotnet --list-sdks 2>&1
    $sdkList = $sdks -join "`n"
    Write-Log "Installed SDKs:`n$sdkList"
    if ($sdks -match '8\.') {
        $ver = & dotnet --version 2>&1
        Write-OK ".NET 8 SDK already installed ($ver)"
        $dotnetOk = $true
    }
}

if (-not $dotnetOk) {
    Write-Log '.NET 8 SDK not found. Installing via winget...'
    if (-not (Test-Command 'winget')) {
        Die 'winget not available. Cannot install .NET 8 SDK automatically.' `
            "Install winget: open Microsoft Store, search 'App Installer', install it.`nThen re-run setup.bat.`nOr install .NET 8 SDK manually from: https://dotnet.microsoft.com/download/dotnet/8.0"
    }

    Write-Host '    Installing .NET 8 SDK via winget...' -ForegroundColor Yellow
    $wingetOut = & winget install --id Microsoft.DotNet.SDK.8 --accept-source-agreements --accept-package-agreements --silent 2>&1
    $wingetOut | ForEach-Object { Write-Log "  winget: $_" }

    if ($LASTEXITCODE -ne 0) {
        Die "winget failed to install .NET 8 SDK (exit code $LASTEXITCODE)" `
            "Try manual install: https://dotnet.microsoft.com/download/dotnet/8.0`nDownload the SDK (x64) installer and run it.`nThen restart your computer and re-run setup.bat."
    }

    # Refresh PATH
    Refresh-Path

    # Verify
    Start-Sleep -Seconds 2
    if (-not (Test-Command 'dotnet')) {
        Die '.NET 8 SDK installed but dotnet not on PATH' `
            'Restart your computer (to refresh PATH) and re-run setup.bat.'
    }

    $sdks = & dotnet --list-sdks 2>&1
    if ($sdks -notmatch '8\.') {
        Die ".NET installed but no 8.x SDK found. Installed: $sdks" `
            'Restart your computer and re-run setup.bat. If still failing, install manually from https://dotnet.microsoft.com/download/dotnet/8.0'
    }

    $ver = & dotnet --version 2>&1
    Write-OK ".NET 8 SDK installed ($ver)"
}

# ============================================================
# Step 3: Install Git
# ============================================================
Write-Step 'Step 3/9 - Checking Git...'

if (Test-Command 'git') {
    $gitVer = & git --version 2>&1
    Write-OK "Git already installed ($gitVer)"
} else {
    Write-Log 'Git not found. Installing via winget...'
    if (-not (Test-Command 'winget')) {
        Die 'winget not available. Cannot install Git automatically.' `
            'Install Git manually from: https://git-scm.com/download/win'
    }

    Write-Host '    Installing Git via winget...' -ForegroundColor Yellow
    $wingetOut = & winget install --id Git.Git --accept-source-agreements --accept-package-agreements --silent 2>&1
    $wingetOut | ForEach-Object { Write-Log "  winget: $_" }

    if ($LASTEXITCODE -ne 0) {
        Die "winget failed to install Git (exit code $LASTEXITCODE)" `
            'Install Git manually from: https://git-scm.com/download/win`nThen re-run setup.bat.'
    }

    Refresh-Path
    Start-Sleep -Seconds 2

    if (-not (Test-Command 'git')) {
        Die 'Git installed but not on PATH' `
            'Restart your computer (to refresh PATH) and re-run setup.bat.'
    }

    $gitVer = & git --version 2>&1
    Write-OK "Git installed ($gitVer)"
}

# ============================================================
# Step 4: Install Inno Setup (optional)
# ============================================================
Write-Step 'Step 4/9 - Checking Inno Setup (optional)...'

$innoInstalled = $false
if (Test-Command 'iscc') {
    Write-OK 'Inno Setup already installed'
    $innoInstalled = $true
} else {
    Write-Host '    Inno Setup not found (only needed for installer builds).' -ForegroundColor Yellow
    $installInno = Read-Host '    Install Inno Setup now? (Y/n)'
    if ($installInno -notmatch '^[nN]') {
        if (-not (Test-Command 'winget')) {
            Write-Warn2 'winget not available. Skipping Inno Setup.'
        } else {
            Write-Host '    Installing Inno Setup via winget...' -ForegroundColor Yellow
            $wingetOut = & winget install --id JRSoftware.InnoSetup --accept-source-agreements --accept-package-agreements --silent 2>&1
            $wingetOut | ForEach-Object { Write-Log "  winget: $_" }

            Refresh-Path
            Start-Sleep -Seconds 2

            if (Test-Command 'iscc') {
                Write-OK 'Inno Setup installed'
                $innoInstalled = $true
            } else {
                Write-Warn2 'Inno Setup installation may have failed. Continuing with portable build only.'
            }
        }
    } else {
        Write-Warn2 'Skipping Inno Setup. Only portable build will be produced.'
    }
}

# ============================================================
# Step 5: Clone or update repository
# ============================================================
Write-Step 'Step 5/9 - Preparing source code...'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoDir = $ScriptDir

$hasCsproj = Test-Path (Join-Path $RepoDir 'XrayVpn.sln')
if (-not $hasCsproj) {
    $RepoDir = Join-Path $ScriptDir 'xray-vpn'
    if (Test-Path $RepoDir) {
        Write-Log "Updating existing repo at: $RepoDir"
        Push-Location $RepoDir
        try {
            & git pull --quiet 2>&1 | ForEach-Object { Write-Log "  git: $_" }
            if ($LASTEXITCODE -ne 0) {
                Write-Warn2 'git pull failed. Continuing with existing code.'
            }
        } finally {
            Pop-Location
        }
    } else {
        Write-Log "Cloning repository to: $RepoDir"
        & git clone --depth 1 https://github.com/faripoorasl/xray-vpn.git $RepoDir 2>&1 | ForEach-Object { Write-Log "  git: $_" }
        if ($LASTEXITCODE -ne 0) {
            Die "Failed to clone repository (git exit code $LASTEXITCODE)" `
                "Check your internet connection.`nIf you are in Iran, you may need a VPN to access GitHub.`nAlternatively, download the ZIP manually from:`nhttps://github.com/faripoorasl/xray-vpn/archive/refs/heads/main.zip`nAnd extract it to: $RepoDir"
        }
    }
} else {
    Write-OK 'Source code already in current folder'
}

# Verify the .sln file exists
$slnPath = Join-Path $RepoDir 'XrayVpn.sln'
if (-not (Test-Path $slnPath)) {
    Die "XrayVpn.sln not found at: $slnPath" `
        'The repository did not clone correctly. Delete the folder and re-run setup.bat.'
}
Write-OK "Source code ready at: $RepoDir"

# ============================================================
# Step 6: Download dependencies
# ============================================================
Write-Step 'Step 6/9 - Downloading dependencies...'

$resourcesDir = Join-Path $RepoDir 'src\XrayVpnApp\Resources'
$expected = @('xray.exe', 'wintun.dll', 'geoip.dat', 'geosite.dat')
$needDownload = $false
foreach ($f in $expected) {
    if (-not (Test-Path (Join-Path $resourcesDir $f))) {
        $needDownload = $true
        break
    }
}

if ($needDownload) {
    $dlScript = Join-Path $RepoDir 'scripts\download-deps.ps1'
    if (-not (Test-Path $dlScript)) {
        Die "download-deps.ps1 not found at: $dlScript" `
            'The repository is incomplete. Delete the folder and re-run setup.bat.'
    }

    Write-Log 'Running download-deps.ps1...'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $dlScript 2>&1 | ForEach-Object {
        Write-Host "    $_"
        Write-Log "  download-deps: $_"
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Warn2 "download-deps.ps1 exited with code $LASTEXITCODE"
    }
} else {
    Write-OK 'All dependencies already present'
}

# Verify each file
$missing = @()
foreach ($f in $expected) {
    $p = Join-Path $resourcesDir $f
    if (Test-Path $p) {
        $size = (Get-Item $p).Length
        Write-Log ("  {0} - {1:N0} bytes" -f $f, $size)
        if ($size -lt 100KB) {
            Write-Warn2 "$f is suspiciously small ({0:N0} bytes)" -f $size
            $missing += $f
        }
    } else {
        Write-Err2 "Missing: $f"
        $missing += $f
    }
}

if ($missing.Count -gt 0) {
    Die "Required dependencies are missing: $($missing -join ', ')" `
        "This usually means GitHub is blocked or your internet is unstable.`nSolutions:`n  1. Connect to a VPN and re-run setup.bat`n  2. Or download manually:`n     - xray.exe, geoip.dat, geosite.dat from: https://github.com/XTLS/Xray-core/releases/latest`n     - wintun.dll from: https://www.wintun.net/builds/wintun-0.14.1.zip`n  3. Place these files in: $resourcesDir`n  4. Re-run setup.bat"
}

Write-OK 'All dependencies present'

# ============================================================
# Step 7: Restore NuGet packages
# ============================================================
Write-Step 'Step 7/9 - Restoring NuGet packages...'

Push-Location $RepoDir
try {
    Write-Log 'Running dotnet restore...'
    $restoreOut = & dotnet restore XrayVpn.sln 2>&1
    $restoreOut | ForEach-Object { Write-Log "  restore: $_" }

    if ($LASTEXITCODE -ne 0) {
        Die "NuGet restore failed (exit code $LASTEXITCODE)" `
            "This is usually a network issue.`nCheck your internet connection and try again.`nIf behind a proxy, configure NuGet:`n  dotnet nuget add source <URL> -n ProxySource"
    }
    Write-OK 'Packages restored'
} finally {
    Pop-Location
}

# ============================================================
# Step 8: Build & publish
# ============================================================
Write-Step 'Step 8/9 - Building Xray VPN...'

$publishDir = Join-Path $RepoDir 'build\Release\publish'
if (Test-Path $publishDir) {
    Write-Log "Cleaning previous publish folder..."
    Remove-Item $publishDir -Recurse -Force
}

Push-Location $RepoDir
try {
    Write-Host '    Publishing single-file self-contained EXE...' -ForegroundColor Yellow
    Write-Log 'Running dotnet publish...'
    $publishOut = & dotnet publish (Join-Path $RepoDir 'src\XrayVpnApp\XrayVpnApp.csproj') `
        -c Release `
        -r win-x64 `
        -p:Platform=x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $publishDir 2>&1

    $publishOut | ForEach-Object { Write-Log "  publish: $_" }

    if ($LASTEXITCODE -ne 0) {
        Die "Build failed (dotnet publish exit code $LASTEXITCODE)" `
            "This is a code or compiler error.`nPlease send the log file to get help:`n  $LogFile`n`nCommon fixes:`n  - Delete the obj/ folder and try again:`n    Remove-Item -Recurse '$RepoDir\src\XrayVpnApp\obj'`n  - Make sure .NET 8 SDK x64 is installed (not just x86)"
    }

    # Verify the EXE was created
    $exePath = Join-Path $publishDir 'XrayVpn.exe'
    if (-not (Test-Path $exePath)) {
        Die "Build reported success but XrayVpn.exe not found at: $exePath" `
            "Check the log file for details: $LogFile"
    }

    $exeSize = (Get-Item $exePath).Length
    if ($exeSize -lt 1MB) {
        Die "XrayVpn.exe is too small ({0:N0} bytes). Build may have failed silently." -f $exeSize `
            "Check the log file: $LogFile"
    }

    Write-OK "Build succeeded (XrayVpn.exe = {0:N0} bytes)" -f $exeSize

    # Copy dependencies to publish folder
    foreach ($f in $expected) {
        $src = Join-Path $resourcesDir $f
        $dst = Join-Path $publishDir $f
        if ((Test-Path $src) -and (-not (Test-Path $dst))) {
            Copy-Item $src $dst -Force
            Write-Log "  Copied $f to publish folder"
        }
    }
    Write-OK 'Dependencies copied to publish folder'

    # Build installer if Inno Setup is available
    if ($innoInstalled) {
        Write-Host '    Building installer...' -ForegroundColor Yellow
        $issPath = Join-Path $RepoDir 'src\XrayVpnApp.Installer\setup.iss'
        if (Test-Path $issPath) {
            $innoOut = & iscc $issPath 2>&1
            $innoOut | ForEach-Object { Write-Log "  inno: $_" }

            $installerOut = Join-Path $RepoDir 'build\installer'
            if (Test-Path $installerOut) {
                $installerExe = Get-ChildItem $installerOut -Filter 'XrayVpn-*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($installerExe) {
                    Write-OK "Installer built: $($installerExe.Name)"
                }
            }
        } else {
            Write-Warn2 "setup.iss not found at: $issPath"
        }
    }
} finally {
    Pop-Location
}

# ============================================================
# Step 9: Create shortcuts
# ============================================================
Write-Step 'Step 9/9 - Creating shortcuts...'

try {
    $shell = New-Object -ComObject WScript.Shell
    if (-not $shell) {
        Die 'Failed to create WScript.Shell COM object' `
            'This is unusual. Try restarting Windows and re-running setup.bat.'
    }

    $desktop = [Environment]::GetFolderPath('Desktop')
    $startMenu = [Environment]::GetFolderPath('Programs')

    # Desktop shortcut
    $desktopShortcut = Join-Path $desktop 'Xray VPN.lnk'
    $sc = $shell.CreateShortcut($desktopShortcut)
    $sc.TargetPath = $exePath
    $sc.WorkingDirectory = $publishDir
    $sc.Description = 'Xray VPN - Xray-based VPN client for Windows 11'
    $sc.WindowStyle = 1
    $sc.Save()

    if (-not (Test-Path $desktopShortcut)) {
        Die "Failed to create desktop shortcut at: $desktopShortcut" `
            'Check that you have write access to your Desktop folder.'
    }
    Write-OK "Desktop shortcut: $desktopShortcut"

    # Start Menu shortcut
    $startMenuFolder = Join-Path $startMenu 'Xray VPN'
    if (-not (Test-Path $startMenuFolder)) {
        New-Item -ItemType Directory -Path $startMenuFolder -Force | Out-Null
    }
    $startShortcut = Join-Path $startMenuFolder 'Xray VPN.lnk'
    $sc2 = $shell.CreateShortcut($startShortcut)
    $sc2.TargetPath = $exePath
    $sc2.WorkingDirectory = $publishDir
    $sc2.Description = 'Xray VPN - Xray-based VPN client for Windows 11'
    $sc2.WindowStyle = 1
    $sc2.Save()

    Write-OK "Start Menu shortcut: $startShortcut"
} catch {
    Die "Error creating shortcuts: $($_.Exception.Message)" `
        "The app was built successfully at:`n  $exePath`nYou can run it directly from there."
}

# ============================================================
# Done
# ============================================================
Write-Host ''
Write-Host '============================================================' -ForegroundColor Green
Write-Host '                Installation Complete!' -ForegroundColor Green
Write-Host '============================================================' -ForegroundColor Green
Write-Host ''
Write-Host "  App location : $exePath"
Write-Host '  Desktop      : Xray VPN (shortcut)'
Write-Host '  Start Menu   : Xray VPN (shortcut)'
Write-Host ''
Write-Host '  Note: The app requires Administrator privileges to run'
Write-Host '        (for TUN adapter creation). The shortcuts are'
Write-Host '        already configured to prompt for elevation.'
Write-Host ''
Write-Host '  Next steps:'
Write-Host '    1. Double-click "Xray VPN" on your Desktop'
Write-Host '    2. Click "Yes" on the UAC prompt'
Write-Host "    3. Paste a vless://, vmess://, trojan://, or ss:// link"
Write-Host "    4. Click '+' to add the server"
Write-Host "    5. Select the server, click 'Connect'"
Write-Host ''
Write-Host '  Logs are at: %LOCALAPPDATA%\XrayVpn\logs\'
Write-Host ''
Write-Log 'INSTALLATION COMPLETE'

$launch = Read-Host 'Launch Xray VPN now? (Y/n)'
if ($launch -notmatch '^[nN]') {
    Write-Host '    Launching...'
    try {
        Start-Process $exePath -Verb RunAs
    } catch {
        Write-Warn2 "Could not auto-launch: $($_.Exception.Message)"
        Write-Host "    Please run manually: $exePath"
    }
}
Write-Host ''
Write-Host "  Log file: $LogFile" -ForegroundColor Yellow
Write-Host ''
Read-Host 'Press Enter to close'
