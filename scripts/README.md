# scripts/README.md

Helper scripts for the Xray VPN project.

## Available scripts

| Script | Purpose |
|--------|---------|
| `download-deps.ps1` | Download xray-core, wintun.dll, geoip.dat, geosite.dat |
| `build.ps1` | Restore + build + publish single-file EXE (+ installer) |
| `run-dev.ps1` | Run the app in dev mode (no publish) |
| `publish-to-github.ps1` | Initialize git & push to your GitHub account |

## Usage

### First time setup
```powershell
.\scripts\download-deps.ps1
```

### Build the app
```powershell
.\scripts\build.ps1                  # Full release + installer
.\scripts\build.ps1 -Portable        # Portable only, no installer
.\scripts\build.ps1 -Debug           # Debug build
```

### Run in development
```powershell
.\scripts\run-dev.ps1
```

### Publish to GitHub
```powershell
# Option A: Use gh CLI (recommended)
winget install GitHub.cli
gh auth login
.\scripts\publish-to-github.ps1 -GitHubUsername "yourname" -UseCli

# Option B: Use Personal Access Token
.\scripts\publish-to-github.ps1 -GitHubUsername "yourname" -Pat "ghp_xxxxx"

# Option C: Manual — create empty repo on github.com first
.\scripts\publish-to-github.ps1 -GitHubUsername "yourname"
```

## Requirements

- PowerShell 5.0+ (built into Windows 10/11)
- .NET 8 SDK (for build/run-dev)
- Git (for publish-to-github)
- GitHub CLI or PAT (for publish-to-github, optional)
- Inno Setup 6 (optional, for installer)
