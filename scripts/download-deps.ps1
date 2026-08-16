#Requires -Version 5.0
<#
.SYNOPSIS
    Downloads external dependencies (xray-core, wintun.dll, geoip.dat, geosite.dat)
    and places them in src\XrayVpnApp\Resources\.

.DESCRIPTION
    Run this script once before building the project.
    Downloads:
      - Xray-core latest release (xray.exe + geoip.dat + geosite.dat)
      - wintun.dll (latest stable from wintun.net)

.EXAMPLE
    .\scripts\download-deps.ps1
#>

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ResourcesDir = Join-Path $ProjectRoot "src\XrayVpnApp\Resources"

Write-Host "=== Downloading Xray VPN dependencies ===" -ForegroundColor Cyan
Write-Host "Target directory: $ResourcesDir"
Write-Host ""

if (-not (Test-Path $ResourcesDir)) {
    New-Item -ItemType Directory -Path $ResourcesDir -Force | Out-Null
}

# 1. Download Xray-core
Write-Host "[1/3] Fetching Xray-core latest release..." -ForegroundColor Yellow
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/XTLS/Xray-core/releases/latest" -UseBasicParsing
    $version = $release.tag_name
    Write-Host "  Latest version: $version"

    $assetName = "xray-windows-64.zip"
    $asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) {
        throw "Asset $assetName not found in latest release"
    }

    $zipUrl = $asset.browser_download_url
    $zipPath = Join-Path $env:TEMP "xray-core.zip"
    Write-Host "  Downloading from: $zipUrl"
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing

    $extractDir = Join-Path $env:TEMP "xray-extract"
    if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

    Copy-Item (Join-Path $extractDir "xray.exe") (Join-Path $ResourcesDir "xray.exe") -Force
    Copy-Item (Join-Path $extractDir "geoip.dat") (Join-Path $ResourcesDir "geoip.dat") -Force
    Copy-Item (Join-Path $extractDir "geosite.dat") (Join-Path $ResourcesDir "geosite.dat") -Force

    Write-Host "  xray.exe, geoip.dat, geosite.dat copied to Resources\" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR downloading Xray-core: $_" -ForegroundColor Red
    Write-Host "  Please download manually from https://github.com/XTLS/Xray-core/releases" -ForegroundColor Yellow
    Write-Host "  And place xray.exe, geoip.dat, geosite.dat into: $ResourcesDir"
}

# 2. Download wintun.dll
Write-Host ""
Write-Host "[2/3] Fetching wintun.dll..." -ForegroundColor Yellow
try {
    $wintunUrl = "https://www.wintun.net/builds/wintun-0.14.1.zip"
    $wintunZip = Join-Path $env:TEMP "wintun.zip"
    Write-Host "  Downloading from: $wintunUrl"
    Invoke-WebRequest -Uri $wintunUrl -OutFile $wintunZip -UseBasicParsing

    $wintunExtract = Join-Path $env:TEMP "wintun-extract"
    if (Test-Path $wintunExtract) { Remove-Item $wintunExtract -Recurse -Force }
    Expand-Archive -Path $wintunZip -DestinationPath $wintunExtract -Force

    # wintun-amd64.dll is at wintun\bin\amd64\wintun.dll
    $wintunDll = Join-Path $wintunExtract "wintun\bin\amd64\wintun.dll"
    if (-not (Test-Path $wintunDll)) {
        # Fallback: search
        $wintunDll = Get-ChildItem -Path $wintunExtract -Filter "wintun.dll" -Recurse |
                     Where-Object { $_.DirectoryName -like "*amd64*" } |
                     Select-Object -First 1 -ExpandProperty FullName
    }

    if ($wintunDll -and (Test-Path $wintunDll)) {
        Copy-Item $wintunDll (Join-Path $ResourcesDir "wintun.dll") -Force
        Write-Host "  wintun.dll copied to Resources\" -ForegroundColor Green
    } else {
        throw "amd64 wintun.dll not found in archive"
    }
}
catch {
    Write-Host "  ERROR downloading wintun.dll: $_" -ForegroundColor Red
    Write-Host "  Please download manually from https://www.wintun.net/builds/wintun-0.14.1.zip" -ForegroundColor Yellow
    Write-Host "  And place amd64\wintun.dll into: $ResourcesDir"
}

# 3. Verify
Write-Host ""
Write-Host "[3/3] Verifying..." -ForegroundColor Yellow
$expected = @("xray.exe", "geoip.dat", "geosite.dat", "wintun.dll")
$allOk = $true
foreach ($file in $expected) {
    $path = Join-Path $ResourcesDir $file
    if (Test-Path $path) {
        $size = (Get-Item $path).Length
        Write-Host "  [OK] $file ($('{0:N0}' -f $size) bytes)" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $file" -ForegroundColor Red
        $allOk = $false
    }
}

Write-Host ""
if ($allOk) {
    Write-Host "=== All dependencies ready! ===" -ForegroundColor Green
    Write-Host "You can now run: .\scripts\build.ps1"
} else {
    Write-Host "=== Some dependencies are missing ===" -ForegroundColor Red
    Write-Host "Build will fail until all files are present."
}
