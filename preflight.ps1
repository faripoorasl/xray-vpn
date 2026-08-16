#Requires -Version 5.0
<#
.SYNOPSIS
    Pre-flight check for Xray VPN build.
    Run this BEFORE setup.bat to verify all files are in correct state.

.DESCRIPTION
    Checks:
      - All required source files exist
      - File sizes look reasonable
      - No leftover .obj/.bin folders from previous builds
      - .NET 8 SDK is installed
      - Reports any issues found
#>

$ErrorActionPreference = "Continue"
$host.UI.RawUI.WindowTitle = "Xray VPN - Pre-flight Check"

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "       Xray VPN - Pre-flight Check" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Find project folder
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoDir = $ScriptDir
if (-not (Test-Path (Join-Path $RepoDir 'XrayVpn.sln'))) {
    $RepoDir = Join-Path $ScriptDir 'xray-vpn-main'
    if (-not (Test-Path $RepoDir)) {
        # Search common locations
        $candidates = @(
            "$env:USERPROFILE\Desktop\xray-vpn-main",
            "$env:USERPROFILE\Desktop\xray-vpn",
            "$env:USERPROFILE\Downloads\xray-vpn-main",
            "$env:USERPROFILE\Downloads\xray-vpn"
        )
        foreach ($c in $candidates) {
            if (Test-Path (Join-Path $c 'XrayVpn.sln')) {
                $RepoDir = $c
                break
            }
        }
    }
}

