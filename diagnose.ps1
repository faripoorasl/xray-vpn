#Requires -Version 5.0
<#
.SYNOPSIS
    Diagnostic tool — finds out exactly where the installation failed.

.DESCRIPTION
    Checks each component step-by-step and reports status.
    No modifications — just diagnosis.
#>

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

# Force window to stay open
$host.UI.RawUI.WindowTitle = "Xray VPN - Diagnostic"

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "       Xray VPN - Installation Diagnostic Tool" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Also write everything to a log file
$logFile = "$env:USERPROFILE\Desktop\xray-vpn-diagnostic.log"
"=== Diagnostic started: $(Get-Date) ===" | Out-File $logFile

function Log-Status {
    param([string]$step, [string]$status, [string]$detail = "")
    $color = if ($status -eq "OK") { "Green" }
             elseif ($status -eq "FAIL") { "Red" }
             elseif ($status -eq "WARN") { "Yellow" }
             else { "Gray" }
    $line = "  [$status] $step"
    if ($detail) { $line += " — $detail" }
    Write-Host $line -ForegroundColor $color
    "$line" | Out-File $logFile -Append
}

# ============================================================
# Step 1: OS version
# ============================================================
Write-Host "`n>>> Step 1: Checking Windows version..." -ForegroundColor Cyan
$os = Get-CimInstance Win32_OperatingSystem
$build = [int]$os.BuildNumber
Write-Host "  OS: $($os.Caption)"
Write-Host "  Build: $build"
Write-Host "  Arch: $env:PROCESSOR_ARCHITECTURE"
if ($build -ge 19045 -or $build -ge 22000) {
    Log-Status "Windows version" "OK" "Build $build"
} else {
    Log-Status "Windows version" "FAIL" "Build $build too old (need 19045+ or 22000+)"
}

# ============================================================
# Step 2: Administrator
# ============================================================
Write-Host "`n>>> Step 2: Checking Administrator privileges..." -ForegroundColor Cyan
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($isAdmin) {
    Log-Status "Administrator" "OK"
} else {
    Log-Status "Administrator" "FAIL" "Script not running as Admin — re-run as Administrator"
}

# ============================================================
# Step 3: winget
# ============================================================
Write-Host "`n>>> Step 3: Checking winget..." -ForegroundColor Cyan
$wg = Get-Command winget -ErrorAction SilentlyContinue
if ($wg) {
    $wgVer = (& winget --version) 2>$null
    Log-Status "winget" "OK" $wgVer
} else {
    Log-Status "winget" "FAIL" "Not installed — install 'App Installer' from Microsoft Store"
}

# ============================================================
# Step 4: .NET 8 SDK
# ============================================================
Write-Host "`n>>> Step 4: Checking .NET 8 SDK..." -ForegroundColor Cyan
$dn = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dn) {
    $sdks = & dotnet --list-sdks 2>$null
    $sdk8 = $sdks | Where-Object { $_ -match "^8\." }
    if ($sdk8) {
        Log-Status ".NET 8 SDK" "OK" ($sdk8 | Select-Object -First 1)
    } else {
        Log-Status ".NET 8 SDK" "FAIL" "dotnet installed but no 8.x SDK found"
        Write-Host "  Installed SDKs:" -ForegroundColor Yellow
        $sdks | ForEach-Object { Write-Host "    $_" }
    }
} else {
    Log-Status ".NET 8 SDK" "FAIL" "dotnet not on PATH"
}

# ============================================================
# Step 5: Git
# ============================================================
Write-Host "`n>>> Step 5: Checking Git..." -ForegroundColor Cyan
$git = Get-Command git -ErrorAction SilentlyContinue
if ($git) {
    $gitVer = & git --version 2>$null
    Log-Status "Git" "OK" $gitVer
} else {
    Log-Status "Git" "FAIL" "Not installed"
}

# ============================================================
# Step 6: Inno Setup (optional)
# ============================================================
Write-Host "`n>>> Step 6: Checking Inno Setup (optional)..." -ForegroundColor Cyan
$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if ($iscc) {
    Log-Status "Inno Setup" "OK"
} else {
    Log-Status "Inno Setup" "WARN" "Not installed (only needed for installer builds)"
}

# ============================================================
# Step 7: Find the project folder
# ============================================================
Write-Host "`n>>> Step 7: Looking for the project folder..." -ForegroundColor Cyan
$candidates = @(
    "$env:USERPROFILE\Desktop\xray-vpn-main",
    "$env:USERPROFILE\Desktop\xray-vpn",
    "$env:USERPROFILE\Downloads\xray-vpn-main",
    "$env:USERPROFILE\Downloads\xray-vpn",
    "C:\xray-vpn",
    "D:\xray-vpn"
)
$projectDir = $null
foreach ($c in $candidates) {
    if (Test-Path (Join-Path $c "XrayVpn.sln")) {
        $projectDir = $c
        break
    }
}
if ($projectDir) {
    Log-Status "Project folder" "OK" $projectDir
} else {
    # Search filesystem
    Write-Host "  Not found in common locations. Searching..." -ForegroundColor Yellow
    $found = Get-ChildItem -Path "$env:USERPROFILE\Desktop", "$env:USERPROFILE\Downloads" -Filter "XrayVpn.sln" -Recurse -ErrorAction SilentlyContinue -Depth 3 2>$null
    if ($found) {
        $projectDir = $found[0].DirectoryName
        Log-Status "Project folder" "OK" "Found at: $projectDir"
    } else {
        Log-Status "Project folder" "FAIL" "XrayVpn.sln not found — clone the repo first"
    }
}

