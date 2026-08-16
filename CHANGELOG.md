# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- QR code scanner (camera input)
- QR code generator for sharing servers
- Config export to JSON / YAML
- Custom routing rules editor
- Per-app routing (split tunneling)
- IPv6 TUN support
- Auto-update mechanism
- Light theme
- Connection statistics dashboard

## [1.0.0] - 2026-08-16

### Added
- ✨ Initial release of Xray VPN for Windows 11
- 🌐 Multi-protocol support: VLESS, VMess, Trojan, Shadowsocks
- 🔒 REALITY / XTLS-Vision / Flow support for VLESS
- 📡 Subscription URL support with auto-update
- 📁 JSON config file import
- 🛡 Direct TUN adapter creation via wintun.dll (no third-party wrappers)
- 🌍 System-wide traffic routing through TUN adapter
- 🇮🇷 Bypass Iranian sites & LAN traffic
- 🚫 Ad blocking via geosite rules
- ⚡ Latency testing (pre-connect, via SOCKS)
- 📊 Download speed testing (pre & post connect, Cloudflare 25MB)
- 🌐 DNS-over-HTTPS support
- 🎭 Fake DNS to prevent DNS leaks
- 🎨 Modern Windows 11 UI with Mica-inspired dark theme
- 🌐 Bilingual UI (فارسی / English) with RTL/LTR auto-switch
- 📍 System tray icon with context menu
- ⚡ Minimize-to-tray and close-to-tray support
- 🔁 Auto-start with Windows (optional)
- 📦 Portable single-file EXE distribution
- 📦 Inno Setup installer with bilingual wizard
- 📝 File-based logging with rotation
- 🛠 PowerShell build scripts (download-deps, build, run-dev)
- 📚 Comprehensive bilingual documentation (README, BUILD, USAGE)

### Technical
- Built with .NET 8 + WPF
- Uses CommunityToolkit.Mvvm for MVVM pattern
- Uses Newtonsoft.Json for JSON parsing
- P/Invoke bindings for wintun.dll, iphlpapi.dll, dnsapi.dll
- Self-contained single-file publish (~85 MB)
- Admin privileges required at runtime (for TUN + route table)

### Known Limitations
- Windows 10 22H2+ / Windows 11 only (wintun requirement)
- x64 architecture only
- IPv4 only (IPv6 support planned)
- No split tunneling yet (planned)
