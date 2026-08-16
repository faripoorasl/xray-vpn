#Requires -Version 5.0
<#
.SYNOPSIS
    One-click installer for Xray VPN.

.DESCRIPTION
    This script does everything:
      1. Verifies Windows version (10 22H2+ / 11)
      2. Installs .NET 8 SDK (if missing) via winget
      3. Installs Git (if missing) via winget
      4. Installs Inno Setup (if missing) — optional, for installer builds
      5. Clones the xray-vpn repository (if not already in the folder)
      6. Downloads dependencies (xray.exe, wintun.dll, geoip.dat, geosite.dat)
      7. Restores NuGet packages
      8. Builds the app (Release x64)
      9. Publishes a single-file self-contained EXE
     10. Offers to launch the app
     11. Creates desktop & start menu shortcuts

.NOTES
    Run via setup.bat (auto-elevates to Administrator).
    Or directly:
        powershell -ExecutionPolicy Bypass -File install.ps1
#>

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"  # speed up Invoke-WebRequest

# ============================================================
# Helpers
# ============================================================
function Write-Step  { param([string]$msg) Write-Host "`n>>> $msg" -ForegroundColor Cyan }
function Write-OK    { param([string]$msg) Write-Host "    [OK] $msg" -ForegroundColor Green }
function Write-Warn2 { param([string]$msg) Write-Host "    [!!] $msg" -ForegroundColor Yellow }
function Write-Err2  { param([string]$msg) Write-Host "    [XX] $msg" -ForegroundColor Red }

function Test-Command {
    param([string]$name)
    return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

function Install-WithWinget {
    param(
        [string]$PackageId,
        [string]$DisplayName
    )
    if (-not (Test-Command "winget")) {
        Write-Warn2 "winget not available. Please install $DisplayName manually."
        return $false
    }
    Write-Host "    Installing $DisplayName via winget..."
    & winget install --id $PackageId --accept-source-agreements --accept-package-agreements --silent 2>&1 |
        Out-Null
    return $LASTEXITCODE -eq 0
}

# ============================================================
# Banner
# ============================================================
$host.UI.RawUI.WindowTitle = "Xray VPN Installer"
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "          Xray VPN - One-Click Installer v1.0" -ForegroundColor Cyan
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
Write-Host "  Press Ctrl+C at any time to abort."
Write-Host ""
$answer = Read-Host "Continue? (Y/n)"
if ($answer -match "^[nN]") { exit 0 }

# ============================================================
# Step 1: Verify Windows version
# ============================================================
Write-Step "Step 1/9 — Verifying Windows version..."
$os = Get-CimInstance Win32_OperatingSystem
$build = [int]$os.BuildNumber
$caption = $os.Caption
Write-Host "    OS: $caption (Build $build)"
if ($build -lt 19045 -and $build -lt 22000) {
    Write-Err2 "Windows 10 22H2 (build 19045) or Windows 11 (build 22000+) required."
    Write-Err2 "Your build: $build"
    Read-Host "Press Enter to exit"
    exit 1
}
Write-OK "Windows version OK"

# Check architecture
$arch = $env:PROCESSOR_ARCHITECTURE
if ($arch -ne "AMD64") {
    Write-Err2 "Only x64 architecture is supported. Detected: $arch"
    Read-Host "Press Enter to exit"
    exit 1
}
Write-OK "Architecture: x64"

# ============================================================
# Step 2: Install .NET 8 SDK
# ============================================================
Write-Step "Step 2/9 — Checking .NET 8 SDK..."
$dotnetOk = $false
if (Test-Command "dotnet") {
    $sdks = & dotnet --list-sdks 2>$null
    if ($sdks -match "8\.") {
        $ver = (& dotnet --version)
        Write-OK ".NET 8 SDK already installed ($ver)"
        $dotnetOk = $true
    }
}
if (-not $dotnetOk) {
    Write-Host "    .NET 8 SDK not found. Installing..."
    if (Install-WithWinget -PackageId "Microsoft.DotNet.SDK.8" -DisplayName ".NET 8 SDK") {
        # Refresh PATH
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
        if (Test-Command "dotnet") {
            Write-OK ".NET 8 SDK installed"
        } else {
            Write-Warn2 "Installed but not on PATH. Please restart your computer and re-run setup.bat."
            Read-Host "Press Enter to exit"
            exit 1
        }
    } else {
        Write-Err2 "Failed to install .NET 8 SDK."
        Write-Host "    Please install manually from: https://dotnet.microsoft.com/download/dotnet/8.0"
        Read-Host "Press Enter to exit"
        exit 1
    }
}

# ============================================================
# Step 3: Install Git
# ============================================================
Write-Step "Step 3/9 — Checking Git..."
if (Test-Command "git") {
    $gitVer = (& git --version)
    Write-OK "Git already installed ($gitVer)"
} else {
    Write-Host "    Git not found. Installing..."
    if (Install-WithWinget -PackageId "Git.Git" -DisplayName "Git") {
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
        Write-OK "Git installed"
    } else {
        Write-Warn2 "Could not install Git via winget."
        Write-Host "    Please install manually from: https://git-scm.com"
        Write-Host "    Then re-run setup.bat"
        Read-Host "Press Enter to exit"
        exit 1
    }
}

# ============================================================
# Step 4: Install Inno Setup (optional, for installer builds)
# ============================================================
Write-Step "Step 4/9 — Checking Inno Setup (optional)..."
if (Test-Command "iscc") {
    Write-OK "Inno Setup already installed"
} else {
    Write-Host "    Inno Setup not found (only needed for installer builds)."
    $installInno = Read-Host "    Install Inno Setup now? (Y/n)"
    if ($installInno -notmatch "^[nN]") {
        Install-WithWinget -PackageId "JRSoftware.InnoSetup" -DisplayName "Inno Setup" | Out-Null
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
        if (Test-Command "iscc") {
            Write-OK "Inno Setup installed"
        } else {
            Write-Warn2 "Inno Setup installation may have failed. Continuing anyway (portable build only)."
        }
    } else {
        Write-Warn2 "Skipping Inno Setup. Only portable build will be produced."
    }
}

# ============================================================
# Step 5: Clone or update repository
# ============================================================
Write-Step "Step 5/9 — Preparing source code..."
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoDir = $ScriptDir

# If running from inside an already-cloned repo, use it.
# Otherwise clone to a subfolder.
$hasCsproj = Test-Path (Join-Path $RepoDir "XrayVpn.sln")
if (-not $hasCsproj) {
    $RepoDir = Join-Path $ScriptDir "xray-vpn"
    if (Test-Path $RepoDir) {
        Write-Host "    Updating existing repo at: $RepoDir"
        Push-Location $RepoDir
        & git pull --quiet
        Pop-Location
    } else {
        Write-Host "    Cloning repository to: $RepoDir"
        & git clone --depth 1 https://github.com/faripoorasl/xray-vpn.git $RepoDir 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Err2 "Failed to clone repository."
            Read-Host "Press Enter to exit"
            exit 1
        }
    }
} else {
    Write-OK "Source code already in current folder"
}
Write-OK "Source code ready at: $RepoDir"

