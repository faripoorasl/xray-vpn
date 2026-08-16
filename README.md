# 🛡 Xray VPN

> یک کلاینت VPN نتیو ویندوز 11 مبتنی بر Xray-core با TUN adapter مستقیم
>
> A native Windows 11 VPN client built on Xray-core with direct TUN adapter support

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D4.svg)](https://www.microsoft.com/windows)

## ✨ Features / امکانات

### 🌐 Multi-Protocol Support
- **VLESS** (with REALITY / XTLS-Vision / Flow)
- **VMess** (with AEAD / legacy support)
- **Trojan**
- **Shadowsocks** (all ciphers)
- **Subscription URL** (auto-update)
- **JSON config** file import

### 🔒 Direct TUN Mode
- Uses `wintun.dll` (WireGuard driver) directly — no third-party TUN wrappers
- Creates a real network adapter that captures **all system traffic**
- No need for system-wide proxy settings
- Works with UWP apps (Microsoft Store), games, and any TCP/UDP app

### 🛠 Configuration & Routing
- Bypass Iranian sites (`geosite:category-ir`, `geoip:ir`)
- Bypass LAN traffic
- Block ads (`geosite:category-ads-all`)
- Custom routing rules
- DNS-over-HTTPS support
- Fake DNS for DNS leak prevention

### 📊 Speed & Latency Testing
- Pre-connect latency test (TCP handshake through SOCKS)
- Pre-connect download speed test (25MB Cloudflare file)
- Post-connect speed test (when VPN is active)
- Bulk test all servers with one click

### 🎨 Modern Windows 11 UI
- Built with WPF .NET 8 + Mica-inspired dark theme
- **Bilingual** UI (فارسی / English) — switch from Settings
- RTL/LTR auto-switch
- System tray icon with context menu
- Minimize to tray, close to tray
- Auto-start with Windows (optional)

### 📦 Distribution
- **Portable** build: single `XrayVpn.exe` (~80MB self-contained)
- **Installer** build: Inno Setup wizard with bilingual UI
- No .NET runtime install required (self-contained)
- No admin-only install (run-time admin is needed for TUN)

---

## 🚀 Quick Start / شروع سریع

### Prerequisites
- Windows 11 (x64) — Windows 10 also works
- .NET 8 SDK (for building only)
- ~150MB free disk space

### Build from source

```powershell
# 1. Clone the repo
git clone https://github.com/yourname/xray-vpn.git
cd xray-vpn

# 2. Download dependencies (xray-core, wintun.dll, geoip)
.\scripts\download-deps.ps1

# 3. Build & publish
.\scripts\build.ps1

# 4. (Optional) Build installer
.\scripts\build.ps1   # automatically builds installer if Inno Setup is installed
```

### Run in dev mode

```powershell
.\scripts\run-dev.ps1
```

---

## 📖 Usage / نحوه استفاده

### 1. Add a server
- Open the app
- Go to **Servers** tab
- Paste a `vless://`, `vmess://`, `trojan://`, or `ss://` link in the text box
- Click **+** or press `Ctrl+Enter`

### 2. Add a subscription
- Go to **Subscriptions** tab
- Enter name + URL
- Click **Add Subscription** — servers are fetched automatically

### 3. Connect
- Select a server from the list
- Click the big **Connect** button at the bottom
- A UAC prompt may appear (TUN requires admin rights)
- Status turns green when connected

### 4. Test servers
- Click 🛰 for latency test
- Click ⚡ for download speed test
- Or click **Test All** to test every server in sequence

### 5. Configure DNS / Routing
- Go to **Settings** tab
- Adjust DNS, Fake DNS, DoH, routing rules as needed
- Click **Save**

---

## 🏗 Architecture / معماری

```
XrayVpn/
├── src/
│   ├── XrayVpnApp/                  # Main WPF application (.NET 8)
│   │   ├── Models/                  # Data models
│   │   │   ├── ServerConfig.cs      # Server config (VLESS/VMess/Trojan/SS)
│   │   │   ├── AppSettings.cs       # Persisted app settings
│   │   │   └── ServerStore.cs       # Collection of servers + subs
│   │   ├── Services/                # Core services
│   │   │   ├── ConfigParserService.cs    # Parse share-links & JSON
│   │   │   ├── XrayConfigGenerator.cs    # Build Xray JSON config
│   │   │   ├── XrayCoreService.cs        # Manage xray.exe process
│   │   │   ├── TunService.cs             # wintun.dll + TUN adapter
│   │   │   ├── RoutingService.cs         # Route table manipulation
│   │   │   ├── DnsService.cs             # Windows DNS settings
│   │   │   ├── SpeedTestService.cs       # Latency + download speed
│   │   │   ├── SubscriptionService.cs    # Subscription URL fetcher
│   │   │   ├── TrayService.cs            # System tray + autostart
│   │   │   ├── LanguageService.cs        # i18n
│   │   │   ├── AppSettingsService.cs     # Load/save settings JSON
│   │   │   └── Logger.cs                 # File logger
│   │   ├── Utils/
│   │   │   └── WintunNative.cs      # P/Invoke for wintun.dll + iphlpapi
│   │   ├── ViewModels/
│   │   │   └── MainViewModel.cs     # MVVM view model
│   │   ├── Views/                   # WPF pages
│   │   │   ├── MainWindow.xaml
│   │   │   ├── ServersPage.xaml
│   │   │   ├── SubscriptionsPage.xaml
│   │   │   ├── SpeedTestPage.xaml
│   │   │   ├── SettingsPage.xaml
│   │   │   ├── LogsPage.xaml
│   │   │   └── AboutPage.xaml
│   │   ├── Resources/
│   │   │   ├── Theme.xaml           # Dark theme
│   │   │   ├── Strings.fa.xaml      # Persian strings
│   │   │   ├── Strings.en.xaml      # English Strings
│   │   │   ├── xray.exe             # (downloaded)
│   │   │   ├── wintun.dll           # (downloaded)
│   │   │   ├── geoip.dat            # (downloaded)
│   │   │   └── geosite.dat          # (downloaded)
│   │   ├── Converters/
│   │   │   └── Converters.cs        # WPF value converters
│   │   ├── App.xaml / .cs           # App entry + DI
│   │   ├── XrayVpnApp.csproj        # .NET 8 + WPF project file
│   │   └── app.manifest             # Require admin + Win11 compat
│   └── XrayVpnApp.Installer/        # Inno Setup installer
│       └── setup.iss
├── scripts/
│   ├── download-deps.ps1            # Fetch xray-core + wintun.dll
│   ├── build.ps1                    # Full build pipeline
│   └── run-dev.ps1                  # Dev runner
├── docs/
│   ├── BUILD.md
│   └── USAGE.md
├── XrayVpn.sln
└── README.md (this file)
```

### How it works

```
┌──────────────────────────────────────────────────────────┐
│                  XrayVpn.exe (WPF UI)                    │
│                                                            │
│  ┌──────────────┐    ┌──────────────┐    ┌─────────────┐ │
│  │ ConfigParser │───▶│ ConfigGen    │───▶│ XrayCore    │ │
│  │ (vmess/vless │    │ (JSON build) │    │ (subprocess)│ │
│  │  trojan/ss)  │    └──────────────┘    └──────┬──────┘ │
│  └──────────────┘                                 │        │
│                                                   ▼        │
│  ┌──────────────┐    ┌──────────────┐    ┌─────────────┐ │
│  │ TunService   │◀──│ RoutingSvc   │◀──│ TUN Adapter │ │
│  │ (wintun.dll) │    │ (route add)  │    │ (10.10.0.x) │ │
│  └──────────────┘    └──────────────┘    └─────────────┘ │
│         │                                                 │
│         ▼                                                 │
│  ┌──────────────┐    ┌──────────────┐                   │
│  │ DnsService   │    │ All system   │                   │
│  │ (netsh ip)   │    │ traffic →TUN │                   │
│  └──────────────┘    └──────────────┘                   │
└──────────────────────────────────────────────────────────┘
```

When you click **Connect**:

1. **ConfigParser** parses the share-link into a `ServerConfig` object
2. **XrayConfigGenerator** builds a complete Xray JSON config (inbounds + outbounds + routing + DNS)
3. **XrayCoreService** launches `xray.exe run -c config.json` as a child process
4. **TunService** calls `WintunCreateAdapter` to create a virtual network adapter named `XrayVpn`
5. `netsh interface ip set address` assigns the IP `10.10.0.2/24` to the TUN adapter
6. `route add 0.0.0.0 mask 0.0.0.0 10.10.0.1 metric 5 if <index>` redirects all IPv4 traffic to TUN
7. **DnsService** sets the system DNS via `netsh interface ip set dns`
8. Xray's `dokodemo-door` inbound listens on `10.10.0.2:1080` and forwards to the proxy outbound

On disconnect, all of this is reversed and the TUN adapter is destroyed.

---

## 🔧 Build Requirements

| Component | Version | Purpose |
|-----------|---------|---------|
| Windows SDK | 10.0.22621+ | WPF + Win32 API |
| .NET SDK | 8.0+ | Build toolchain |
| Inno Setup | 6.x | Installer (optional) |
| PowerShell | 5.0+ | Build scripts |

Install via winget:
```powershell
winget install Microsoft.DotNet.SDK.8
winget install JRSoftware.InnoSetup
```

---

## 🛡 Security Notes

- The app requires **administrator privileges** at runtime (needed for `wintun.dll` + route table edits)
- UAC prompt will appear on first connect
- No data is sent anywhere except to your chosen Xray server
- Settings & server list stored locally at `%LOCALAPPDATA%\XrayVpn\`
- All logs at `%LOCALAPPDATA%\XrayVpn\logs\`

---

## 📝 License

MIT — see [LICENSE.txt](src/XrayVpnApp.Installer/LICENSE.txt)

This project uses:
- [Xray-core](https://github.com/XTLS/Xray-core) — MIT
- [wintun](https://www.wintun.net) — BSD-style (WireGuard)
- [.NET 8](https://dotnet.microsoft.com) — MIT
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MIT
- [Newtonsoft.Json](https://www.newtonsoft.com/json) — MIT

---

## 🤝 Contributing

Issues & PRs welcome. Please read [docs/BUILD.md](docs/BUILD.md) first.

## ⭐ Acknowledgments

Inspired by:
- [v2rayN](https://github.com/2dust/v2rayN)
- [Nekobox/Nekoray](https://github.com/MatsuriDayo/NekoBox)
- [Hiddify](https://github.com/hiddify/Hiddify-Next)
