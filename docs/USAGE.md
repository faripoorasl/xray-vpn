# Usage Guide / راهنمای استفاده

## First launch / اولین اجرا

1. Double-click `XrayVpn.exe`
2. **UAC prompt** appears — click **Yes** (admin rights needed for TUN adapter)
3. App opens with the **Servers** tab

---

## Adding servers / افزودن سرور

### Method 1: Paste a share-link

1. Copy a config link (`vless://...`, `vmess://...`, `trojan://...`, or `ss://...`)
2. In the app, go to **Servers** tab
3. Paste in the text box at the top
4. Click **+** button or press `Ctrl+Enter`

### Method 2: Import from file

1. Click **Import from File**
2. Select a `.json`, `.txt`, or `.yaml` file containing one or more links
3. Servers are added automatically

### Method 3: Add a subscription

1. Go to **Subscriptions** tab
2. Enter a **Name** (anything you like)
3. Paste the **Subscription URL** (provided by your VPN provider)
4. Check **Auto Update** if you want automatic refresh
5. Click **Add Subscription**
6. Servers are fetched immediately

To update later: click the **↻** button next to a subscription.
To update all: click **Update All**.

---

## Connecting / اتصال

1. Select a server from the list (single click)
2. Click the big **Connect** button at the bottom-right
3. Status turns yellow ("Connecting...")
4. After ~2 seconds, status turns green ("Connected")
5. **All system traffic** is now routed through the VPN

You can verify by:
- Checking the status bar at the bottom
- Visiting https://whatismyip.com — should show the VPN IP
- Opening any app that uses the internet (browser, game, etc.)

---

## Disconnecting / قطع اتصال

Click the same button (now labeled **Disconnect**).

The TUN adapter is removed, routes are restored, DNS is reset.

---

## Speed & latency testing / تست سرعت و پینگ

### Test a single server's latency

Click the 🛰 icon next to the server in the list.

The latency (in ms) appears in the **Ping** column.

Color coding:
- 🟢 Green: < 150 ms (excellent)
- 🟡 Yellow: 150-500 ms (acceptable)
- 🔴 Red: > 500 ms (poor)
- ⚫ Gray: Timeout (failed)

### Test a single server's download speed

Click the ⚡ icon.

The app:
1. Spins up a temporary Xray instance on ports 10810/10811
2. Downloads a 25 MB file from Cloudflare through the proxy
3. Calculates Mbps
4. Stops the temporary instance

Results appear in the **Speed** column.

### Test all servers

Click **Test All** at the top. Tests run sequentially (one at a time) to avoid interference.

### Test active VPN speed

Go to **Speed Test** tab → click **Post-Connect Test**.

