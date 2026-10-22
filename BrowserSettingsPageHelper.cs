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

        /* Cache Manager Sub-page styles */
        .kpi-grid {
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 16px;
            margin-bottom: 24px;
        }

        .kpi-card {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 12px;
            padding: 18px 20px;
            display: flex;
            flex-direction: column;
            gap: 6px;
            position: relative;
            overflow: hidden;
        }

        .kpi-label {
            font-size: 12px;
            color: var(--text-secondary);
            font-weight: 500;
        }

        .kpi-value {
            font-size: 24px;
            font-weight: 700;
            color: var(--text-primary);
        }

        .kpi-sub {
            font-size: 11px;
            color: var(--text-muted);
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .category-card {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 12px;
            padding: 16px 20px;
            margin-bottom: 12px;
            transition: all 0.2s;
        }

        .category-card:hover {
            border-color: #474a50;
            background: #2e3034;
        }

        .category-header {
            display: flex;
            align-items: center;
            justify-content: space-between;
        }

        .category-title-group {
            display: flex;
            align-items: center;
            gap: 14px;
        }

        .category-icon {
            font-size: 24px;
            width: 44px;
            height: 44px;
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            background: rgba(255, 255, 255, 0.05);
            flex-shrink: 0;
        }

        .category-names h4 {
            font-size: 14px;
            font-weight: 600;
            color: var(--text-primary);
            margin-bottom: 2px;
        }

        .category-names p {
            font-size: 11px;
            color: var(--text-muted);
        }

        .category-stats-badge {
            text-align: left;
        }

        .category-size {
            font-size: 16px;
            font-weight: 700;
            color: var(--text-primary);
        }

        .category-count {
            font-size: 11px;
            color: var(--text-secondary);
            margin-top: 2px;
        }

        .progress-bar-bg {
            height: 6px;
            background: rgba(255, 255, 255, 0.08);
            border-radius: 6px;
            overflow: hidden;
            margin: 12px 0 10px 0;
        }

        .progress-bar-fill {
            height: 100%;
            border-radius: 6px;
            transition: width 0.4s ease;
        }

        .category-actions {
            display: flex;
            align-items: center;
            justify-content: space-between;
        }

        .percent-text {
            font-size: 12px;
            font-weight: 600;
            color: var(--text-secondary);
        }

        /* Site Table */
        .site-table {
            width: 100%;
            border-collapse: collapse;
        }

        .site-table th {
            text-align: right;
            padding: 10px 16px;
            font-size: 12px;
            color: var(--text-secondary);
            border-bottom: 1px solid var(--border);
            font-weight: 600;
        }

        .site-table td {
            padding: 12px 16px;
            font-size: 13px;
            border-bottom: 1px solid rgba(255, 255, 255, 0.04);
            color: var(--text-primary);
        }

        .site-table tr:hover td {
            background: rgba(255, 255, 255, 0.02);
        }

        @keyframes spin {
            to { transform: rotate(360deg); }
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

        <div class=""nav-item"" onclick=""openCacheManager(this)"" style=""color: #60A5FA;"">
            <svg class=""nav-icon"" viewBox=""0 0 24 24""><path d=""M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M19,19H5V5H19V19M7,10H9V17H7V10M11,7H13V17H11V7M15,13H17V17H15V13Z""/></svg>
            <span style=""font-weight: 600;"">مدیریت پیشرفته کش</span>
        </div>

        <div class=""nav-item"" onclick=""scrollToSection('diagnostic-section', this)"" style=""color: #F59E0B;"">
            <svg class=""nav-icon"" viewBox=""0 0 24 24""><path d=""M20,8H17.19C16.74,7.22 16.12,6.55 15.37,6.04L17,4.41L15.59,3L13.42,5.17C12.96,5.06 12.49,5 12,5C11.51,5 11.04,5.06 10.59,5.17L8.41,3L7,4.41L8.62,6.04C7.88,6.55 7.26,7.22 6.81,8H4V10H6.09C6.04,10.33 6,10.66 6,11V12H4V14H6V15C6,15.34 6.04,15.67 6.09,16H4V18H6.81C7.85,19.79 9.78,21 12,21C14.22,21 16.15,19.79 17.19,18H20V16H17.91C17.96,15.67 18,15.34 18,15V14H20V12H18V11C18,10.66 17.96,10.33 17.91,10H20V8M16,15A4,4 0 0,1 12,19A4,4 0 0,1 8,15V11A4,4 0 0,1 12,7A4,4 0 0,1 16,11V15M14,10V12H10V10H14M10,14H14V16H10V14Z""/></svg>
            <span style=""font-weight: 600;"">عیب‌یابی و لاگ عمیق</span>
        </div>

        <div class=""nav-item"" onclick=""scrollToSection('about-section', this)"">
            <svg class=""nav-icon"" viewBox=""0 0 24 24""><path d=""M11,9H13V7H11M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M11,17H13V11H11V17Z""/></svg>
            <span>درباره مرورگر</span>
        </div>
    </div>

    <!-- Content -->
    <div class=""content"" id=""contentArea"">
        <div id=""mainSettingsView"">
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
                    <h3>بازیابی آخرین تب‌ها و آدرس در شروع (Restore Session on Startup)</h3>
                    <p>باز شدن مجدد آخرین آدرس و تب‌های باز شده در شروع مرورگر (در صورت خاموش بودن، صفحه نیو تب آفلاین پیش‌فرض باز می‌شود)</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkRestoreSession"" onchange=""saveAllSettings()"">
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
            <div class=""row"">
                <div class=""row-info"">
                    <h3>غیرفعال‌سازی کش مرورگر (Disable Browser Cache)</h3>
                    <p>بارگیری مجدد همه فایل‌ها از شبکه بدون استفاده از کش (لود همیشه از اینترنت)</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkDisableCache"" onchange=""saveAllSettings()"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>کش فقط برای رسانه و فونت‌ها (Media &amp; Fonts Only)</h3>
                    <p>فقط عکس‌ها، ویدیوها، صوت‌ها و فونت‌ها از کش خوانده شوند؛ اسکریپت‌ها، دکمه‌ها و کدهای سایت همیشه از سرور دریافت شوند</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkCacheMediaOnly"" onchange=""saveAllSettings()"">
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
            <div class=""row"" style=""background: linear-gradient(135deg, rgba(59, 130, 246, 0.1), rgba(168, 85, 247, 0.08)); border-bottom: 1px solid var(--border);"">
                <div class=""row-info"">
                    <h3 style=""color:#60A5FA; display:flex; align-items:center; gap:8px;"">
                        <span>📊</span> آنالیز هوشمند و تفکیکی فایل‌های کش
                    </h3>
                    <p>مشاهده حجم دقیق عکس‌ها، ویدیوها، کدهای وب، فونت‌ها و پاک‌سازی انتخابی هر دسته به صورت مجزا</p>
                </div>
                <button class=""btn btn-primary"" onclick=""openCacheManager()"" style=""display:flex; align-items:center; gap:6px; font-weight:700;"">
                    <span>ورود به آنالیز کش</span>
                    <span>←</span>
                </button>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>پاک‌سازی سریع حافظه موقت</h3>
                    <p>حذف فایل‌های موقت و کش مرورگر بدون خروج از حساب‌های کاربری</p>
                </div>
                <button class=""btn btn-danger"" onclick=""clearCache(false)"">پاک‌سازی سریع</button>
            </div>
            <div class=""row"">
                <div class=""row-info"">
                    <h3>پاک‌سازی کامل (کوکی‌ها، لاگین و نشست‌ها)</h3>
                    <p>خروج از تمام حساب‌های کاربری و پاک کردن اطلاعات ورود و کوکی‌ها</p>
                </div>
                <button class=""btn btn-danger"" onclick=""clearCache(true)"">پاک‌سازی کامل داده‌ها</button>
            </div>
        </div>

        <!-- Diagnostic Logging Section -->
        <div class=""section-title"" id=""diagnostic-section"">🛠️ ضبط لاگ عیب‌یابی عمیق (Deep Diagnostic Logging)</div>
        <div class=""card"">
            <div class=""row"">
                <div class=""row-info"">
                    <h3>شروع / توقف ضبط لاگ (Record Diagnostic Log)</h3>
                    <p>ثبت حداکثر اطلاعات فنی (خطاهای شبکه، فریم‌ها، استثناهای DevTools، خطاهای CORS/CSP و اسکریپت‌ها) در فایل متنی</p>
                </div>
                <label class=""switch"">
                    <input type=""checkbox"" id=""chkRecordDiagnosticLog"" onchange=""toggleDiagnosticLogging(this.checked)"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""row"" style=""background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 12px 14px; margin-top: 10px; border: 1px solid var(--border);"">
                <div class=""row-info"">
                    <div style=""display: flex; align-items: center; gap: 8px;"">
                        <span id=""diagStatusDot"" style=""width: 10px; height: 10px; border-radius: 50%; background: #6D737B; display: inline-block; transition: all 0.3s;""></span>
                        <span id=""diagStatusText"" style=""font-weight: 600; font-size: 13px; color: var(--text-secondary);"">وضعیت: غیرفعال (بدون بار اضافی)</span>
                    </div>
                    <p style=""font-size: 11px; color: var(--text-muted); margin-top: 4px; font-family: monospace;"">مسیر فایل: Browser_Debug_Log.txt (در پوشه اصلی برنامه)</p>
                </div>
                <div style=""display: flex; gap: 8px;"">
                    <button class=""btn btn-primary"" onclick=""openDiagnosticLogFile()"" style=""font-size: 12px; padding: 6px 14px;"">📄 باز کردن فایل لاگ</button>
                    <button class=""btn btn-danger"" onclick=""clearDiagnosticLogFile()"" style=""font-size: 12px; padding: 6px 14px;"">🗑️ پاک کردن لاگ</button>
                </div>
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
        </div> <!-- End of mainSettingsView -->

        <!-- Dedicated Cache Manager Subpage -->
        <div id=""cacheManagerView"" style=""display: none;"">
            <div style=""display: flex; align-items: center; justify-content: space-between; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid var(--border);"">
                <div>
                    <button class=""btn"" onclick=""closeCacheManager()"" style=""background: var(--bg-hover); color: var(--text-primary); display: flex; align-items: center; gap: 8px; margin-bottom: 10px; padding: 6px 14px;"">
                        <span>← بازگشت به تنظیمات اصلی</span>
                    </button>
                    <h1 style=""font-size: 22px; font-weight: 700; color: var(--text-primary); display: flex; align-items: center; gap: 10px;"">
                        <span>📊</span> آنالیز و مدیریت فایل‌های کش مرورگر
                    </h1>
                    <p style=""color: var(--text-secondary); font-size: 13px; margin-top: 4px;"">
                        آنالیز حجم بر اساس نوع فایل، تفکیک بر اساس وب‌سایت‌ها و پاک‌سازی انتخابی جهت سبکسازی دیسک
                    </p>
                </div>
                <div style=""display: flex; gap: 8px; flex-wrap: wrap;"">
                    <button class=""btn"" onclick=""refreshCacheAnalysis()"" style=""background: var(--bg-card); border: 1px solid var(--border); color: var(--text-primary); display: flex; align-items: center; gap: 6px;"">
                        <span>🔄</span> اسکن مجدد
                    </button>
                    <button class=""btn"" onclick=""openCacheFolder()"" style=""background: var(--bg-card); border: 1px solid var(--border); color: var(--text-primary); display: flex; align-items: center; gap: 6px;"">
                        <span>📂</span> پوشه کش
                    </button>
                    <button class=""btn btn-danger"" onclick=""deleteCacheCategory('all')"" style=""display: flex; align-items: center; gap: 6px;"">
                        <span>🧹</span> حذف کل کش
                    </button>
                </div>
            </div>

            <!-- KPI Summary Cards -->
            <div class=""kpi-grid"">
                <div class=""kpi-card"" style=""border-top: 3px solid #38BDF8;"">
                    <span class=""kpi-label"">کل حجم اشغال‌شده کش</span>
                    <span class=""kpi-value"" id=""kpiTotalSize"">0 B</span>
                    <span class=""kpi-sub"" id=""lblCacheFolderPath"" title=""مسیر ذخیره‌سازی کش"">مسیر پوشه کش</span>
                </div>
                <div class=""kpi-card"" style=""border-top: 3px solid #A855F7;"">
                    <span class=""kpi-label"">تعداد کل فایل‌های کش‌شده</span>
                    <span class=""kpi-value"" id=""kpiTotalFiles"">0 فایل</span>
                    <span class=""kpi-sub"">شامل تصاویر، ویدیو، کدها و فونت‌ها</span>
                </div>
                <div class=""kpi-card"" style=""border-top: 3px solid #10B981;"">
                    <span class=""kpi-label"">تعداد وب‌سایت‌های دارای کش</span>
                    <span class=""kpi-value"" id=""kpiTotalSites"">0 سایت</span>
                    <span class=""kpi-sub"">تفکیک‌شده بر اساس نام دامنه اینترنتی</span>
                </div>
            </div>

            <!-- Category Breakdown Section -->
            <div class=""section-title"" style=""margin-top: 10px;"">
                <span>📁</span> تفکیک حجم بر اساس نوع فایل و پاک‌سازی انتخابی
            </div>
            <div id=""categoryContainer"">
                <!-- Dynamically rendered category cards -->
            </div>

            <!-- Sites Breakdown Section -->
            <div class=""section-title"" style=""margin-top: 28px; display: flex; justify-content: space-between; align-items: center;"">
                <div style=""display: flex; align-items: center; gap: 8px;"">
                    <span>🌐</span> تفکیک کش به تفکیک وب‌سایت‌ها
                </div>
                <div style=""display: flex; gap: 10px; align-items: center;"">
                    <input type=""text"" id=""txtSiteSearch"" placeholder=""🔍 جستجوی وب‌سایت..."" oninput=""filterSites()"" style=""min-width: 160px; height: 32px; padding: 4px 10px; font-size: 12px;"">
                    <select id=""selSiteSort"" onchange=""filterSites()"" style=""min-width: 140px; height: 32px; padding: 4px 8px; font-size: 12px;"">
                        <option value=""size_desc"">حجم (بیشترین)</option>
                        <option value=""size_asc"">حجم (کمترین)</option>
                        <option value=""name_asc"">نام دامنه (الفبا)</option>
                    </select>
                </div>
            </div>

            <div class=""card"" style=""max-height: 380px; overflow-y: auto;"">
                <table class=""site-table"">
                    <thead>
                        <tr>
                            <th style=""width: 45%;"">وب‌سایت / دامنه</th>
                            <th style=""width: 20%;"">تعداد فایل</th>
                            <th style=""width: 20%;"">حجم مصرفی</th>
                            <th style=""width: 15%; text-align: left;"">عملیات</th>
                        </tr>
                    </thead>
                    <tbody id=""tblSitesBody"">
                        <!-- Dynamically rendered site rows -->
                    </tbody>
                </table>
            </div>
        </div>

        <!-- Loading Overlay -->
        <div id=""cacheLoadingOverlay"" style=""display: none; position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0, 0, 0, 0.65); z-index: 1000; align-items: center; justify-content: center; flex-direction: column; gap: 14px;"">
            <div style=""width: 44px; height: 44px; border: 4px solid rgba(59, 130, 246, 0.2); border-top-color: var(--accent); border-radius: 50%; animation: spin 0.8s linear infinite;""></div>
            <div style=""font-weight: 600; font-size: 14px; color: var(--text-primary);"" id=""loadingOverlayText"">در حال اسکن و پردازش فایل‌های کش...</div>
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
                DisableBrowserCache: document.getElementById('chkDisableCache').checked,
                CacheMediaOnly: document.getElementById('chkCacheMediaOnly').checked,
                RestoreSessionOnStartup: document.getElementById('chkRestoreSession').checked,
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
                ProxyPort: document.getElementById('txtProxyPort').value || '',
                RecordDiagnosticLog: document.getElementById('chkRecordDiagnosticLog') ? document.getElementById('chkRecordDiagnosticLog').checked : false
            };

            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                    type: 'saveSettings',
                    data: settings
                });
            }
            showToast('تنظیمات ذخیره و اعمال شد');
        }

        function toggleDiagnosticLogging(enabled) {
            updateDiagStatusUI(enabled);
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                    type: 'setDiagnosticLogging',
                    enabled: enabled
                });
            }
            showToast(enabled ? 'ضبط لاگ عیب‌یابی فعال شد (Browser_Debug_Log.txt)' : 'ضبط لاگ عیب‌یابی متوقف شد');
        }

        function updateDiagStatusUI(enabled) {
            const dot = document.getElementById('diagStatusDot');
            const text = document.getElementById('diagStatusText');
            if (dot && text) {
                if (enabled) {
                    dot.style.background = '#EF4444';
                    dot.style.boxShadow = '0 0 10px #EF4444';
                    text.textContent = 'وضعیت: در حال ضبط لاگ عیب‌یابی... (Recording Active)';
                    text.style.color = '#EF4444';
                } else {
                    dot.style.background = '#6D737B';
                    dot.style.boxShadow = 'none';
                    text.textContent = 'وضعیت: غیرفعال (بدون بار اضافی)';
                    text.style.color = 'var(--text-secondary)';
                }
            }
        }

        function openDiagnosticLogFile() {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ type: 'openDiagnosticLogFile' });
            }
        }

        function clearDiagnosticLogFile() {
            if (confirm('آیا مایلید تمام محتویات قبلی فایل Browser_Debug_Log.txt پاک شوند؟')) {
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage({ type: 'clearDiagnosticLogFile' });
                }
                showToast('فایل لاگ با موفقیت پاک شد');
            }
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
            if (s.DisableBrowserCache !== undefined) document.getElementById('chkDisableCache').checked = s.DisableBrowserCache;
            if (s.CacheMediaOnly !== undefined) document.getElementById('chkCacheMediaOnly').checked = s.CacheMediaOnly;
            if (s.RestoreSessionOnStartup !== undefined) document.getElementById('chkRestoreSession').checked = s.RestoreSessionOnStartup;
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

            if (s.RecordDiagnosticLog !== undefined) {
                const diagChk = document.getElementById('chkRecordDiagnosticLog');
                if (diagChk) diagChk.checked = !!s.RecordDiagnosticLog;
                updateDiagStatusUI(!!s.RecordDiagnosticLog);
            }
        }

        // Cache Management Functions
        let cachedData = null;

        function openCacheManager(navEl) {
            document.getElementById('mainSettingsView').style.display = 'none';
            document.getElementById('cacheManagerView').style.display = 'block';
            if (navEl) {
                document.querySelectorAll('.nav-item').forEach(el => el.classList.remove('active'));
                navEl.classList.add('active');
            }
            refreshCacheAnalysis();
        }

        function closeCacheManager() {
            document.getElementById('cacheManagerView').style.display = 'none';
            document.getElementById('mainSettingsView').style.display = 'block';
            document.querySelectorAll('.nav-item').forEach(el => el.classList.remove('active'));
            const firstNav = document.querySelector('.nav-item');
            if (firstNav) firstNav.classList.add('active');
        }

        function refreshCacheAnalysis() {
            const overlay = document.getElementById('cacheLoadingOverlay');
            document.getElementById('loadingOverlayText').textContent = 'در حال اسکن و آنالیز فایل‌های کش...';
            overlay.style.display = 'flex';
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ type: 'analyzeCache' });
            }
        }

        function deleteCacheCategory(categoryKey) {
            const titles = {
                'images': 'تصاویر و عکس‌ها',
                'media': 'ویدیوها و صوت',
                'webCode': 'کدها و اسکریپت‌های وب',
                'fonts': 'فونت‌های وب',
                'other': 'سایر داده‌ها و کش سیستمی',
                'all': 'تمامی فایل‌های کش مرورگر'
            };
            const catName = titles[categoryKey] || categoryKey;
            const confirmMsg = categoryKey === 'all'
                ? 'آیا از پاک‌سازی تمامی فایل‌های کش مرورگر اطمینان دارید؟'
                : `آیا از پاک‌سازی فایل‌های مربوط به دسته «${catName}» اطمینان دارید؟`;

            if (confirm(confirmMsg)) {
                const overlay = document.getElementById('cacheLoadingOverlay');
                document.getElementById('loadingOverlayText').textContent = `در حال پاک‌سازی ${catName}...`;
                overlay.style.display = 'flex';
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage({
                        type: 'deleteCacheCategory',
                        category: categoryKey
                    });
                }
            }
        }

        function deleteSiteCache(domain) {
            if (confirm('آیا از حذف کامل فایل‌های کش وب‌سایت «' + domain + '» اطمینان دارید؟')) {
                const overlay = document.getElementById('cacheLoadingOverlay');
                document.getElementById('loadingOverlayText').textContent = 'در حال حذف کش ' + domain + '...';
                overlay.style.display = 'flex';
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage({
                        type: 'deleteSiteCache',
                        domain: domain
                    });
                }
            }
        }

        function openCacheFolder() {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ type: 'openCacheFolder' });
            }
        }

        function renderCacheAnalysis(data) {
            cachedData = data;
            if (!data) return;

            // KPIs
            document.getElementById('kpiTotalSize').textContent = data.FormattedTotalSize || '0 B';
            document.getElementById('kpiTotalFiles').textContent = (data.TotalFileCount || 0).toLocaleString() + ' فایل';
            document.getElementById('kpiTotalSites').textContent = (data.TotalSiteCount || 0).toLocaleString() + ' وب‌سایت';
            document.getElementById('lblCacheFolderPath').textContent = data.CacheFolderPath || '';

            // Categories
            const catContainer = document.getElementById('categoryContainer');
            catContainer.innerHTML = '';

            const catOrder = ['images', 'media', 'webCode', 'fonts', 'other'];
            catOrder.forEach(key => {
                const cat = data.Categories ? data.Categories[key] : null;
                if (!cat) return;

                const card = document.createElement('div');
                card.className = 'category-card';
                card.innerHTML = 
                    '<div class=""category-header"">' +
                        '<div class=""category-title-group"">' +
                            '<div class=""category-icon"">' + (cat.Icon || '📁') + '</div>' +
                            '<div class=""category-names"">' +
                                '<h4>' + (cat.Title || key) + '</h4>' +
                                '<p>' + (cat.Description || '') + '</p>' +
                            '</div>' +
                        '</div>' +
                        '<div class=""category-stats-badge"">' +
                            '<div class=""category-size"" style=""color: ' + cat.ColorHex + ';"">' + (cat.FormattedSize || '0 B') + '</div>' +
                            '<div class=""category-count"">' + (cat.FileCount || 0).toLocaleString() + ' فایل</div>' +
                        '</div>' +
                    '</div>' +
                    '<div class=""progress-bar-bg"">' +
                        '<div class=""progress-bar-fill"" style=""width: ' + (cat.Percentage || 0) + '%; background-color: ' + cat.ColorHex + ';""></div>' +
                    '</div>' +
                    '<div class=""category-actions"">' +
                        '<span class=""percent-text"" style=""color: ' + cat.ColorHex + ';"">' + (cat.Percentage || 0) + '% از کل کش</span>' +
                        '<button class=""btn btn-danger"" onclick=""deleteCacheCategory(\'' + cat.Key + '\')"" ' + (cat.FileCount === 0 ? 'disabled style=""opacity:0.4; cursor:not-allowed; padding: 5px 12px; font-size: 12px; display: flex; align-items: center; gap: 4px;""' : 'style=""padding: 5px 12px; font-size: 12px; display: flex; align-items: center; gap: 4px;""') + '>' +
                            '<span>🗑️</span>' +
                            '<span>پاک‌سازی این دسته</span>' +
                        '</button>' +
                    '</div>';
                catContainer.appendChild(card);
            });

            // Render Sites
            filterSites();
        }

        function filterSites() {
            if (!cachedData || !cachedData.Sites) return;

            const search = (document.getElementById('txtSiteSearch').value || '').trim().toLowerCase();
            const sortMode = document.getElementById('selSiteSort').value;

            let filtered = cachedData.Sites.filter(s => !search || s.Domain.toLowerCase().includes(search));

            if (sortMode === 'size_desc') {
                filtered.sort((a, b) => b.SizeBytes - a.SizeBytes);
            } else if (sortMode === 'size_asc') {
                filtered.sort((a, b) => a.SizeBytes - b.SizeBytes);
            } else if (sortMode === 'name_asc') {
                filtered.sort((a, b) => a.Domain.localeCompare(b.Domain));
            }

            const tbody = document.getElementById('tblSitesBody');
            tbody.innerHTML = '';

            if (filtered.length === 0) {
                const tr = document.createElement('tr');
                tr.innerHTML = '<td colspan=""4"" style=""text-align: center; color: var(--text-muted); padding: 24px;"">هیچ وب‌سایتی با این مشخصات یافت نشد</td>';
                tbody.appendChild(tr);
                return;
            }

            filtered.forEach(site => {
                const tr = document.createElement('tr');
                tr.innerHTML = 
                    '<td style=""font-weight: 600; display: flex; align-items: center; gap: 8px;"">' +
                        '<span style=""font-size: 14px;"">🌐</span>' +
                        '<span>' + site.Domain + '</span>' +
                    '</td>' +
                    '<td style=""color: var(--text-secondary);"">' + (site.FileCount || 0).toLocaleString() + ' فایل</td>' +
                    '<td style=""font-weight: 700; color: #38BDF8;"">' + site.FormattedSize + '</td>' +
                    '<td style=""text-align: left;"">' +
                        '<button class=""btn btn-danger"" onclick=""deleteSiteCache(\'' + site.Domain + '\')"" style=""padding: 4px 10px; font-size: 11px;"">' +
                            'حذف کش' +
                        '</button>' +
                    '</td>';
                tbody.appendChild(tr);
            });
        }

        // Listen to messages from C#
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.addEventListener('message', event => {
                const msg = event.data;
                if (!msg) return;
                if (msg.type === 'initSettings' || msg.action === 'initSettings') {
                    loadInitialSettings(msg.data);
                } else if (msg.type === 'cacheAnalysisResult') {
                    const overlay = document.getElementById('cacheLoadingOverlay');
                    if (overlay) overlay.style.display = 'none';
                    renderCacheAnalysis(msg.data);
                    if (msg.message) {
                        showToast(msg.message);
                    }
                } else if (msg.type === 'diagnosticStatusChanged') {
                    const chk = document.getElementById('chkRecordDiagnosticLog');
                    if (chk) chk.checked = !!msg.enabled;
                    updateDiagStatusUI(!!msg.enabled);
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
