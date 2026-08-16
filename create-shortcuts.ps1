#Requires -Version 5.0
<#
.SYNOPSIS
    Creates Desktop & Start Menu shortcuts for Xray VPN.
    Use this if the main installer completed but shortcuts were not created.

.DESCRIPTION
    Searches common locations for XrayVpn.exe and creates shortcuts.
#>

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host ""
Write-Host "=== Xray VPN - Shortcut Creator ===" -ForegroundColor Cyan
Write-Host ""

# ---- 1. Find XrayVpn.exe ----
Write-Host "Searching for XrayVpn.exe..." -ForegroundColor Yellow

$candidates = @()

# Common locations
$searchPaths = @(
    "$env:USERPROFILE\Desktop\xray-vpn-main\build\Release\publish\XrayVpn.exe",
    "$env:USERPROFILE\Desktop\xray-vpn\build\Release\publish\XrayVpn.exe",
    "$env:USERPROFILE\Downloads\xray-vpn-main\build\Release\publish\XrayVpn.exe",
    "$env:USERPROFILE\Downloads\xray-vpn\build\Release\publish\XrayVpn.exe",
    "$env:USERPROFILE\xray-vpn\build\Release\publish\XrayVpn.exe",
    "C:\xray-vpn\build\Release\publish\XrayVpn.exe",
    "D:\xray-vpn\build\Release\publish\XrayVpn.exe"
)

foreach ($p in $searchPaths) {
    if (Test-Path $p) {
        $candidates += $p
    }
}

# Search filesystem as fallback
if ($candidates.Count -eq 0) {
    Write-Host "  Not found in common locations. Searching C:\ and D:\..." -ForegroundColor Yellow
    $found = Get-ChildItem -Path "$env:USERPROFILE", "C:\xray-vpn", "D:\" -Filter "XrayVpn.exe" -Recurse -ErrorAction SilentlyContinue -Depth 6 2>$null
    foreach ($f in $found) {
        $candidates += $f.FullName
    }
}

if ($candidates.Count -eq 0) {
    Write-Host ""
    Write-Host "[ERROR] XrayVpn.exe was not found." -ForegroundColor Red
    Write-Host ""
    Write-Host "This means the build did not complete successfully."
    Write-Host "Please re-run setup.bat as Administrator."
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

# Use the first match (or let user pick if multiple)
$exePath = $candidates[0]
if ($candidates.Count -gt 1) {
    Write-Host ""
    Write-Host "Multiple XrayVpn.exe found:" -ForegroundColor Yellow
    for ($i = 0; $i -lt $candidates.Count; $i++) {
        Write-Host "  [$i] $($candidates[$i])"
    }
    $choice = Read-Host "Pick one (0-$($candidates.Count-1))"
    $exePath = $candidates[[int]$choice]
}

$publishDir = Split-Path -Parent $exePath
Write-Host ""
Write-Host "  Found: $exePath" -ForegroundColor Green
Write-Host ""

# ---- 2. Verify dependencies exist alongside ----
$deps = @("xray.exe", "wintun.dll", "geoip.dat", "geosite.dat")
$missing = @()
foreach ($d in $deps) {
    $depPath = Join-Path $publishDir $d
    if (-not (Test-Path $depPath)) {
        $missing += $d
    }
}
if ($missing.Count -gt 0) {
    Write-Host "[WARNING] Missing dependencies:" -ForegroundColor Yellow
    foreach ($m in $missing) {
        Write-Host "  - $m" -ForegroundColor Yellow
    }
    Write-Host "The app may not run correctly without these files." -ForegroundColor Yellow
    Write-Host ""
}

# ---- 3. Create Desktop shortcut ----
Write-Host "Creating Desktop shortcut..." -ForegroundColor Yellow
try {
    $shell = New-Object -ComObject WScript.Shell
    $desktop = [Environment]::GetFolderPath("Desktop")
    $desktopShortcut = Join-Path $desktop "Xray VPN.lnk"

    $sc = $shell.CreateShortcut($desktopShortcut)
    $sc.TargetPath = $exePath
    $sc.WorkingDirectory = $publishDir
    $sc.Description = "Xray VPN - Xray-based VPN client for Windows 11"
    $sc.WindowStyle = 1
    $sc.Save()

    Write-Host "  [OK] $desktopShortcut" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

# ---- 4. Create Start Menu shortcut ----
Write-Host "Creating Start Menu shortcut..." -ForegroundColor Yellow
try {
    $startMenu = [Environment]::GetFolderPath("Programs")
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

    Write-Host "  [OK] $startShortcut" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

# ---- 5. Done ----
Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Shortcuts created!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  App location : $exePath"
Write-Host "  Desktop      : Xray VPN (shortcut)"
Write-Host "  Start Menu   : Xray VPN (shortcut)"
Write-Host ""
$launch = Read-Host "Launch Xray VPN now? (Y/n)"
if ($launch -notmatch "^[nN]") {
    Start-Process $exePath -Verb RunAs
}
Write-Host ""
Read-Host "Press Enter to close"