This tests the speed while the VPN is active (uses the system's default route).

---

## Settings / تنظیمات

### General / عمومی

| Setting | Description |
|---------|-------------|
| Language | Switch UI between فارسی / English (changes instantly) |
| Start with Windows | Auto-launch on Windows boot |
| Minimize to Tray | Hide window instead of minimizing to taskbar |
| Close to Tray | Hide window when X is clicked (instead of quitting) |
| Auto-connect on Start | Connect to last server automatically |

### TUN Settings / تنظیمات TUN

| Setting | Default | Description |
|---------|---------|-------------|
| Adapter Name | `XrayVpn` | Name shown in `ipconfig` |
| Adapter IP | `10.10.0.2` | IP assigned to the TUN adapter |
| Gateway | `10.10.0.1` | Gateway IP |
| MTU | `1500` | Maximum transmission unit |

⚠ **Don't change these unless you know what you're doing.** If you have a network on `10.10.0.x`, change to a different subnet like `172.16.0.2`.

### DNS

| Setting | Default | Description |
|---------|---------|-------------|
| Remote DNS | `8.8.8.8` | DNS used for proxied domains |
| Local DNS | `223.5.5.5` | DNS used for direct (bypass) domains |
| DoH URL | `https://1.1.1.1/dns-query` | DNS-over-HTTPS endpoint |
| Enable Fake DNS | On | Prevent DNS leaks by returning fake IPs |
| DNS over HTTPS | On | Encrypt DNS queries |

### Routing

| Setting | Default | Description |
|---------|---------|-------------|
| Bypass LAN | On | Don't route LAN traffic (192.168.x, 10.x, 172.16-31.x) through VPN |
| Bypass Iranian Sites | On | Direct connect for `.ir` domains and Iranian IPs |
| Block Ads | Off | Block known ad domains via `geosite:category-ads-all` |

### Xray Core

| Setting | Default | Description |
|---------|---------|-------------|
| Log Level | Info | Xray verbosity (Debug/Info/Warning/Error) |
| SOCKS Port | `10808` | Local SOCKS5 port (for proxy-mode apps) |
| HTTP Port | `10809` | Local HTTP proxy port |
| Enable Mux | Off | Multiplex multiple connections (can improve or hurt speed) |
| Mux Concurrency | `8` | Number of mux streams |

---

## System tray / System Tray

When you minimize or close the app, it hides to the system tray (bottom-right corner).

Right-click the tray icon for:
- **Show** — bring the window back
- **Connect / Disconnect** — quick toggle
- **Exit** — fully quit the app

Double-click the icon to show the window.

---

## Logs / لاگ‌ها

The **Logs** tab shows the current day's log file.

Click **Open Folder** to open the log directory in Explorer.

Logs are at: `%LOCALAPPDATA%\XrayVpn\logs\xrayvpn-YYYY-MM-DD.log`

The log contains:
- App startup/shutdown events
- Config parsing results
- Xray core output (with `[xray]` prefix)
- TUN adapter creation steps
- Route table changes
- Errors with stack traces

---

## Sharing a server / اشتراک‌گذاری سرور

Click the 📋 icon next to a server to copy its share-link to the clipboard.

You can then paste it in another app, send it to a friend, or generate a QR code (planned feature).

---

## Tips & tricks / نکات

### Quick connect via tray
Right-click tray icon → **Connect**. Uses the last-connected server.

### Test before connecting
Always test latency before connecting — saves time on dead servers.

### Use Fake DNS
Keep Fake DNS enabled to prevent DNS leaks. Without it, your ISP can see which domains you're querying.

### Don't enable Mux unless needed
Mux can hurt speed on high-latency servers. Test both with and without.

### Bypass Iran
Keep this ON if you're in Iran. Otherwise Iranian sites (banks, government, etc.) will be routed through the VPN and may be blocked.

### Close completely vs. minimize
If you want the VPN to stay active while you do other things, use **Minimize to Tray**.
If you want to fully quit, right-click tray icon → **Exit**.

---

## Troubleshooting / رفع اشکال

### App won't start
- Run as Administrator
- Check the log file at `%LOCALAPPDATA%\XrayVpn\logs\`
- Make sure `xray.exe` and `wintun.dll` are in the same folder as `XrayVpn.exe`

### Connection fails
1. Check the **Logs** tab for errors
2. Verify the server config is valid (try the link in v2rayN to confirm)
3. Make sure your internet works without VPN
4. Try a different server

### TUN adapter creation fails
- Run as Administrator (must)
- Close other VPN apps (WireGuard, OpenVPN, etc.)
- Check Windows Services: `Windows Management Instrumentation` should be running
- Try a different adapter name in Settings

### Internet doesn't work after connect
- Check the **Logs** tab — look for Xray errors
- Try disabling **Bypass Iran** (sometimes `geosite:category-ir` is outdated)
- Try disabling **Fake DNS**
- Try changing the **Remote DNS** to `1.1.1.1`

### Internet doesn't work after disconnect
- This shouldn't happen, but if it does:
  - Open `cmd` as admin
  - Run `netsh winsock reset`
  - Run `ipconfig /flushdns`
  - Restart your computer

### High CPU usage
- Disable **Mux** in Settings
- Lower **Log Level** to **Warning**
- Try a different server (some VLESS servers have inefficient configs)

### Can't access Iranian sites while connected
- Make sure **Bypass Iran** is ON in Settings
- If still failing, add the specific domain to the routing rules (advanced — see Xray docs)

---

## FAQ / سؤالات متداول

**Q: Does this work on Windows 7/8?**
A: No. Windows 10 22H2+ or Windows 11 only. The wintun.dll driver requires Windows 10+.

**Q: Can I use this on Mac/Linux?**
A: No. This is Windows-only. For Mac/Linux, use V2RayL, Nekoray, or Hiddify.

**Q: Does it work with UWP apps (Microsoft Store)?**
A: Yes! TUN mode captures UWP traffic by default. No `CheckNetIsolation` needed.

**Q: Can multiple instances run at the same time?**
A: No. Only one instance can use the TUN adapter at a time.

**Q: Will my real IP leak?**
A: With Fake DNS + Bypass LAN off, your IP should not leak. You can verify at https://browserleaks.com/ip

**Q: How much RAM does it use?**
A: ~50-100 MB for the WPF UI + ~30-50 MB for xray.exe = ~100-150 MB total.

**Q: Is the source code safe to audit?**
A: Yes — it's 100% open-source on GitHub. No telemetry, no analytics, no phone-home.

**Q: Can I use this commercially?**
A: Yes — MIT licensed. Free for personal and commercial use.

---

## Uninstalling / حذف برنامه

### Portable version
Delete the folder. Done.

### Installer version
Use **Add/Remove Programs** in Windows Settings, or run the uninstaller from the Start Menu.

Both methods will:
- Kill running `XrayVpn.exe` and `xray.exe` processes
- Remove the install directory
- Remove the user data directory at `%LOCALAPPDATA%\XrayVpn`
