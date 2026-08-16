#Requires -Version 5.0
<#
.SYNOPSIS
    Builds the Xray VPN client for Windows 11.

.DESCRIPTION
    1. Ensures .NET 8 SDK is installed
    2. Ensures all dependencies are downloaded (calls download-deps.ps1 if needed)
    3. Restores NuGet packages
    4. Builds Release configuration
    5. Publishes a self-contained single-file .exe

.PARAMETER Portable
    If set, produces a portable single-EXE build (no installer).
    Otherwise, also builds the Inno Setup installer.

.PARAMETER Debug
    Build Debug configuration instead of Release.

.EXAMPLE
    .\scripts\build.ps1
    .\scripts\build.ps1 -Portable
    .\scripts\build.ps1 -Debug
#>

param(
    [switch]$Portable,
    [switch]$Debug
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

$Config = if ($Debug) { "Debug" } else { "Release" }
$OutputDir = Join-Path $ProjectRoot "build\$Config"

Write-Host "=== Building Xray VPN ($Config) ===" -ForegroundColor Cyan
Write-Host ""

# 1. Verify .NET SDK
Write-Host "[1/5] Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = & dotnet --version 2>$null
    if (-not $dotnetVersion -or $dotnetVersion -lt "8.0") {
        Write-Host "  .NET 8 SDK not found. Installing..." -ForegroundColor Yellow
        Write-Host "  Please install from: https://dotnet.microsoft.com/download/dotnet/8.0"
        Write-Host "  Or run: winget install Microsoft.DotNet.SDK.8"
        exit 1
    }
    Write-Host "  .NET SDK $dotnetVersion OK" -ForegroundColor Green
} catch {
    Write-Host "  .NET SDK not found. Please install .NET 8 SDK first." -ForegroundColor Red
    exit 1
}

# 2. Ensure dependencies
Write-Host ""
Write-Host "[2/5] Checking dependencies..." -ForegroundColor Yellow
$resourcesDir = Join-Path $ProjectRoot "src\XrayVpnApp\Resources"
$expectedFiles = @("xray.exe", "geoip.dat", "geosite.dat", "wintun.dll")
$needDownload = $false
foreach ($f in $expectedFiles) {
    if (-not (Test-Path (Join-Path $resourcesDir $f))) {
        $needDownload = $true
        break
    }
}
if ($needDownload) {
    Write-Host "  Some dependencies missing. Running download-deps.ps1..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "download-deps.ps1")
} else {
    Write-Host "  All dependencies present" -ForegroundColor Green
}

# 3. Restore packages
Write-Host ""
Write-Host "[3/5] Restoring NuGet packages..." -ForegroundColor Yellow
& dotnet restore (Join-Path $ProjectRoot "XrayVpn.sln")
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Restore failed" -ForegroundColor Red
    exit 1
}
Write-Host "  Packages restored" -ForegroundColor Green

# 4. Build
Write-Host ""
Write-Host "[4/5] Building ($Config)..." -ForegroundColor Yellow
& dotnet build (Join-Path $ProjectRoot "XrayVpn.sln") -c $Config -p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "  Build succeeded" -ForegroundColor Green

# 5. Publish single-file
Write-Host ""
Write-Host "[5/5] Publishing single-file executable..." -ForegroundColor Yellow
$publishDir = Join-Path $OutputDir "publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

& dotnet publish (Join-Path $ProjectRoot "src\XrayVpnApp\XrayVpnApp.csproj") `
    -c $Config `
    -r win-x64 `
    -p:Platform=x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "  Publish failed" -ForegroundColor Red
    exit 1
}

# Copy resources to publish dir
Copy-Item (Join-Path $resourcesDir "xray.exe") $publishDir -Force
Copy-Item (Join-Path $resourcesDir "wintun.dll") $publishDir -Force
Copy-Item (Join-Path $resourcesDir "geoip.dat") $publishDir -Force
Copy-Item (Join-Path $resourcesDir "geosite.dat") $publishDir -Force

Write-Host ""
Write-Host "=== Build complete! ===" -ForegroundColor Green
Write-Host "Output: $publishDir"
Write-Host ""
Write-Host "Contents:"
Get-ChildItem $publishDir | ForEach-Object {
    $size = if ($_.Length -gt 1MB) { "{0:N1} MB" -f ($_.Length / 1MB) }
            elseif ($_.Length -gt 1KB) { "{0:N1} KB" -f ($_.Length / 1KB) }
            else { "$($_.Length) B" }
    Write-Host ("  {0,-30} {1}" -f $_.Name, $size)
}

# Build installer if not Portable
if (-not $Portable -and -not $Debug) {
    Write-Host ""
    Write-Host "=== Building Installer ===" -ForegroundColor Cyan
    $installerScript = Join-Path $ProjectRoot "src\XrayVpnApp.Installer\setup.iss"
    if (Test-Path $installerScript) {
        $iscc = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
        if ($iscc) {
            & iscc $installerScript
            Write-Host "  Installer built" -ForegroundColor Green
        } else {
            Write-Host "  Inno Setup (iscc.exe) not found on PATH" -ForegroundColor Yellow
            Write-Host "  Install from: https://jrsoftware.org/isdl.php"
            Write-Host "  Or run: winget install JRSoftware.InnoSetup"
            Write-Host "  Then manually compile: $installerScript"
        }
    }
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
