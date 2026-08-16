#Requires -Version 5.0
<#
.SYNOPSIS
    Runs the Xray VPN app in dev mode (no publish).

.DESCRIPTION
    Builds and runs the app directly from source.
    Useful for development & testing.
#>

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== Running Xray VPN (dev mode) ===" -ForegroundColor Cyan

# Check dependencies
$resourcesDir = Join-Path $ProjectRoot "src\XrayVpnApp\Resources"
$expected = @("xray.exe", "wintun.dll", "geoip.dat", "geosite.dat")
foreach ($f in $expected) {
    if (-not (Test-Path (Join-Path $resourcesDir $f))) {
        Write-Host "Missing: $f. Run download-deps.ps1 first." -ForegroundColor Red
        exit 1
    }
}

# Run
& dotnet run --project (Join-Path $ProjectRoot "src\XrayVpnApp\XrayVpnApp.csproj") -c Debug
