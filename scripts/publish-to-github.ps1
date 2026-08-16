#Requires -Version 5.0
<#
.SYNOPSIS
    Publishes the local Xray VPN git repository to a new GitHub repository.

.DESCRIPTION
    This script helps you push the local Xray VPN code to your own GitHub account.

    It supports two modes:
      1. **GitHub CLI (gh)** — automatically creates the remote repo and pushes.
         Requires `gh` CLI installed and authenticated.
      2. **HTTPS with Personal Access Token (PAT)** — you create an empty repo
         on github.com manually, then this script adds the remote and pushes.

.PARAMETER GitHubUsername
    Your GitHub username (e.g., "octocat").

.PARAMETER RepoName
    Name of the repository on GitHub (default: "xray-vpn").

.PARAMETER Private
    Create the repository as private (default: public).

.PARAMETER UseCli
    Use `gh` CLI to create the repo automatically. Otherwise, you must
    create the empty repo on github.com first.

.PARAMETER Pat
    Your GitHub Personal Access Token (only needed if not using gh CLI and
    not already authenticated via git credential helper).

.EXAMPLE
    # Mode 1: Using gh CLI (recommended)
    .\scripts\publish-to-github.ps1 -GitHubUsername "yourname" -UseCli

.EXAMPLE
    # Mode 2: PAT-based (creates repo via API)
    .\scripts\publish-to-github.ps1 -GitHubUsername "yourname" -Pat "ghp_xxxxx"

.EXAMPLE
    # Mode 3: Manual repo creation, then push
    # 1. Go to https://github.com/new
    # 2. Create empty repo named "xray-vpn" (no README, no .gitignore)
    # 3. Run:
    .\scripts\publish-to-github.ps1 -GitHubUsername "yourname"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$GitHubUsername,

    [string]$RepoName = "xray-vpn",

    [switch]$Private,

    [switch]$UseCli,

    [string]$Pat = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== Publishing Xray VPN to GitHub ===" -ForegroundColor Cyan
Write-Host "  Username : $GitHubUsername"
Write-Host "  Repo     : $RepoName"
Write-Host "  Visibility: $(if ($Private) { 'Private' } else { 'Public' })"
Write-Host ""

# 1. Verify git is installed
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: git not found. Install from https://git-scm.com" -ForegroundColor Red
    exit 1
}

# 2. Initialize local repo if needed
Set-Location $ProjectRoot
if (-not (Test-Path ".git")) {
    Write-Host "[1/5] Initializing local git repository..." -ForegroundColor Yellow
    git init
    git branch -M main
} else {
    Write-Host "[1/5] Git repository already initialized" -ForegroundColor Green
}

# 3. Configure git user if not set
$email = git config user.email 2>$null
if (-not $email) {
    Write-Host "  Configuring git user (you can change this later with git config)..." -ForegroundColor Yellow
    $defaultEmail = "$GitHubUsername@users.noreply.github.com"
    git config user.email $defaultEmail
    git config user.name $GitHubUsername
    Write-Host "  Set: $GitHubUsername <$defaultEmail>" -ForegroundColor Green
}

# 4. Add and commit
Write-Host ""
Write-Host "[2/5] Staging files..." -ForegroundColor Yellow
git add -A

$status = git status --porcelain
if ($status) {
    Write-Host "  Committing changes..." -ForegroundColor Yellow
    git commit -m "feat: initial release of Xray VPN for Windows 11

- Multi-protocol support: VLESS, VMess, Trojan, Shadowsocks
- REALITY / XTLS-Vision / Flow support
- Direct TUN adapter via wintun.dll (no third-party wrappers)
- System-wide traffic routing through TUN
- Subscription URL support with auto-update
- JSON config file import
- Bypass Iranian sites & LAN traffic
- Fake DNS + DNS-over-HTTPS
- Latency & download speed testing (pre & post connect)
- Modern Windows 11 UI with Mica-inspired dark theme
- Bilingual UI (فارسی / English) with RTL/LTR auto-switch
- System tray icon with context menu
- Auto-start with Windows (optional)
- Portable single-file EXE distribution
- Inno Setup installer with bilingual wizard
- PowerShell build scripts (download-deps, build, run-dev)
- Comprehensive bilingual documentation

Built with .NET 8 + WPF + CommunityToolkit.Mvvm.
Uses Xray-core (MIT) and wintun.dll (BSD)."
    Write-Host "  Committed" -ForegroundColor Green
} else {
    Write-Host "  Nothing to commit (working tree clean)" -ForegroundColor Yellow
}

