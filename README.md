# 🖼️ imgsaver

> **A specialized WPF browser for AI artists**
> Automate image capture, prompt management, and workflow optimization for SeaArt and other AI art platforms.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-WPF%20%7C%20Windows-lightgrey)]()
[![WebView2](https://img.shields.io/badge/WebView2-Chromium-green)]()

[🇬🇧 English](#-english) | [🇮🇷 فارسی](#-فارسی)

---

## 🇬🇧 English

### ✨ Overview

**imgsaver** is a purpose-built WPF web browser designed to supercharge the workflow of AI artists — especially users of [SeaArt](https://www.seaart.me). It automatically detects, captures, and organizes generated images along with their metadata, while offering powerful tools for prompt management, clipboard automation, and local file sharing.

### 🚀 Key Features

| Feature | Description |
|---------|-------------|
| 🖼️ **Auto Image Capture** | Instantly detects & caches images from `seaart.me` with full metadata (prompt, seed, model, etc.) |
| 🧠 **Smart Prompt Snippets** | Save reusable prompt templates with shortcuts — type `/portrait` to auto-expand to the full prompt |
| 🌐 **Multi-Tab Chromium Browser** | Built on WebView2 for modern rendering, extension support, and smooth performance |
| 📋 **Clipboard Manager** | Quick access to copied images, prompts, and generation parameters |
| 🖼️ **Integrated Gallery** | Browse, filter, tag, and export your saved AI artworks — all inside the app |
| ⚡ **Performance & Privacy** | Ad/tracker blocking, custom cache, JS/media toggle, HTTP/SOCKS5 proxy support |
| 📡 **Local Web Server** | Share your gallery via `http://localhost:PORT` — no cloud upload required |
| 🔖 **Browser Essentials** | Bookmarks, session restore, history, and download management |

### 🛠️ Getting Started

#### Prerequisites

- Windows 10/11 (64-bit)
- [.NET 6+ Runtime](https://dotnet.microsoft.com/download)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

#### Installation

1. Download the latest release from [Releases](https://github.com/samadbfoxpro/imgsaver/releases)
2. Extract the archive and run `imgsaver.exe`
3. *(Optional)* Add to PATH or create a desktop shortcut

#### Building from Source

```bash
git clone https://github.com/samadbfoxpro/imgsaver.git
cd imgsaver
# Open imgsaver.sln in Visual Studio 2022+
# Restore NuGet packages and build the solution
```

### 📁 Project Structure

```
imgsaver/
├── Core/          # Browser engine, image capture logic
├── UI/            # WPF views, controls, themes
├── Services/      # Prompt manager, clipboard, local server
├── Models/        # Data classes for images & metadata
├── Utils/         # Helpers, extensions, caching
└── Resources/     # Icons, default snippets, localization
```

### ⚙️ Configuration

Edit `config.json` to customize behavior:

```json
{
  "savePath": "C:/AI_Art/SeaArt",
  "autoCapture": true,
  "proxy": { "enabled": false, "type": "HTTP", "address": "" },
  "snippetShortcuts": {
    "/anime": "masterpiece, best quality, anime style, ...",
    "/realistic": "photorealistic, 8k, detailed skin, ..."
  }
}
```

### 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feat/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add: AmazingFeature'`)
4. Push and open a Pull Request

See [CONTRIBUTING.md](CONTRIBUTING.md) for full guidelines.

### 📄 License

Distributed under the MIT License. See `LICENSE` for more information.

### 🙏 Acknowledgements

- [WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- [SeaArt](https://www.seaart.me) community for inspiration
- All contributors & beta testers

---

## 🇮🇷 فارسی

### ✨ معرفی

**imgsaver** یک مرورگر وب تخصصی مبتنی بر WPF است که به‌طور خاص برای بهینه‌سازی گردش کار هنرمندان هوش مصنوعی — به‌ویژه کاربران [SeaArt](https://www.seaart.me) — طراحی شده است. این ابزار به‌طور خودکار تصاویر تولیدشده را همراه با متادیتای کامل (پرامپت، seed، مدل و غیره) شناسایی، ذخیره و سازماندهی می‌کند؛ و در کنار آن امکانات قدرتمندی برای مدیریت پرامپت، اتوماسیون کلیپ‌بورد و اشتراک‌گذاری فایل به صورت محلی ارائه می‌دهد.

### 🚀 ویژگی‌های کلیدی

| ویژگی | توضیحات |
|-------|---------|
| 🖼️ **ذخیره خودکار تصاویر** | شناسایی فوری و کش تصاویر از `seaart.me` به همراه متادیتای کامل |
| 🧠 **اسنیپت‌های هوشمند پرامپت** | ذخیره قالب‌های پرامپت با میانبر — تایپ `/portrait` برای درج خودکار پرامپت کامل |
| 🌐 **مرورگر چندتبانه Chromium** | مبتنی بر WebView2 برای رندرینگ مدرن و عملکرد روان |
| 📋 **مدیریت کلیپ‌بورد** | دسترسی سریع به تصاویر کپی‌شده، پرامپت‌ها و پارامترهای تولید |
| 🖼️ **گالری یکپارچه** | مشاهده، فیلتر، برچسب‌گذاری و خروجی‌گیری از آثار هنری ذخیره‌شده — همه در داخل اپ |
| ⚡ **عملکرد و حریم خصوصی** | مسدودسازی تبلیغات و ردیاب‌ها، کش سفارشی، کنترل JS/رسانه، پشتیبانی از پراکسی HTTP/SOCKS5 |
| 📡 **سرور وب محلی** | اشتراک‌گذاری گالری از طریق `http://localhost:PORT` — بدون نیاز به آپلود ابری |
| 🔖 **امکانات استاندارد مرورگر** | بوکمارک، بازگردانی جلسه، تاریخچه و مدیریت دانلود |

### 🛠️ شروع به کار

#### پیش‌نیازها

- ویندوز ۱۰/۱۱ (۶۴ بیتی)
- [.NET 6+ Runtime](https://dotnet.microsoft.com/download)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

#### نصب

1. آخرین نسخه را از [Releases](https://github.com/samadbfoxpro/imgsaver/releases) دانلود کنید
2. فایل را استخراج کرده و `imgsaver.exe` را اجرا کنید
3. *(اختیاری)* به PATH اضافه کنید یا یک میانبر روی دسکتاپ بسازید

#### کامپایل از سورس

```bash
git clone https://github.com/samadbfoxpro/imgsaver.git
cd imgsaver
# فایل imgsaver.sln را در Visual Studio 2022+ باز کنید
# پکیج‌های NuGet را Restore کرده و Solution را Build کنید
```

### 📁 ساختار پروژه

```
imgsaver/
├── Core/          # موتور مرورگر، منطق ضبط تصاویر
├── UI/            # ویوها، کنترل‌ها و تم‌های WPF
├── Services/      # مدیریت پرامپت، کلیپ‌بورد، سرور محلی
├── Models/        # کلاس‌های داده برای تصاویر و متادیتا
├── Utils/         # ابزارهای کمکی، اکستنشن‌ها، کش
└── Resources/     # آیکن‌ها، اسنیپت‌های پیش‌فرض، بومی‌سازی
```

### ⚙️ پیکربندی

فایل `config.json` را برای سفارشی‌سازی ویرایش کنید:

```json
{
  "savePath": "C:/AI_Art/SeaArt",
  "autoCapture": true,
  "proxy": { "enabled": false, "type": "HTTP", "address": "" },
  "snippetShortcuts": {
    "/anime": "masterpiece, best quality, anime style, ...",
    "/realistic": "photorealistic, 8k, detailed skin, ..."
  }
}
```

### 🤝 مشارکت

از مشارکت شما استقبال می‌شود! لطفاً:

1. ریپازیتوری را Fork کنید
2. یک برنچ ویژگی ایجاد کنید (`git checkout -b feat/AmazingFeature`)
3. تغییرات را کامیت کنید (`git commit -m 'Add: AmazingFeature'`)
4. Push کنید و یک Pull Request باز کنید

برای راهنمایی کامل‌تر، فایل [CONTRIBUTING.md](CONTRIBUTING.md) را مطالعه کنید.

### 📄 مجوز

این پروژه تحت مجوز MIT توزیع شده است. برای اطلاعات بیشتر فایل `LICENSE` را مشاهده کنید.

### 🙏 سپاسگزاری

- [WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- جامعه [SeaArt](https://www.seaart.me) برای الهام‌بخشی
- تمامی مشارکت‌کنندگان و آزمایش‌کنندگان نسخه بتا