if (-not (Test-Path (Join-Path $RepoDir 'XrayVpn.sln'))) {
    Write-Host "  [FAIL] Could not find XrayVpn.sln" -ForegroundColor Red
    Write-Host "  Make sure you extracted the project ZIP to a folder."
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "  Project folder: $RepoDir" -ForegroundColor Green
Write-Host ""

$issues = @()

# ============================================================
# Check 1: Required source files
# ============================================================
Write-Host ">>> Check 1: Required source files..." -ForegroundColor Cyan

$requiredFiles = @(
    @{Path='XrayVpn.sln'; MinSize=500},
    @{Path='install.ps1'; MinSize=10000},
    @{Path='setup.bat'; MinSize=500},
    @{Path='nuget.config'; MinSize=100},
    @{Path='src\XrayVpnApp\XrayVpnApp.csproj'; MinSize=1000},
    @{Path='src\XrayVpnApp\app.manifest'; MinSize=500},
    @{Path='src\XrayVpnApp\App.xaml'; MinSize=300},
    @{Path='src\XrayVpnApp\App.xaml.cs'; MinSize=1500},
    @{Path='src\XrayVpnApp\GlobalUsings.cs'; MinSize=500},
    @{Path='src\XrayVpnApp\Models\ServerConfig.cs'; MinSize=3000},
    @{Path='src\XrayVpnApp\Models\AppSettings.cs'; MinSize=1500},
    @{Path='src\XrayVpnApp\Models\ServerStore.cs'; MinSize=200},
    @{Path='src\XrayVpnApp\Services\Logger.cs'; MinSize=500},
    @{Path='src\XrayVpnApp\Services\AppSettingsService.cs'; MinSize=800},
    @{Path='src\XrayVpnApp\Services\ConfigParserService.cs'; MinSize=10000},
    @{Path='src\XrayVpnApp\Services\XrayConfigGenerator.cs'; MinSize=8000},
    @{Path='src\XrayVpnApp\Services\XrayCoreService.cs'; MinSize=3000},
    @{Path='src\XrayVpnApp\Services\TunService.cs'; MinSize=4000},
    @{Path='src\XrayVpnApp\Services\RoutingService.cs'; MinSize=1500},
    @{Path='src\XrayVpnApp\Services\DnsService.cs'; MinSize=1500},
    @{Path='src\XrayVpnApp\Services\SpeedTestService.cs'; MinSize=2500},
    @{Path='src\XrayVpnApp\Services\SubscriptionService.cs'; MinSize=1500},
    @{Path='src\XrayVpnApp\Services\TrayService.cs'; MinSize=1500},
    @{Path='src\XrayVpnApp\Services\LanguageService.cs'; MinSize=700},
    @{Path='src\XrayVpnApp\Utils\WintunNative.cs'; MinSize=2000},
    @{Path='src\XrayVpnApp\ViewModels\MainViewModel.cs'; MinSize=4000},
    @{Path='src\XrayVpnApp\Converters\Converters.cs'; MinSize=500},
    @{Path='src\XrayVpnApp\Views\MainWindow.xaml'; MinSize=2000},
    @{Path='src\XrayVpnApp\Views\MainWindow.xaml.cs'; MinSize=1500},
    @{Path='src\XrayVpnApp\Views\ServersPage.xaml'; MinSize=2000},
    @{Path='src\XrayVpnApp\Views\ServersPage.xaml.cs'; MinSize=800},
    @{Path='src\XrayVpnApp\Views\SubscriptionsPage.xaml'; MinSize=1500},
    @{Path='src\XrayVpnApp\Views\SubscriptionsPage.xaml.cs'; MinSize=1500},
    @{Path='src\XrayVpnApp\Views\SpeedTestPage.xaml'; MinSize=1500},
    @{Path='src\XrayVpnApp\Views\SpeedTestPage.xaml.cs'; MinSize=1000},
    @{Path='src\XrayVpnApp\Views\SettingsPage.xaml'; MinSize=3000},
    @{Path='src\XrayVpnApp\Views\SettingsPage.xaml.cs'; MinSize=2000},
    @{Path='src\XrayVpnApp\Views\LogsPage.xaml'; MinSize=500},
    @{Path='src\XrayVpnApp\Views\LogsPage.xaml.cs'; MinSize=600},
    @{Path='src\XrayVpnApp\Views\AboutPage.xaml'; MinSize=1500},
    @{Path='src\XrayVpnApp\Views\AboutPage.xaml.cs'; MinSize=500},
    @{Path='src\XrayVpnApp\Resources\Theme.xaml'; MinSize=3000},
    @{Path='src\XrayVpnApp\Resources\Strings.xaml'; MinSize=100},
    @{Path='src\XrayVpnApp\Resources\Strings.fa.xaml'; MinSize=2000},
    @{Path='src\XrayVpnApp\Resources\Strings.en.xaml'; MinSize=2000}
)

$missing = @()
$wrongSize = @()
foreach ($f in $requiredFiles) {
    $fullPath = Join-Path $RepoDir $f.Path
    if (-not (Test-Path $fullPath)) {
        $missing += $f.Path
        Write-Host "  [MISSING] $($f.Path)" -ForegroundColor Red
    } else {
        $size = (Get-Item $fullPath).Length
        if ($size -lt $f.MinSize) {
            $wrongSize += "$($f.Path) ($size bytes, expected >= $($f.MinSize))"
            Write-Host "  [WARN] $($f.Path) - too small ($size bytes)" -ForegroundColor Yellow
        }
    }
}

if ($missing.Count -eq 0 -and $wrongSize.Count -eq 0) {
    Write-Host "  [OK] All $($requiredFiles.Count) source files present" -ForegroundColor Green
} else {
    if ($missing.Count -gt 0) {
        $issues += "Missing files: $($missing.Count)"
    }
    if ($wrongSize.Count -gt 0) {
        $issues += "Wrong-size files: $($wrongSize.Count)"
    }
}

# ============================================================
# Check 2: ConfigParserService.cs has the static method fix
# ============================================================
Write-Host ""
Write-Host ">>> Check 2: ConfigParserService.cs has latest fix..." -ForegroundColor Cyan
$cpPath = Join-Path $RepoDir 'src\XrayVpnApp\Services\ConfigParserService.cs'
if (Test-Path $cpPath) {
    $cpContent = Get-Content $cpPath -Raw
    $hasBug = $cpContent -match 'this Dictionary<string, string> dict'
    $hasFix = $cpContent -match 'DictGet\(Dictionary<string, string> dict'
    if ($hasBug -and -not $hasFix) {
        Write-Host "  [FAIL] File has OLD version (extension method bug)" -ForegroundColor Red
        $issues += "ConfigParserService.cs is OLD version (still has extension method)"
    } elseif ($hasFix) {
        Write-Host "  [OK] Latest fix present (static method)" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] Could not determine version" -ForegroundColor Yellow
    }
} else {
    Write-Host "  [FAIL] File not found" -ForegroundColor Red
}

