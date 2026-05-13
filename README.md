بفرما! این هم نسخه کامل و آماده کپی برای فایل `README.md`. کل کد زیر را انتخاب کن و مستقیم توی گیت‌هاب پیست کن:

```markdown
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
| 🖼️ **Auto Image Capture** | Instantly detects & caches images from `seaart.me` with metadata (prompt, seed, model, etc.) |
| 🧠 **Smart Prompt Snippets** | Save reusable prompt templates with shortcuts — type `/portrait` → auto-expand to full prompt |
| 🌐 **Multi-Tab Chromium Browser** | Built on WebView2 for modern rendering, extensions support, and smooth performance |
| 📋 **Clipboard Manager** | Quick access to copied images, prompts, and generation parameters |
| 🖼️ **Integrated Gallery** | Browse, filter, tag, and export your saved AI artworks — all inside the app |
| ⚡ **Performance & Privacy** | Ad/tracker blocking, custom cache, JS/media toggle, HTTP/SOCKS5 proxy support |
| 📡 **Local Web Server** | Share your gallery via `http://localhost:PORT` — no cloud upload needed |
| 🔖 **Browser Essentials** | Bookmarks, session restore, history, and download management |

### 🛠️ Getting Started

#### Prerequisites
- Windows 10/11 (64-bit)
- [.NET 6+ Runtime](https://dotnet.microsoft.com/download)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

#### Installation
1. Download the latest release from [Releases](https://github.com/YOUR_USERNAME/imgsaver/releases)
2. Extract and run `imgsaver.exe`
3. (Optional) Add to PATH or create a desktop shortcut

#### Building from Source
```bash
git clone https://github.com/YOUR_USERNAME/imgsaver.git
cd imgsaver
# Open imgsaver.sln in Visual Studio 2022+
# Restore NuGet packages & build solution
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
Edit `config.json` to customize:
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
1. Fork the repo
2. Create a feature branch (`git checkout -b feat/AmazingFeature`)
3. Commit changes (`git commit -m 'Add: AmazingFeature'`)
4. Push & open a Pull Request

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### 📄 License
Distributed under the MIT License. See `LICENSE` for more information.

### 🙏 Acknowledgements
- [WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- [SeaArt](https://www.seaart.me) community for inspiration
- All contributors & beta testers

---

## 🇮🇷 فارسی

### ✨ معرفی
**imgsaver** یک مرورگر وب تخصصی مبتنی بر WPF است که برای بهینه‌سازی گردش کار هنرمندان هوش مصنوعی — به‌ویژه کاربران [SeaArt](https://www.seaart.me) — طراحی شده است. این برنامه به‌طور خودکار تصاویر تولیدشده را همراه با متادیتای کامل (پرامپت، seed، مدل و...) شناسایی
بفرما! این هم نسخه کامل و آماده کپی برای فایل `README.md`. کل کد زیر را انتخاب کن و مستقیم توی گیت‌هاب پیست کن:

```markdown
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
| 🖼️ **Auto Image Capture** | Instantly detects & caches images from `seaart.me` with metadata (prompt, seed, model, etc.) |
| 🧠 **Smart Prompt Snippets** | Save reusable prompt templates with shortcuts — type `/portrait` → auto-expand to full prompt |
| 🌐 **Multi-Tab Chromium Browser** | Built on WebView2 for modern rendering, extensions support, and smooth performance |
| 📋 **Clipboard Manager** | Quick access to copied images, prompts, and generation parameters |
| 🖼️ **Integrated Gallery** | Browse, filter, tag, and export your saved AI artworks — all inside the app |
| ⚡ **Performance & Privacy** | Ad/tracker blocking, custom cache, JS/media toggle, HTTP/SOCKS5 proxy support |
| 📡 **Local Web Server** | Share your gallery via `http://localhost:PORT` — no cloud upload needed |
| 🔖 **Browser Essentials** | Bookmarks, session restore, history, and download management |

### 🛠️ Getting Started

#### Prerequisites
- Windows 10/11 (64-bit)
- [.NET 6+ Runtime](https://dotnet.microsoft.com/download)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

#### Installation
1. Download the latest release from [Releases](https://github.com/YOUR_USERNAME/imgsaver/releases)
2. Extract and run `imgsaver.exe`
3. (Optional) Add to PATH or create a desktop shortcut

#### Building from Source
```bash
git clone https://github.com/YOUR_USERNAME/imgsaver.git
cd imgsaver
# Open imgsaver.sln in Visual Studio 2022+
# Restore NuGet packages & build solution
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
Edit `config.json` to customize:
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
1. Fork the repo
2. Create a feature branch (`git checkout -b feat/AmazingFeature`)
3. Commit changes (`git commit -m 'Add: AmazingFeature'`)
4. Push & open a Pull Request

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### 📄 License
Distributed under the MIT License. See `LICENSE` for more information.

### 🙏 Acknowledgements
- [WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- [SeaArt](https://www.seaart.me) community for inspiration
- All contributors & beta testers

---

## 🇮🇷 فارسی

### ✨ معرفی
**imgsaver** یک مرورگر وب تخصصی مبتنی بر WPF است که برای بهینه‌سازی گردش کار هنرمندان هوش مصنوعی — به‌ویژه کاربران [SeaArt](https://www.seaart.me) — طراحی شده است. این برنامه به‌طور خودکار تصاویر تولیدشده را همراه با متادیتای کامل (پرامپت، seed، مدل و...) شناسایی، ذخیره و سازماندهی می‌کند.

### 🚀 ویژگی‌های کلیدی

| ویژگی | توضیحات |
|--------|---------|
| 🖼️ **ذخیره خودکار تصاویر** | شناسایی و کش فوری تصاویر از `seaart.me` همراه با متادیتا |
| 🧠 **اسنیپت‌های هوشمند پرامپت** | ذخیره قالب‌های پرامپت با میانبر — تایپ `/portrait` → درج خودکار پرامپت کامل |
| 🌐 **مرورگر چندتبانه Chromium** | مبتنی بر WebView2 برای رندرینگ مدرن و عملکرد روان |
| 📋 **مدیریت کلیپ‌بورد** | دسترسی سریع به تصاویر، پرامپت‌ها و پارامترهای کپی‌شده |
| 🖼️ **گالری داخلی** | مشاهده، فیلتر، برچسب‌گذاری و خروجی‌گیری از آثار هنری ذخیره‌شده |
| ⚡ **عملکرد و حریم خصوصی** | مسدودسازی تبلیغات، کش سفارشی، کنترل JS/رسانه، پشتیبانی از پراکسی |
| 📡 **سرور وب محلی** | اشتراک‌گذاری گالری از طریق `http://localhost:PORT` — بدون نیاز به آپلود ابری |
| 🔖 **امکانات استاندارد مرورگر** | بوکمارک، بازگردانی جلسه، تاریخچه و مدیریت دانلود |

### 🛠️ شروع به کار

#### پیش‌نیازها
- ویندوز ۱۰/۱۱ (۶۴ بیتی)
- [.NET 6+ Runtime](https://dotnet.microsoft.com/download)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

#### نصب
1. دانلود آخرین نسخه از [Releases](https://github.com/YOUR_USERNAME/imgsaver/releases)
2. استخراج و اجرای `imgsaver.exe`
3. (اختیاری) افزودن به PATH یا ایجاد میانبر دسکتاپ

#### کامپایل از سورس
```bash
git clone https://github.com/YOUR_USERNAME/imgsaver.git
cd imgsaver
# فایل imgsaver.sln را در Visual Studio 2022+ باز کنید
# پکیج‌های NuGet را Restore و_solution را Build کنید
```

### 📁 ساختار پروژه
```
imgsaver/
├── Core/          # موتور مرورگر، منطق ضبط تصاویر
├── UI/            # ویوها، کنترل‌ها و تم‌های WPF
├── Services/      # مدیریت پرامپت، کلیپ‌بورد، سرور محلی
├── Models/        # کلاس‌های داده برای تصاویر و متادیتا
├── Utils/         # ابزارهای کمکی، اکستنشن‌ها، کش
└── Resources/     # آیکن‌ها، اسنیپت‌های پیش‌فرض، локаلیزیشن
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
4. پوش کنید و Pull Request باز کنید

برای راهنمایی بیشتر، [CONTRIBUTING.md](CONTRIBUTING.md) را مطالعه کنید.

### 📄 مجوز
تحت مجوز MIT توزیع شده است. برای اطلاعات بیشتر فایل `LICENSE` را مشاهده کنید.

### 🙏 سپاسگزاری
- [WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- جامعه [SeaArt](https://www.seaart.me) برای الهام‌بخشی
- همه مشارکت‌کنندگان و تست‌کنندگان بتا
```

---

### ⚠️ قبل از انتشار یادت نره:
1. جای `YOUR_USERNAME` اسم کاربری گیت‌هاب خودت رو بنویس (دو جا هست).
2. اگر فایل‌های `LICENSE` یا `CONTRIBUTING.md` رو نداری، لینک‌هاشون رو حذف کن یا فایل‌ها رو بساز.
3. اگر خواستی اسکرین‌شات اضافه کنی، یه فولدر `screenshots` بساز و عکس‌ها رو اونجا بذار.

موفق باشی! 🚀
