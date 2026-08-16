# Contributing to Xray VPN

First off, **thank you** for taking the time to contribute! 🎉

This document outlines how to contribute to the Xray VPN project.

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Pull Request Process](#pull-request-process)
- [Reporting Bugs](#reporting-bugs)
- [Feature Requests](#feature-requests)
- [Translation](#translation)

---

## Code of Conduct

Be respectful, constructive, and inclusive. Harassment of any kind will not be tolerated.

---

## Getting Started

### Prerequisites

- Windows 10 22H2+ or Windows 11
- .NET 8 SDK
- Visual Studio 2022 or VS Code with C# Dev Kit
- Git
- (Optional) Inno Setup 6 for installer builds

### Setup

```powershell
# 1. Fork the repo on GitHub, then clone your fork
git clone https://github.com/YOUR_USERNAME/xray-vpn.git
cd xray-vpn

# 2. Add upstream remote
git remote add upstream https://github.com/ORIGINAL_OWNER/xray-vpn.git

# 3. Download dependencies
.\scripts\download-deps.ps1

# 4. Run in dev mode
.\scripts\run-dev.ps1
```

---

## Development Workflow

1. **Create a branch** for your feature/fix:
   ```powershell
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/issue-123
   ```

2. **Make changes** following our coding standards (see below).

3. **Test your changes**:
   ```powershell
   .\scripts\build.ps1 -Debug
   .\scripts\run-dev.ps1
   ```

4. **Commit** using conventional commits:
   ```powershell
   git commit -m "feat: add QR code scanner"
   git commit -m "fix: TUN adapter creation on Windows 10"
   git commit -m "docs: update USAGE.md"
   ```

5. **Push** to your fork:
   ```powershell
   git push origin feature/your-feature-name
   ```

6. **Open a Pull Request** on GitHub.

---

## Coding Standards

### C# Code Style

- Follow [Microsoft's C# Coding Conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use **file-scoped namespaces** (e.g., `namespace Foo;`)
- Use **4 spaces** for indentation (not tabs)
- Use **PascalCase** for classes, methods, properties
- Use **camelCase** for local variables, parameters
- Use **_camelCase** for private fields
- Enable **nullable reference types** (`<Nullable>enable</Nullable>`)
- Use **`var`** when the type is obvious
- Use **explicit types** when the type is not clear from the right-hand side

### XAML Style

- Use **2 spaces** for indentation
- Place each attribute on its own line for elements with 3+ attributes
- Use **PascalCase** for element/attribute names (XAML default)
- Use **DynamicResource** for localizable strings
- Use **StaticResource** for theme brushes/colors

### Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Class | PascalCase | `TunService` |
| Interface | IPascalCase | `ILogger` |
| Method | PascalCase | `StartAdapter` |
| Property | PascalCase | `IsConnected` |
| Field | _camelCase | `_logger` |
| Local var | camelCase | `processId` |
| Constant | PascalCase | `MaxRetryCount` |
| Private method | PascalCase | `ParseQueryString` |

### Comments

- Use **XML doc comments** (`///`) for public APIs
- Use **regular comments** (`//`) for non-obvious logic
- Avoid obvious comments like `// Get the user`
- Comment **why**, not **what**

---

## Pull Request Process

1. **Update the CHANGELOG.md** under the `[Unreleased]` section.
2. **Update documentation** if needed (README.md, BUILD.md, USAGE.md).
3. **Test on Windows 11** — make sure the app builds and runs.
4. **Verify your PR**:
   - Code compiles without warnings
   - No `Console.WriteLine` debug output left
   - No commented-out code blocks
   - No hardcoded credentials/paths
5. **Write a clear PR description**:
   - What does this PR do?
   - Why is it needed?
   - How was it tested?
   - Screenshots (if UI changes)
6. **Link related issues**: Use `Closes #123` or `Fixes #123`.

### PR Title Format

Use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat: add QR scanner`
- `fix: TUN adapter creation fails on first run`
- `docs: add Persian translation`
- `refactor: simplify ConfigParserService`
- `chore: bump .NET to 8.0.4`
- `style: format XAML files`

---

## Reporting Bugs

Before creating a bug report:

1. **Search existing issues** to avoid duplicates.
2. **Update to the latest version** — the bug may already be fixed.
3. **Try a clean install** — delete `%LOCALAPPDATA%\XrayVpn` and re-run.

When filing a bug report, include:

- **OS version** (Windows 11 23H2, Windows 10 22H2, etc.)
- **App version** (from About tab)
- **Xray-core version** (from About tab)
- **Steps to reproduce** the behavior
- **Expected behavior**
- **Actual behavior**
- **Logs** (from Logs tab or `%LOCALAPPDATA%\XrayVpn\logs\`)
- **Screenshots** (if applicable)
- **Server config** (redact sensitive info — UUID, password)

---

## Feature Requests

We welcome feature requests! Please:

1. Check the [Unreleased section of CHANGELOG.md](CHANGELOG.md#unreleased) — it may already be planned.
2. Search existing issues for similar requests.
3. Open a new issue with:
   - Clear use case
   - Proposed solution
   - Alternatives considered

---

## Translation

We'd love to add more languages! To add a new language:

1. Copy `src/XrayVpnApp/Resources/Strings.en.xaml` to `Strings.<code>.xaml` (e.g., `Strings.ar.xaml`).
2. Translate all `<sys:String>` values.
3. Add the language to `LanguageService.cs`:
   ```csharp
   public const string Ar = "ar";
   ```
4. Add a `ComboBoxItem` in `SettingsPage.xaml`:
   ```xml
   <ComboBoxItem Content="العربية" Tag="ar"/>
   ```
5. Update `App.xaml.cs` to include the new satellite resource:
   ```xml
   <SatelliteResourceLanguages>en;fa;ar</SatelliteResourceLanguages>
   ```
6. Submit a PR.

---

## Architecture Quick Reference

```
UI Layer (WPF)
└─ Views (XAML) ──── ViewModels (MVVM)
                          │
                          ▼
Service Layer ─── ConfigParser ──▶ XrayConfigGenerator ──▶ XrayCoreService
                │                                            │
                ├─ TunService ─── wintun.dll (P/Invoke)     │
                ├─ RoutingService ── iphlpapi.dll           │
                ├─ DnsService ─── netsh                     │
                ├─ SpeedTestService ─── HttpClient          │
                └─ SubscriptionService ─── HttpClient       │
                                                            ▼
                                                    xray.exe (subprocess)
```

---

## Areas Needing Help

- 🌍 More language translations
- 🎨 Light theme support
- 📱 QR code scanner (camera)
- 🧪 Unit tests (currently none)
- 📊 Real-time traffic charts
- 🖥️ Per-app routing (split tunneling)
- 🔄 Auto-update mechanism

---

## Questions?

Feel free to open an issue with the `question` label.

Happy coding! 🚀
