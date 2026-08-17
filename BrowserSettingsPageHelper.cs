using System;

namespace imgsaver
{
    public static class BrowserSettingsPageHelper
    {
        public const string SettingsUrl = "imgsaver://settings";
        public const string AltSettingsUrl = "about:settings";

        public static bool IsSettingsUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.Equals(SettingsUrl, StringComparison.OrdinalIgnoreCase) ||
                   url.Equals(AltSettingsUrl, StringComparison.OrdinalIgnoreCase) ||
                   url.Equals("settings", StringComparison.OrdinalIgnoreCase) ||
                   url.Equals("webino://settings", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetSettingsHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""fa"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>تنظیمات مرورگر</title>
    <style>
        :root {
            color-scheme: dark;
            --bg-primary: #18191C;
            --bg-secondary: #1E1F22;
            --bg-card: #2B2D30;
            --bg-hover: #35373C;
            --accent: #3B82F6;
            --accent-hover: #60A5FA;
            --text-primary: #F2F3F5;
            --text-secondary: #949BA4;
            --text-muted: #6D737B;
            --border: #36393E;
            --danger: #EF4444;
            --danger-bg: #2F1C1C;
            --success: #10B981;
        }

        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Tahoma, sans-serif;
            user-select: none;
        }

        /* Custom Dark Scrollbar */
        ::-webkit-scrollbar {
            width: 8px;
            height: 8px;
        }

        ::-webkit-scrollbar-track {
            background: var(--bg-primary);
        }

        ::-webkit-scrollbar-thumb {
            background: #2b2d30;
            border-radius: 4px;
            border: 2px solid var(--bg-primary);
        }

        ::-webkit-scrollbar-thumb:hover {
            background: #474a50;
        }

        * {
            scrollbar-width: thin;
            scrollbar-color: #2b2d30 var(--bg-primary);
        }

        body {
            background-color: var(--bg-primary);
            color: var(--text-primary);
            display: flex;
            height: 100vh;
            overflow: hidden;
        }

        /* Sidebar */
        .sidebar {
            width: 260px;
            background-color: var(--bg-secondary);
            border-left: 1px solid var(--border);
            display: flex;
            flex-direction: column;
            padding: 24px 16px;
            flex-shrink: 0;
        }

        .brand {
            display: flex;
            align-items: center;
            gap: 12px;
            margin-bottom: 28px;
            padding: 0 8px;
        }

        .brand-logo {
            width: 32px;
            height: 32px;
            background: var(--accent);
            border-radius: 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            font-size: 18px;
            color: white;
        }

        .brand-title {
            font-size: 17px;
            font-weight: 700;
        }

        .nav-item {
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 10px 14px;
            border-radius: 8px;
            color: var(--text-secondary);
            cursor: pointer;
            transition: all 0.2s;
            margin-bottom: 4px;
            font-size: 14px;
        }

        .nav-item:hover {
            background-color: var(--bg-hover);
            color: var(--text-primary);
        }

        .nav-item.active {
            background-color: var(--bg-card);
            color: var(--accent);
            font-weight: 600;
        }

        .nav-icon {
            width: 18px;
            height: 18px;
            fill: currentColor;
            flex-shrink: 0;
        }

        /* Main Content */
        .content {
            flex: 1;
            padding: 32px 48px;
            overflow-y: auto;
            max-width: 880px;
        }

        .page-header {
            margin-bottom: 28px;
        }

        .page-header h1 {
            font-size: 24px;
            font-weight: 700;
            margin-bottom: 6px;
        }

        .page-header p {
            color: var(--text-secondary);
            font-size: 14px;
        }