# ============================================================
# Check 3: GlobalUsings.cs exists
# ============================================================
Write-Host ""
Write-Host ">>> Check 3: GlobalUsings.cs exists..." -ForegroundColor Cyan
$guPath = Join-Path $RepoDir 'src\XrayVpnApp\GlobalUsings.cs'
if (Test-Path $guPath) {
    $guContent = Get-Content $guPath -Raw
    if ($guContent -match 'global using Application = System.Windows.Application') {
        Write-Host "  [OK] GlobalUsings.cs present and correct" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] GlobalUsings.cs exists but may be wrong version" -ForegroundColor Yellow
    }
} else {
    Write-Host "  [FAIL] GlobalUsings.cs missing" -ForegroundColor Red
    $issues += "GlobalUsings.cs missing"
}

# ============================================================
# Check 4: AboutPage.xaml has &amp; (not raw &)
# ============================================================
Write-Host ""
Write-Host ">>> Check 4: AboutPage.xaml XML fix..." -ForegroundColor Cyan
$apPath = Join-Path $RepoDir 'src\XrayVpnApp\Views\AboutPage.xaml'
if (Test-Path $apPath) {
    $apContent = Get-Content $apPath -Raw
    if ($apContent -match 'personal & commercial') {
        Write-Host "  [FAIL] File has OLD version (raw & bug)" -ForegroundColor Red
        $issues += "AboutPage.xaml is OLD version (raw & character)"
    } elseif ($apContent -match 'personal &amp; commercial') {
        Write-Host "  [OK] Latest fix present (&amp;)" -ForegroundColor Green
    }
} else {
    Write-Host "  [FAIL] File not found" -ForegroundColor Red
}

# ============================================================
# Check 5: XrayVpnApp.csproj does NOT have QRSharp
# ============================================================
Write-Host ""
Write-Host ">>> Check 5: XrayVpnApp.csproj is clean..." -ForegroundColor Cyan
$cpPath = Join-Path $RepoDir 'src\XrayVpnApp\XrayVpnApp.csproj'
if (Test-Path $cpPath) {
    $cpContent = Get-Content $cpPath -Raw
    if ($cpContent -match 'QRSharp') {
        Write-Host "  [FAIL] File has OLD version (still references QRSharp)" -ForegroundColor Red
        $issues += "XrayVpnApp.csproj is OLD version (still has QRSharp)"
    } else {
        Write-Host "  [OK] Latest version (QRSharp removed)" -ForegroundColor Green
    }
}

# ============================================================
# Check 6: Stale build artifacts
# ============================================================
Write-Host ""
Write-Host ">>> Check 6: Stale build artifacts..." -ForegroundColor Cyan
$stalePaths = @(
    'src\XrayVpnApp\obj',
    'src\XrayVpnApp\bin',
    'build'
)
$staleFound = @()
foreach ($p in $stalePaths) {
    $fullPath = Join-Path $RepoDir $p
    if (Test-Path $fullPath) {
        $staleFound += $p
        Write-Host "  [WARN] Found stale folder: $p" -ForegroundColor Yellow
    }
}
if ($staleFound.Count -eq 0) {
    Write-Host "  [OK] No stale build artifacts" -ForegroundColor Green
} else {
    $issues += "Stale folders: $($staleFound -join ', ')"
    Write-Host ""
    $clean = Read-Host "Clean stale folders now? (Y/n)"
    if ($clean -notmatch '^[nN]') {
        foreach ($p in $staleFound) {
            $fullPath = Join-Path $RepoDir $p
            Remove-Item $fullPath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  Removed: $p" -ForegroundColor Green
        }
        $issues = $issues | Where-Object { $_ -notmatch 'Stale folders' }
    }
}