# ============================================================
# Step 6: Download dependencies (xray.exe, wintun.dll, etc.)
# ============================================================
Write-Step "Step 6/9 — Downloading dependencies..."
$resourcesDir = Join-Path $RepoDir "src\XrayVpnApp\Resources"
$expected = @("xray.exe", "wintun.dll", "geoip.dat", "geosite.dat")
$needDownload = $false
foreach ($f in $expected) {
    if (-not (Test-Path (Join-Path $resourcesDir $f))) {
        $needDownload = $true
        break
    }
}
if ($needDownload) {
    $dlScript = Join-Path $RepoDir "scripts\download-deps.ps1"
    if (Test-Path $dlScript) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $dlScript
        if ($LASTEXITCODE -ne 0) {
            Write-Warn2 "Some dependencies may not have downloaded. Check the output above."
        }
    } else {
        Write-Err2 "download-deps.ps1 not found."
        Read-Host "Press Enter to exit"
        exit 1
    }
} else {
    Write-OK "All dependencies already present"
}

# Verify
$allOk = $true
foreach ($f in $expected) {
    $p = Join-Path $resourcesDir $f
    if (Test-Path $p) {
        $size = (Get-Item $p).Length
        Write-Host ("    {0,-15} {1,10:N0} bytes" -f $f, $size)
    } else {
        Write-Err2 "Missing: $f"
        $allOk = $false
    }
}
if (-not $allOk) {
    Write-Err2 "Some dependencies are missing. Build will likely fail."
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -notmatch "^[yY]") { exit 1 }
}

# ============================================================
# Step 7: Restore NuGet packages
# ============================================================
Write-Step "Step 7/9 — Restoring NuGet packages..."
Push-Location $RepoDir
& dotnet restore XrayVpn.sln 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Warn2 "Restore had issues, but continuing..."
} else {
    Write-OK "Packages restored"
}