if (-not $projectDir) {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host "  Cannot continue without the project folder." -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Suggested action:"
    Write-Host "    1. Download the project ZIP from:"
    Write-Host "       https://github.com/faripoorasl/xray-vpn/archive/refs/heads/main.zip"
    Write-Host "    2. Extract it to a folder (e.g., Desktop\xray-vpn-main)"
    Write-Host "    3. Re-run this diagnostic"
    Write-Host ""
    Write-Host "  Diagnostic log saved to: $logFile" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# ============================================================
# Step 8: Check dependencies in Resources folder
# ============================================================
Write-Host "`n>>> Step 8: Checking downloaded dependencies..." -ForegroundColor Cyan
$resourcesDir = Join-Path $projectDir "src\XrayVpnApp\Resources"
if (-not (Test-Path $resourcesDir)) {
    Log-Status "Resources folder" "FAIL" "Does not exist: $resourcesDir"
} else {
    $deps = @(
        @{Name="xray.exe"; MinSize=10MB},
        @{Name="wintun.dll"; MinSize=100KB},
        @{Name="geoip.dat"; MinSize=1MB},
        @{Name="geosite.dat"; MinSize=1MB}
    )
    foreach ($d in $deps) {
        $p = Join-Path $resourcesDir $d.Name
        if (Test-Path $p) {
            $size = (Get-Item $p).Length
            if ($size -ge $d.MinSize) {
                Log-Status "  $($d.Name)" "OK" ("{0:N0} bytes" -f $size)
            } else {
                Log-Status "  $($d.Name)" "FAIL" "Too small ({0:N0} bytes, need >= {1:N0})" -f $size, $d.MinSize
            }
        } else {
            Log-Status "  $($d.Name)" "FAIL" "Missing"
        }
    }
}

# ============================================================
# Step 9: Check build output
# ============================================================
Write-Host "`n>>> Step 9: Checking build output..." -ForegroundColor Cyan
$publishDir = Join-Path $projectDir "build\Release\publish"
if (Test-Path $publishDir) {
    Log-Status "Publish folder" "OK" $publishDir
    $exePath = Join-Path $publishDir "XrayVpn.exe"
    if (Test-Path $exePath) {
        $size = (Get-Item $exePath).Length
        Log-Status "  XrayVpn.exe" "OK" ("{0:N0} bytes" -f $size)
    } else {
        Log-Status "  XrayVpn.exe" "FAIL" "Missing in publish folder"
    }
} else {
    Log-Status "Publish folder" "FAIL" "Does not exist — build did not complete"
    
    # Check if bin/obj exist (sign of partial build)
    $binDir = Join-Path $projectDir "src\XrayVpnApp\bin"
    $objDir = Join-Path $projectDir "src\XrayVpnApp\obj"
    if (Test-Path $binDir) {
        Log-Status "  bin folder" "WARN" "Exists (build was attempted)"
    }
    if (Test-Path $objDir) {
        Log-Status "  obj folder" "WARN" "Exists (restore was attempted)"
    }
}

# ============================================================
# Step 10: Check app data folder
# ============================================================
Write-Host "`n>>> Step 10: Checking app data folder..." -ForegroundColor Cyan
$appData = "$env:LOCALAPPDATA\XrayVpn"
if (Test-Path $appData) {
    Log-Status "App data" "OK" $appData
    $logs = Get-ChildItem (Join-Path $appData "logs") -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($logs) {
        Log-Status "Latest log" "OK" $logs.Name
    }
} else {
    Log-Status "App data" "WARN" "Does not exist (app may not have been run yet)"
}

# ============================================================
# Summary
# ============================================================
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "                     DIAGNOSTIC SUMMARY" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Full log saved to: $logFile" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Recommended next steps based on what failed:" -ForegroundColor Yellow
Write-Host ""

if (-not $isAdmin) {
    Write-Host "  [!] Re-run this script as Administrator" -ForegroundColor Red
}
if (-not $dn) {
    Write-Host "  [!] Install .NET 8 SDK:" -ForegroundColor Red
    Write-Host "      winget install Microsoft.DotNet.SDK.8" -ForegroundColor Gray
}
if (-not $git) {
    Write-Host "  [!] Install Git:" -ForegroundColor Red
    Write-Host "      winget install Git.Git" -ForegroundColor Gray
}
if ($projectDir -and -not (Test-Path (Join-Path $resourcesDir "xray.exe"))) {
    Write-Host "  [!] Run download-deps.ps1 to fetch xray.exe, wintun.dll, etc." -ForegroundColor Red
    Write-Host "      cd `"$projectDir`"" -ForegroundColor Gray
    Write-Host "      .\scripts\download-deps.ps1" -ForegroundColor Gray
}
if ($projectDir -and (Test-Path (Join-Path $resourcesDir "xray.exe")) -and -not (Test-Path $publishDir)) {
    Write-Host "  [!] Build the app:" -ForegroundColor Red
    Write-Host "      cd `"$projectDir`"" -ForegroundColor Gray
    Write-Host "      .\scripts\build.ps1 -Portable" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  If build fails, run:" -ForegroundColor Yellow
    Write-Host "      dotnet build XrayVpn.sln -c Release -p:Platform=x64" -ForegroundColor Gray
    Write-Host "  and report the error message." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Diagnostic log: $logFile" -ForegroundColor Yellow
Write-Host "  You can open it with: notepad `"$logFile`"" -ForegroundColor Gray
Write-Host ""

# Copy to clipboard for easy sharing
try {
    Get-Content $logFile | Set-Clipboard
    Write-Host "  [OK] Diagnostic log copied to clipboard." -ForegroundColor Green
    Write-Host "  You can paste it directly into a chat to share." -ForegroundColor Green
} catch {
    Write-Host "  Tip: Open the log file and copy its contents to share." -ForegroundColor Gray
}

Write-Host ""
Read-Host "Press Enter to close"
