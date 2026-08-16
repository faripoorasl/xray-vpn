# 🚀 Quick Start — One-Click Install

## 📥 Download

Download **just these two files** from this repository:

1. [`setup.bat`](setup.bat)
2. [`install.ps1`](install.ps1)

Put them in the **same folder** (e.g., `D:\xray-vpn-install\`).

> Or, download the entire repo as ZIP and extract.

---

## ▶️ Run

**Double-click `setup.bat`** — that's it!

The script will:
1. ✅ Request Administrator privileges (UAC prompt — click Yes)
2. ✅ Install .NET 8 SDK (if missing)
3. ✅ Install Git (if missing)
4. ✅ Offer to install Inno Setup (optional)
5. ✅ Clone the repository
6. ✅ Download Xray-core + wintun.dll + geoip.dat + geosite.dat
7. ✅ Build the app as a single-file EXE
8. ✅ Create Desktop and Start Menu shortcuts
9. ✅ Offer to launch the app

---

## 🎯 After Installation

1. **Desktop shortcut** → double-click "Xray VPN"
2. UAC prompt → click **Yes**
3. App opens — go to **Servers** tab
4. Paste a config link (`vless://...`, `vmess://...`, etc.)
5. Click **+** button
6. Select the server → click **Connect**

---

## 🛠 Manual Alternative (if setup.bat fails)

Open PowerShell **as Administrator** and run:

```powershell
# 1. Clone
git clone https://github.com/faripoorasl/xray-vpn.git
cd xray-vpn

# 2. Run installer
.\setup.bat
# or
powershell -ExecutionPolicy Bypass -File install.ps1
```

---

## ❓ Troubleshooting

### "PowerShell execution policy" error
Run this first:
```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

### "winget not recognized"
Install App Installer from Microsoft Store:
```
ms-windows-store://pdp/?productid=9NBLGGH4NNS1
```

### Build fails
Check the log output. Most common issues:
- Missing dependencies → re-run `scripts\download-deps.ps1`
- .NET SDK not on PATH → restart your computer
- Antivirus blocking `wintun.dll` → add exclusion

### App won't start
- Make sure you run as **Administrator**
- Check `%LOCALAPPDATA%\XrayVpn\logs\` for errors
- Verify `xray.exe` and `wintun.dll` exist next to `XrayVpn.exe`

---

## 📞 Support

- GitHub Issues: [https://github.com/faripoorasl/xray-vpn/issues](https://github.com/faripoorasl/xray-vpn/issues)
- Logs: `%LOCALAPPDATA%\XrayVpn\logs\`
- Full docs: [`docs/USAGE.md`](docs/USAGE.md)