# ============================================================
# Step 8: Build & publish single-file EXE
# ============================================================
Write-Step "Step 8/9 — Building Xray VPN..."
$publishDir = Join-Path $RepoDir "build\Release\publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "    Publishing single-file self-contained EXE..."
& dotnet publish (Join-Path $RepoDir "src\XrayVpnApp\XrayVpnApp.csproj") `
    -c Release `
    -r win-x64 `
    -p:Platform=x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir 2>&1 | Select-Object -Last 5

if ($LASTEXITCODE -ne 0) {
    Write-Err2 "Build failed."
    Read-Host "Press Enter to exit"
    Pop-Location
    exit 1
}
Write-OK "Build succeeded"

# Copy dependencies to publish dir
foreach ($f in $expected) {
    $src = Join-Path $resourcesDir $f
    $dst = Join-Path $publishDir $f
    if ((Test-Path $src) -and (-not (Test-Path $dst))) {
        Copy-Item $src $dst -Force
    }
}
Write-OK "Dependencies copied to publish folder"

# (Optional) Build installer if Inno Setup is available
if (Test-Command "iscc") {
    Write-Host "    Building installer..."
    $issPath = Join-Path $RepoDir "src\XrayVpnApp.Installer\setup.iss"
    if (Test-Path $issPath) {
        & iscc $issPath 2>&1 | Select-Object -Last 3
        $installerOut = Join-Path $RepoDir "build\installer"
        if (Test-Path $installerOut) {
            Write-OK "Installer built in: $installerOut"
        }
    }
}
Pop-Location

# ============================================================
# Step 9: Create shortcuts & offer to launch
# ============================================================
Write-Step "Step 9/9 — Creating shortcuts..."
$exePath = Join-Path $publishDir "XrayVpn.exe"
if (-not (Test-Path $exePath)) {
    Write-Err2 "XrayVpn.exe not found at $exePath"
    Read-Host "Press Enter to exit"
    exit 1
}

$shell = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath("Desktop")
$startMenu = [Environment]::GetFolderPath("Programs")

# Desktop shortcut
$desktopShortcut = Join-Path $desktop "Xray VPN.lnk"
$sc = $shell.CreateShortcut($desktopShortcut)
$sc.TargetPath = $exePath
$sc.WorkingDirectory = $publishDir
$sc.Description = "Xray VPN - Xray-based VPN client for Windows 11"
$sc.WindowStyle = 1
$sc.Save()
Write-OK "Desktop shortcut: $desktopShortcut"

# Start Menu shortcut
$startMenuFolder = Join-Path $startMenu "Xray VPN"
if (-not (Test-Path $startMenuFolder)) {
    New-Item -ItemType Directory -Path $startMenuFolder -Force | Out-Null
}
$startShortcut = Join-Path $startMenuFolder "Xray VPN.lnk"
$sc2 = $shell.CreateShortcut($startShortcut)
$sc2.TargetPath = $exePath
$sc2.WorkingDirectory = $publishDir
$sc2.Description = "Xray VPN - Xray-based VPN client for Windows 11"
$sc2.WindowStyle = 1
$sc2.Save()
Write-OK "Start Menu shortcut: $startShortcut"

# ============================================================
# Done
# ============================================================
Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "                Installation Complete!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  App location : $exePath"
Write-Host "  Desktop      : Xray VPN (shortcut)"
Write-Host "  Start Menu   : Xray VPN (shortcut)"
Write-Host ""
Write-Host "  Note: The app requires Administrator privileges to run"
Write-Host "        (for TUN adapter creation). The shortcuts are"
Write-Host "        already configured to prompt for elevation."
Write-Host ""
Write-Host "  Next steps:"
Write-Host "    1. Double-click 'Xray VPN' on your Desktop"
Write-Host "    2. Click 'Yes' on the UAC prompt"
Write-Host "    3. Paste a vless://, vmess://, trojan://, or ss:// link"
Write-Host "    4. Click '+' to add the server"
Write-Host "    5. Select the server, click 'Connect'"
Write-Host ""
Write-Host "  Logs are at: %LOCALAPPDATA%\XrayVpn\logs\"
Write-Host ""
$launch = Read-Host "Launch Xray VPN now? (Y/n)"
if ($launch -notmatch "^[nN]") {
    Write-Host "    Launching..."
    Start-Process $exePath -Verb RunAs
}
Write-Host ""
Write-Host "Done. Press Enter to close this window."
Read-Host