# ============================================================
# Check 7: .NET 8 SDK
# ============================================================
Write-Host ""
Write-Host ">>> Check 7: .NET 8 SDK..." -ForegroundColor Cyan
$dn = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dn) {
    $sdks = & dotnet --list-sdks 2>$null
    if ($sdks -match '8\.') {
        Write-Host "  [OK] .NET 8 SDK installed" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] .NET 8 SDK not found" -ForegroundColor Red
        $issues += ".NET 8 SDK not installed"
    }
} else {
    Write-Host "  [FAIL] dotnet not on PATH" -ForegroundColor Red
    $issues += "dotnet not on PATH"
}

# ============================================================
# Check 8: Bundled dependencies (xray.exe, wintun.dll, etc.)
# ============================================================
Write-Host ""
Write-Host ">>> Check 8: Bundled dependencies..." -ForegroundColor Cyan
$resourcesDir = Join-Path $RepoDir 'src\XrayVpnApp\Resources'
$bundledFiles = @(
    @{Name='xray.exe'; MinSize=10MB},
    @{Name='wintun.dll'; MinSize=100KB},
    @{Name='geoip.dat'; MinSize=1MB},
    @{Name='geosite.dat'; MinSize=1MB}
)
$missingBundled = @()
foreach ($f in $bundledFiles) {
    $p = Join-Path $resourcesDir $f.Name
    if (Test-Path $p) {
        $size = (Get-Item $p).Length
        if ($size -ge $f.MinSize) {
            Write-Host ("  [OK] {0,-15} {1,10:N0} bytes" -f $f.Name, $size) -ForegroundColor Green
        } else {
            Write-Host ("  [FAIL] {0,-15} too small ({1:N0} bytes)" -f $f.Name, $size) -ForegroundColor Red
            $missingBundled += $f.Name
        }
    } else {
        Write-Host "  [FAIL] $($f.Name) missing" -ForegroundColor Red
        $missingBundled += $f.Name
    }
}
if ($missingBundled.Count -gt 0) {
    $issues += "Bundled deps missing: $($missingBundled -join ', ')"
}

# ============================================================
# Check 9: Bundled NuGet packages
# ============================================================
Write-Host ""
Write-Host ">>> Check 9: Bundled NuGet packages..." -ForegroundColor Cyan
$localPkgsDir = Join-Path $RepoDir 'local-packages'
if (Test-Path $localPkgsDir) {
    $nupkgs = Get-ChildItem -Path $localPkgsDir -Filter '*.nupkg' -ErrorAction SilentlyContinue
    if ($nupkgs.Count -gt 0) {
        Write-Host "  [OK] Found $($nupkgs.Count) .nupkg files:" -ForegroundColor Green
        foreach ($p in $nupkgs) {
            Write-Host ("        {0,-50} {1,8:N0} bytes" -f $p.Name, $p.Length) -ForegroundColor Gray
        }
    } else {
        Write-Host "  [WARN] local-packages folder is empty" -ForegroundColor Yellow
        Write-Host "         Online NuGet restore will be used (needs internet)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  [WARN] local-packages folder not found" -ForegroundColor Yellow
    Write-Host "         Online NuGet restore will be used (needs internet)" -ForegroundColor Yellow
}

# ============================================================
# Summary
# ============================================================
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "                    SUMMARY" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

if ($issues.Count -eq 0) {
    Write-Host "  All checks passed! Ready to build." -ForegroundColor Green
    Write-Host ""
    Write-Host "  Next: run setup.bat to install." -ForegroundColor Yellow
} else {
    Write-Host "  Issues found ($($issues.Count)):" -ForegroundColor Red
    foreach ($i in $issues) {
        Write-Host "    - $i" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "  Recommended action:" -ForegroundColor Yellow
    Write-Host "    1. Delete your current xray-vpn folder" -ForegroundColor White
    Write-Host "    2. Download the latest ZIP from:" -ForegroundColor White
    Write-Host "       https://github.com/faripoorasl/xray-vpn/archive/refs/heads/main.zip" -ForegroundColor Cyan
    Write-Host "    3. Extract it to a new folder" -ForegroundColor White
    Write-Host "    4. Run preflight.ps1 again to verify" -ForegroundColor White
    Write-Host "    5. Then run setup.bat" -ForegroundColor White
}

Write-Host ""
Read-Host "Press Enter to close"
