# Build Guide / راهنمای بیلد

## Prerequisites / پیش‌نیازها

### Required
| Component | Min Version | Install |
|-----------|-------------|---------|
| Windows 10/11 x64 | — | — |
| .NET 8 SDK | 8.0.100 | `winget install Microsoft.DotNet.SDK.8` |
| PowerShell | 5.0 | (built-in on Win10/11) |

### Optional
| Component | Purpose | Install |
|-----------|---------|---------|
| Inno Setup 6 | Build installer | `winget install JRSoftware.InnoSetup` |
| Visual Studio 2022 | IDE (optional) | https://visualstudio.microsoft.com |
| Git | Clone source | `winget install Git.Git` |

---

## Step-by-step Build / بیلد گام‌به‌گام

### Step 1: Clone & enter directory

```powershell
git clone https://github.com/yourname/xray-vpn.git
cd xray-vpn
```

If you don't have git, download the ZIP from GitHub and extract.

### Step 2: Download dependencies

```powershell
.\scripts\download-deps.ps1
```

This script:
1. Fetches the latest Xray-core release from `https://github.com/XTLS/Xray-core/releases`
2. Extracts `xray.exe`, `geoip.dat`, `geosite.dat`
3. Fetches `wintun.dll` from `https://www.wintun.net/builds/wintun-0.14.1.zip`
4. Copies all files to `src\XrayVpnApp\Resources\`

**Expected output:**
```
=== All dependencies ready! ===
  [OK] xray.exe (15,234,567 bytes)
  [OK] geoip.dat (5,123,456 bytes)
  [OK] geosite.dat (8,234,567 bytes)
  [OK] wintun.dll (156,789 bytes)
```

### Step 3: Build the app

```powershell
.\scripts\build.ps1
```

This script:
1. Verifies .NET SDK
2. Verifies dependencies exist
3. Restores NuGet packages
4. Builds Release configuration
5. Publishes a single-file self-contained EXE
6. (Optional) Builds the Inno Setup installer

**Output location:** `build\Release\publish\XrayVpn.exe`

### Step 4: Build the installer (optional)

If Inno Setup is installed, the build script will automatically compile the installer.

Output: `build\installer\XrayVpn-1.0.0-setup.exe`

To build only the portable version (no installer):
```powershell
.\scripts\build.ps1 -Portable
```

### Step 5: Run

```powershell
# From publish dir
.\build\Release\publish\XrayVpn.exe

# Or run installer:
.\build\installer\XrayVpn-1.0.0-setup.exe
```

---

## Development mode / حالت توسعه

For development with hot reload:

```powershell
.\scripts\run-dev.ps1
```

This runs `dotnet run` directly without publishing.

---

## Debug build / بیلد دیباگ

```powershell
.\scripts\build.ps1 -Debug
```

Output: `build\Debug\publish\XrayVpn.exe`

---

## Manual build (without scripts) / بیلد دستی

```powershell
# Restore
dotnet restore XrayVpn.sln

# Build
dotnet build XrayVpn.sln -c Release -p:Platform=x64

# Publish single-file
dotnet publish src\XrayVpnApp\XrayVpnApp.csproj `
    -c Release `
    -r win-x64 `
    -p:Platform=x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o build\Release\publish

# Copy dependencies
Copy-Item src\XrayVpnApp\Resources\*.exe build\Release\publish\
Copy-Item src\XrayVpnApp\Resources\*.dll build\Release\publish\
Copy-Item src\XrayVpnApp\Resources\*.dat build\Release\publish\
```

---

## Troubleshooting / رفع اشکال

### "xray.exe not found at runtime"
Run `download-deps.ps1` again, or manually download from https://github.com/XTLS/Xray-core/releases and place `xray.exe`, `geoip.dat`, `geosite.dat` into `src\XrayVpnApp\Resources\`.

### "wintun.dll not found"
Download from https://www.wintun.net/builds/wintun-0.14.1.zip, extract `wintun\bin\amd64\wintun.dll` to `src\XrayVpnApp\Resources\wintun.dll`.

### "dotnet command not found"
Install .NET 8 SDK: `winget install Microsoft.DotNet.SDK.8`

### "iscc.exe not found"
Install Inno Setup: `winget install JRSoftware.InnoSetup`
Or build portable only: `.\scripts\build.ps1 -Portable`

### Build fails with "platform x64 not found"
The project is x64-only. Make sure you don't have `Any CPU` selected. Use `-p:Platform=x64` on all `dotnet` commands.

### App fails to start with "requireAdministrator"
Right-click `XrayVpn.exe` → Properties → Compatibility → check "Run this program as an administrator". Or run from an elevated PowerShell.

### TUN adapter creation fails
- Make sure you're running as Administrator
- Make sure no other VPN client is using `wintun.dll` simultaneously
- Check `Logs` tab for the specific error code

---

## File size breakdown / حجم فایل خروجی

| File | Size (approx) |
|------|---------------|
| XrayVpn.exe (self-contained) | ~85 MB |
| xray.exe | ~15 MB |
| wintun.dll | ~150 KB |
| geoip.dat | ~5 MB |
| geosite.dat | ~8 MB |
| Total | ~115 MB |

The single-file EXE includes the .NET 8 runtime, so no separate install is needed on the target machine.

---

## Build configurations / تنظیمات بیلد

The `.csproj` file uses these settings for the single-file publish:

```xml
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<PublishReadyToRun>true</PublishReadyToRun>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

- `SelfContained`: bundles .NET runtime (~70 MB)
- `PublishSingleFile`: produces one .exe instead of many DLLs
- `PublishReadyToRun`: AOT-compiles some IL → faster startup, slightly larger
- `EnableCompressionInSingleFile`: LZMA compresses the bundle

If you want a smaller build (but require .NET 8 on the target machine):
```xml
<SelfContained>false</SelfContained>
```
This reduces the EXE to ~5 MB.