# 5. Create remote repo (if using gh CLI or PAT)
$remoteUrl = "https://github.com/$GitHubUsername/$RepoName.git"

if ($UseCli) {
    Write-Host ""
    Write-Host "[3/5] Creating GitHub repo via gh CLI..." -ForegroundColor Yellow
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Host "  gh CLI not found. Install from https://cli.github.com" -ForegroundColor Red
        Write-Host "  Or run: winget install GitHub.cli" -ForegroundColor Yellow
        exit 1
    }

    # Check auth
    $authStatus = gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  gh is not authenticated. Run: gh auth login" -ForegroundColor Red
        exit 1
    }

    $visFlag = if ($Private) { "--private" } else { "--public" }
    gh repo create $RepoName $visFlag --source=. --remote=origin --push
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Failed to create repo. It may already exist." -ForegroundColor Yellow
        Write-Host "  Trying to add remote and push..." -ForegroundColor Yellow
        git remote remove origin 2>$null
        git remote add origin $remoteUrl
        git push -u origin main
    }
    Write-Host "  Repository created and pushed" -ForegroundColor Green
}
elseif ($Pat) {
    Write-Host ""
    Write-Host "[3/5] Creating GitHub repo via API (PAT)..." -ForegroundColor Yellow
    $visibility = if ($Private) { "true" } else { "false" }
    $body = @{
        name = $RepoName
        description = "Xray-based VPN client for Windows 11 with direct TUN adapter"
        private = [bool]$Private
        auto_init = $false
    } | ConvertTo-Json

    $headers = @{
        Authorization = "Bearer $Pat"
        Accept = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
    }

    try {
        Invoke-RestMethod -Uri "https://api.github.com/user/repos" -Method Post -Body $body -Headers $headers -ContentType "application/json"
        Write-Host "  Repository created" -ForegroundColor Green
    } catch {
        Write-Host "  Failed to create repo: $_" -ForegroundColor Yellow
        Write-Host "  The repo may already exist. Continuing with push..." -ForegroundColor Yellow
    }

    # Add remote with PAT embedded (so push doesn't prompt)
    $patUrl = "https://$GitHubUsername`:$Pat@github.com/$GitHubUsername/$RepoName.git"
    git remote remove origin 2>$null
    git remote add origin $patUrl
}
else {
    Write-Host ""
    Write-Host "[3/5] Skipping remote repo creation" -ForegroundColor Yellow
    Write-Host "  IMPORTANT: Create an empty repo at https://github.com/new" -ForegroundColor Yellow
    Write-Host "  Name: $RepoName" -ForegroundColor Yellow
    Write-Host "  DO NOT initialize with README/.gitignore/license" -ForegroundColor Yellow
    Write-Host ""
    $confirm = Read-Host "  Have you created the empty repo? (yes/no)"
    if ($confirm -notmatch "^[yY]") {
        Write-Host "  Aborting. Please create the repo first." -ForegroundColor Red
        exit 1
    }
    git remote remove origin 2>$null
    git remote add origin $remoteUrl
}

# 6. Push
Write-Host ""
Write-Host "[4/5] Pushing to GitHub..." -ForegroundColor Yellow
try {
    git push -u origin main
    Write-Host "  Pushed!" -ForegroundColor Green
} catch {
    Write-Host "  Push failed: $_" -ForegroundColor Red
    Write-Host "  If using PAT mode, the URL with embedded token may have been saved in .git/config" -ForegroundColor Yellow
    Write-Host "  To remove it: git remote set-url origin $remoteUrl" -ForegroundColor Yellow
    exit 1
}

# 7. Done
Write-Host ""
Write-Host "[5/5] Done!" -ForegroundColor Green
Write-Host ""
Write-Host "  Your repository: https://github.com/$GitHubUsername/$RepoName" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Next steps:" -ForegroundColor Yellow
Write-Host "    1. Add a description on the repo page"
Write-Host "    2. Add topics: vpn, xray, v2ray, windows, wpf, dotnet"
Write-Host "    3. Star your own repo ⭐"
Write-Host "    4. Share with friends!"
Write-Host ""
Write-Host "  To push future changes:" -ForegroundColor Yellow
Write-Host "    git add -A"
Write-Host "    git commit -m 'feat: ...'"
Write-Host "    git push"