        /* Settings Card */
        .section-title {
            font-size: 16px;
            font-weight: 600;
            margin: 28px 0 12px 0;
            color: var(--text-primary);
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .card {
            background-color: var(--bg-card);
            border-radius: 12px;
            border: 1px solid var(--border);
            overflow: hidden;
            margin-bottom: 20px;
        }

        .row {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 16px 20px;
            border-bottom: 1px solid var(--border);
            transition: background-color 0.15s;
        }

        .row:last-child {
            border-bottom: none;
        }

        .row:hover {
            background-color: rgba(255, 255, 255, 0.02);
        }

        .row-info h3 {
            font-size: 14px;
            font-weight: 600;
            margin-bottom: 4px;
        }

        .row-info p {
            font-size: 12px;
            color: var(--text-secondary);
        }

        /* Form Controls */
        select, input[type=""text""] {
            background-color: var(--bg-primary);
            border: 1px solid var(--border);
            color: var(--text-primary);
            padding: 8px 12px;
            border-radius: 6px;
            font-size: 13px;
            outline: none;
            transition: border-color 0.2s;
            min-width: 180px;
        }

        select:focus, input[type=""text""]:focus {
            border-color: var(--accent);
        }

        /* Toggle Switch */
        .switch {
            position: relative;
            display: inline-block;
            width: 44px;
            height: 24px;
            flex-shrink: 0;
        }

        .switch input {
            opacity: 0;
            width: 0;
            height: 0;
        }

        .slider {
            position: absolute;
            cursor: pointer;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background-color: var(--bg-hover);
            transition: .3s;
            border-radius: 24px;
        }

        .slider:before {
            position: absolute;
            content: """";
            height: 18px;
            width: 18px;
            left: 3px;
            bottom: 3px;
            background-color: white;
            transition: .3s;
            border-radius: 50%;
        }

        input:checked + .slider {
            background-color: var(--accent);
        }

        input:checked + .slider:before {
            transform: translateX(20px);
        }

        /* Buttons */
        .btn {
            padding: 8px 16px;
            border-radius: 6px;
            font-size: 13px;
            font-weight: 600;
            cursor: pointer;
            border: none;
            transition: all 0.2s;
        }

        .btn-primary {
            background-color: var(--accent);
            color: white;
        }

        .btn-primary:hover {
            background-color: var(--accent-hover);
        }

        .btn-danger {
            background-color: var(--danger-bg);
            color: #f87171;
            border: 1px solid #7f1d1d;
        }

        .btn-danger:hover {
            background-color: #451a1a;
            color: #fecaca;
        }

        /* Toast notification */
        .toast {
            position: fixed;
            bottom: 24px;
            left: 50%;
            transform: translateX(-50%) translateY(100px);
            background-color: var(--accent);
            color: white;
            padding: 10px 20px;
            border-radius: 8px;
            font-size: 13px;
            font-weight: 500;
            box-shadow: 0 4px 14px rgba(0,0,0,0.4);
            opacity: 0;
            transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
            z-index: 999;
        }

        .toast.show {
            transform: translateX(-50%) translateY(0);
            opacity: 1;
        }
    </style>
</head>
<body>

    <!-- Sidebar -->
    <div class=""sidebar"">
        <div class=""brand"">
            <div class=""brand-logo"">⚙️</div>
            <div class=""brand-title"">تنظیمات مرورگر</div>
        </div>

        <div class=""nav-item active"" onclick=""scrollToSection('general-section', this)"">
            <svg class=""nav-icon"" viewBox=""0 0 24 24""><path d=""M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z""/></svg>
            <span>عمومی و محتوا</span>
        </div>

        <div class=""nav-item"" onclick=""scrollToSection('tools-section', this)"">
            <svg class=""nav-icon"" viewBox=""0 0 24 24""><path d=""M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M19,19H5V5H19V19M17,17H7V15H17V17M17,13H7V11H17V13M17,9H7V7H17V9Z""/></svg>
            <span>ابزارها و مینی کلیپ</span>
        </div>

        <div class=""nav-item"" onclick=""scrollToSection('proxy-section', this)"">
            <svg class=""nav-icon"" viewBox=""0 0 24 24""><path d=""M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M17.9,17.39C17.64,16.59 16.7,16 15.5,16H15V13A1,1 0 0,0 14,12H8V10H10A1,1 0 0,0 11,9V7H13A2,2 0 0,0 15,5V4.59C17.93,5.77 20,8.64 20,12C20,14.08 19.2,15.97 17.9,17.39Z""/></svg>
            <span>شبکه و پراکسی</span>
        </div>

        <div class=""nav-item"" onclick=""scrollToSection('privacy-section', this)"">
            <svg class=""nav-icon"" viewBox=""0 0 24 24""><path d=""M12,17A2,2 0 0,0 14,15C14,13.89 13.1,13 12,13A2,2 0 0,0 10,15A2,2 0 0,0 12,17M18,8A2,2 0 0,1 20,10V20A2,2 0 0,1 18,22H6A2,2 0 0,1 4,20V10C4,8.89 4.89,8 6,8H7V6A5,5 0 0,1 12,1A5,5 0 0,1 17,6V8H18M12,3A3,3 0 0,0 9,6V8H15V6A3,3 0 0,0 12,3Z""/></svg>
            <span>حافظه کش و حریم خصوصی</span>
        </div>

        <div class=""nav-item"" onclick=""scrollToSection('about-section', this)"">
            <svg class=""nav-icon"" viewBox=""0 0 24 24""><path d=""M11,9H13V7H11M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M11,17H13V11H11V17Z""/></svg>
            <span>درباره مرورگر</span>
        </div>
    </div>

    <!-- Content -->
    <div class=""content"" id=""contentArea"">
        <div class=""page-header"">
            <h1>تنظیمات مرورگر</h1>
            <p>مدیریت و پیکربندی رفتار محتوا، ابزارهای کمکی، پراکسی و حافظه کش</p>
        </div>

        <!-- General Section -->
        <div class=""section-title"" id=""general-section"">🌐 عمومی و بارگیری محتوا</div>
        <div class=""card"">
            <div class=""row"">
                <div class=""row-info"">
                    <h3>بارگیری تصاویر (Load Images)</h3>
                    <p>نمایش خودکار تصاویر در صفحات وب</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkLoadImages"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>بارگیری ویدیو و صوت (Media)</h3>
                    <p>اجرای خودکار فایل‌های چندرسانه‌ای و استریم‌ها</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkLoadMedia"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>فعال‌سازی جاوا اسکریپت (JavaScript)</h3>
                    <p>اجرای کدهای پویای جاوا اسکریپت در صفحات</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkEnableJS"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>بی‌صدا کردن صدای مرورگر (Mute Audio)</h3>
                    <p>قطع کردن تمام صداهای پخش‌شده در تب‌های مرورگر</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkMuteAudio"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
        </div>

        <!-- Tools & MiniClip Section -->
        <div class=""section-title"" id=""tools-section"">⚡ ابزارها، ترکیب هوشمند و مینی کلیپ</div>
        <div class=""card"">
            <div class=""row"">
                <div class=""row-info"">
                    <h3>نوار ترکیب هوشمند پرامپت (Combiner Bar)</h3>
                    <p>نمایش نوار ابزار هوشمند ترکیب پرامپت در پایین مرورگر</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkEnableCombinerBar"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>ایمپورت خودکار تصاویر به مینی کلیپ</h3>
                    <p>ذخیره و ارسال مستقیم عکس‌های دانلود شده به پنجره مینی کلیپ</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkAutoImportImages"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>دکمه ارسال به مینی کلیپ روی تصاویر بزرگ</h3>
                    <p>نمایش دکمه شناور روی تصاویر صفحات برای ایمپورت با یک کلیک</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkShowImportButtons"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>دکمه چسباندن سریع (Quick Paste) روی کادرها</h3>
                    <p>نمایش دکمه کمکی پیست پرامپت‌ها داخل فیلدهای ورودی صفحات</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkShowQuickPaste"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>پیست و کلیک خودکار (Auto Quick Paste & Action)</h3>
                    <p>جایگزینی خودکار متن جدید کلیپ‌بورد در موقعیت تعیین‌شده و کلیک روی دکمه مقصد</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkEnableAutoQuickPaste"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>نمایش پین‌های شناور تنظیم موقعیت (Auto Pins)</h3>
                    <p>نمایش نشانگرهای شناور برای جابجایی و تعیین نقطه فیلد متن (۱) و دکمه اقدام (۲)</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkShowAutoPastePins"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>میزان شفافیت پین‌های شناور (Opacity)</h3>
                    <p>تنظیم شفافیت پین‌های ۱ و ۲ از ۰٪ (نامرئی کامل) تا ۱۰۰٪ (کاملاً واضح)</p>
                </div>
                <div style=""display:flex; align-items:center; gap:10px;"">
                    <input type=""range"" id=""rngPinsOpacity"" min=""0"" max=""100"" value=""100"" style=""width:120px; accent-color:var(--accent); cursor:pointer;"" oninput=""document.getElementById('lblPinsOpacityVal').innerText = this.value + '%'; saveAllSettings();"">
                    <span id=""lblPinsOpacityVal"" style=""font-weight:bold; min-width:40px; text-align:left; font-size:13px; color:var(--text-secondary);"">100%</span>
                </div>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>پلیر شناور ضبط و بازپخش مرورگر</h3>
                    <p>نمایش کنترل‌های شناور هنگام ضبط یا پخش سناریوهای مرورگر</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkShowRecordPlayer"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>مخفی‌سازی خودکار نوار وضعیت پایین</h3>
                    <p>پنهان شدن نوار وضعیت پس از بارگیری کامل صفحه</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkAutoHideStatus"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
        </div>

        <!-- Proxy Section -->
        <div class=""section-title"" id=""proxy-section"">🛡️ شبکه و پراکسی</div>
        <div class=""card"">
            <div class=""row"">
                <div class=""row-info"">
                    <h3>حالت اتصال پراکسی (Proxy Mode)</h3>
                    <p>نوع استفاده از پراکسی برای اتصال وب</p>
                </div>
                <select id=""selProxyMode"" onchange=""saveAllSettings()"">
                    <option value=""system"">استفاده از تنظیمات سیستم (System)</option>
                    <option value=""custom"">پراکسی سفارشی دستی (Custom)</option>
                    <option value=""off"">بدون پراکسی و مستقیم (Direct / Off)</option>
                </select>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>نوع پراکسی</h3>
                    <p>پروتکل مورد استفاده برای پراکسی سفارشی</p>
                </div>
                <select id=""selProxyType"" onchange=""saveAllSettings()"">
                    <option value=""http"">HTTP / HTTPS</option>
                    <option value=""socks5"">SOCKS5</option>
                </select>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>آدرس سرور پراکسی (IP / Host)</h3>
                    <p>آدرس IP یا دامنه سرور پراکسی</p>
                </div>
                <input type=""text"" id=""txtProxyAddress"" onblur=""saveAllSettings()"" placeholder=""127.0.0.1"">
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>پورت سرور پراکسی (Port)</h3>
                    <p>شماره پورت اتصال</p>
                </div>
                <input type=""text"" id=""txtProxyPort"" onblur=""saveAllSettings()"" placeholder=""8080"" style=""min-width:100px; max-width:120px;"">
            </div>
        </div>

        <!-- Privacy & Cache Section -->
        <div class=""section-title"" id=""privacy-section"">🔒 حافظه کش و پاک‌سازی داده‌ها</div>
        <div class=""card"">
            <div class=""row"">
                <div class=""row-info"">
                    <h3>پاک‌سازی حافظه موقت و کش مرورگر</h3>
                    <p>حذف فایل‌های موقت، تصاویر کش‌شده و حافظه موقت Chromium</p>
                </div>
                <button class=""btn btn-danger"" onclick=""clearCache(false)"">پاک‌سازی کش</button>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>پاک‌سازی کامل (کوکی‌ها، لاگین و نشست‌ها)</h3>
                    <p>خروج از تمام حساب‌های کاربری و پاک کردن اطلاعات ورود و کوکی‌ها</p>
                </div>
                <button class=""btn btn-danger"" onclick=""clearCache(true)"">پاک‌سازی کامل داده‌ها</button>
            </div>
        </div>

        <!-- About Section -->
        <div class=""section-title"" id=""about-section"">ℹ️ درباره مرورگر</div>
        <div class=""card"">
            <div class=""row"">
                <div class=""row-info"">
                    <h3>مرورگر حرفه‌ای دیتاست و هوش مصنوعی</h3>
                    <p>طراحی مدرن الهام‌گرفته از Webino | موتور پرسرعت Microsoft WebView2</p>
                </div>
                <button class=""btn btn-primary"" onclick=""showToast('تنظیمات و مرورگر شما آماده است.')"">وضعیت سیستم</button>
            </div>
        </div>
    </div>

    <div class=""toast"" id=""toast"">تنظیمات ذخیره شد</div>

    <script>
        function scrollToSection(id, element) {
            document.getElementById(id).scrollIntoView({ behavior: 'smooth' });
            if (element) {
                document.querySelectorAll('.nav-item').forEach(el => el.classList.remove('active'));
                element.classList.add('active');
            }
        }

        function showToast(msg) {
            const toast = document.getElementById('toast');
            toast.textContent = msg || 'تنظیمات ذخیره شد';
            toast.classList.add('show');
            setTimeout(() => { toast.classList.remove('show'); }, 2000);
        }

        function saveAllSettings() {
            const settings = {
                LoadImages: document.getElementById('chkLoadImages').checked,
                LoadMedia: document.getElementById('chkLoadMedia').checked,
                EnableJavaScript: document.getElementById('chkEnableJS').checked,
                MuteAudio: document.getElementById('chkMuteAudio').checked,
                EnableCombinerBar: document.getElementById('chkEnableCombinerBar').checked,
                AutoImportImagesToMiniClip: document.getElementById('chkAutoImportImages').checked,
                ShowMiniClipImageImportButtons: document.getElementById('chkShowImportButtons').checked,
                ShowQuickPasteButton: document.getElementById('chkShowQuickPaste').checked,
                EnableAutoQuickPaste: document.getElementById('chkEnableAutoQuickPaste').checked,
                ShowAutoPastePins: document.getElementById('chkShowAutoPastePins').checked,
                AutoPastePinsOpacity: parseInt(document.getElementById('rngPinsOpacity').value) || 100,
                ShowFloatingRecordPlayer: document.getElementById('chkShowRecordPlayer').checked,
                AutoHideStatus: document.getElementById('chkAutoHideStatus').checked,
                ProxyMode: document.getElementById('selProxyMode').value,
                ProxyType: document.getElementById('selProxyType').value,
                ProxyAddress: document.getElementById('txtProxyAddress').value || '',
                ProxyPort: document.getElementById('txtProxyPort').value || ''
            };

            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                    type: 'saveSettings',
                    data: settings
                });
            }
            showToast('تنظیمات ذخیره و اعمال شد');
        }

        function clearCache(deleteAll) {
            const msg = deleteAll ? 'آیا از پاک کردن کامل کوکی‌ها، نشست‌ها و لاگین‌ها اطمینان دارید؟' : 'آیا از پاک کردن حافظه کش اطمینان دارید؟';
            if (confirm(msg)) {
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage({
                        type: 'clearBrowserData',
                        deleteLogin: deleteAll
                    });
                }
                showToast(deleteAll ? 'تمام داده‌های ورود و کش پاک شدند' : 'حافظه کش پاک شد');
            }
        }

        function loadInitialSettings(s) {
            if (!s) return;
            if (s.LoadImages !== undefined) document.getElementById('chkLoadImages').checked = s.LoadImages;
            if (s.LoadMedia !== undefined) document.getElementById('chkLoadMedia').checked = s.LoadMedia;
            if (s.EnableJavaScript !== undefined) document.getElementById('chkEnableJS').checked = s.EnableJavaScript;
            if (s.MuteAudio !== undefined) document.getElementById('chkMuteAudio').checked = s.MuteAudio;
            if (s.EnableCombinerBar !== undefined) document.getElementById('chkEnableCombinerBar').checked = s.EnableCombinerBar;
            if (s.AutoImportImagesToMiniClip !== undefined) document.getElementById('chkAutoImportImages').checked = s.AutoImportImagesToMiniClip;
            if (s.ShowMiniClipImageImportButtons !== undefined) document.getElementById('chkShowImportButtons').checked = s.ShowMiniClipImageImportButtons;
            if (s.ShowQuickPasteButton !== undefined) document.getElementById('chkShowQuickPaste').checked = s.ShowQuickPasteButton;
            if (s.EnableAutoQuickPaste !== undefined) document.getElementById('chkEnableAutoQuickPaste').checked = s.EnableAutoQuickPaste;
            if (s.ShowAutoPastePins !== undefined) document.getElementById('chkShowAutoPastePins').checked = s.ShowAutoPastePins;
            if (s.AutoPastePinsOpacity !== undefined) {
                document.getElementById('rngPinsOpacity').value = s.AutoPastePinsOpacity;
                document.getElementById('lblPinsOpacityVal').innerText = s.AutoPastePinsOpacity + '%';
            }
            if (s.ShowFloatingRecordPlayer !== undefined) document.getElementById('chkShowRecordPlayer').checked = s.ShowFloatingRecordPlayer;
            if (s.AutoHideStatus !== undefined) document.getElementById('chkAutoHideStatus').checked = s.AutoHideStatus;
            
            if (s.ProxyMode) document.getElementById('selProxyMode').value = s.ProxyMode;
            if (s.ProxyType) document.getElementById('selProxyType').value = s.ProxyType;
            if (s.ProxyAddress) document.getElementById('txtProxyAddress').value = s.ProxyAddress;
            if (s.ProxyPort) document.getElementById('txtProxyPort').value = s.ProxyPort;
        }

        // Listen to messages from C#
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.addEventListener('message', event => {
                const msg = event.data;
                if (msg && (msg.type === 'initSettings' || msg.action === 'initSettings')) {
                    loadInitialSettings(msg.data);
                }
            });

            // Request initial settings
            window.chrome.webview.postMessage({ type: 'getSettings' });
        }
    </script>
</body>
</html>";
        }
    }
}
