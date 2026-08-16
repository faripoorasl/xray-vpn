# 🛡 Xray VPN — کلاینت VPN نتیو ویندوز 11

> یک کلاینت VPN نتیو ویندوز 11 مبتنی بر Xray-core با TUN adapter مستقیم

## ✨ امکانات

### 🌐 پشتیبانی از چندین پروتکل
- **VLESS** (با REALITY / XTLS-Vision / Flow)
- **VMess** (با AEAD / legacy)
- **Trojan**
- **Shadowsocks** (تمام cipherها)
- **Subscription URL** (آپدیت خودکار)
- **وارد کردن فایل JSON**

### 🔒 حالت TUN مستقیم
- استفاده از `wintun.dll` (درایور WireGuard) به صورت مستقیم — بدون نیاز به wrapper شخص ثالث
- ایجاد یک اداپتور شبکه واقعی که **تمام ترافیک سیستم** را شکار می‌کند
- بدون نیاز به تنظیمات پروکسی سیستم
- کار با اپلیکیشن‌های UWP (Microsoft Store)، بازی‌ها و هر اپ TCP/UDP

### 🛠 پیکربندی و مسیریابی
- عبور از سایت‌های ایرانی (`geosite:category-ir`, `geoip:ir`)
- عبور از ترافیک LAN
- مسدودسازی تبلیغات (`geosite:category-ads-all`)
- قوانین مسیریابی سفارشی
- پشتیبانی از DNS-over-HTTPS
- Fake DNS برای جلوگیری از DNS leak

### 📊 تست سرعت و پینگ
- تست پینگ قبل از اتصال (TCP handshake از طریق SOCKS)
- تست سرعت دانلود قبل از اتصال (فایل 25MB از Cloudflare)
- تست سرعت بعد از اتصال (وقتی VPN فعال است)
- تست گروهی همه سرورها با یک کلیک

### 🎨 رابط کاربری مدرن ویندوز 11
- ساخته‌شده با WPF .NET 8 + تم تیره الهام‌گرفته از Mica
- **دوزبانه** (فارسی / English) — قابل تغییر از تنظیمات
- تغییر خودکار RTL/LTR
- آیکون System Tray با منوی راست‌کلیک
- مینیمایز به ترای، بستن به ترای
- اجرای خودکار با ویندوز (اختیاری)

### 📦 توزیع
- نسخه **Portable**: تک‌فایل `XrayVpn.exe` (~80MB self-contained)
- نسخه **Installer**: ویزارد Inno Setup با UI دوزبانه
- بدون نیاز به نصب .NET runtime (self-contained)
- بدون نیاز به نصب ادمین‌فقط (runtime نیاز به ادمین برای TUN دارد)

---

## 🚀 شروع سریع

### پیش‌نیازها
- ویندوز 11 (x64) — ویندوز 10 هم کار می‌کند
- .NET 8 SDK (فقط برای بیلد)
- ~150MB فضای خالی

### بیلد از سورس

```powershell
# 1. کلون کردن مخزن
git clone https://github.com/yourname/xray-vpn.git
cd xray-vpn

# 2. دانلود پیش‌نیازها (xray-core, wintun.dll, geoip)
.\scripts\download-deps.ps1

# 3. بیلد و انتشار
.\scripts\build.ps1

# 4. (اختیاری) ساخت نصب‌کننده
# به صورت خودکار اگر Inno Setup نصب باشد
```

### اجرا در حالت توسعه

```powershell
.\scripts\run-dev.ps1
```

---

## 📖 نحوه استفاده

### 1. افزودن سرور
- اپ را باز کنید
- به تب **سرورها** بروید
- یک لینک `vless://`، `vmess://`، `trojan://` یا `ss://` را در کادر متنی paste کنید
- روی دکمه **+** کلیک کنید یا `Ctrl+Enter` بزنید

### 2. افزودن سابسکریپشن
- به تب **سابسکریپشن** بروید
- نام و آدرس URL را وارد کنید
- روی **افزودن سابسکریپشن** کلیک کنید — سرورها به صورت خودکار دریافت می‌شوند

### 3. اتصال
- یک سرور از لیست انتخاب کنید
- دکمه بزرگ **اتصال** در پایین را بزنید
- ممکن است پنجره UAC ظاهر شود (TUN به دسترسی ادمین نیاز دارد)
- وضعیت سبز می‌شود وقتی متصل شد

### 4. تست سرورها
- 🛰 برای تست پینگ
- ⚡ برای تست سرعت دانلود
- یا **تست همه** برای تست همه سرورها به ترتیب

### 5. تنظیم DNS و مسیریابی
- به تب **تنظیمات** بروید
- DNS، Fake DNS، DoH، قوانین مسیریابی را تنظیم کنید
- **ذخیره** را بزنید

---

## 🛡 نکات امنیتی

- اپ در زمان اجرا به **دسترسی ادمین** نیاز دارد (برای `wintun.dll` + ویرایش route table)
- پنجره UAC در اولین اتصال ظاهر می‌شود
- هیچ داده‌ای به جایی ارسال نمی‌شود جز سرور Xray انتخابی شما
- تنظیمات و لیست سرورها در `%LOCALAPPDATA%\XrayVpn\` ذخیره می‌شود
- لاگ‌ها در `%LOCALAPPDATA%\XrayVpn\logs\`

---

## 📝 لایسنس

MIT — فایل [LICENSE.txt](src/XrayVpnApp.Installer/LICENSE.txt) را ببینید.

این پروژه از موارد زیر استفاده می‌کند:
- [Xray-core](https://github.com/XTLS/Xray-core) — MIT
- [wintun](https://www.wintun.net) — BSD-style (WireGuard)
- [.NET 8](https://dotnet.microsoft.com) — MIT
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MIT
- [Newtonsoft.Json](https://www.newtonsoft.com/json) — MIT

---

## 🤝 مشارکت

Issues و PR ها مورد استقبال هستند. لطفاً اول [docs/BUILD.md](docs/BUILD.md) را بخوانید.

## ⭐ تشکر از

الهام‌گرفته از:
- [v2rayN](https://github.com/2dust/v2rayN)
- [Nekobox/Nekoray](https://github.com/MatsuriDayo/NekoBox)
- [Hiddify](https://github.com/hiddify/Hiddify-Next)
