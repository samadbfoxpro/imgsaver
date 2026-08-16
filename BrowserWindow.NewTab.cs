using System;

namespace imgsaver
{
    public partial class BrowserWindow
    {
        private string GetIconForUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return "✦";
            string lower = url.ToLower();
            if (lower.Contains("google")) return "G";
            if (lower.Contains("github")) return "GH";
            if (lower.Contains("youtube")) return "YT";
            if (lower.Contains("facebook")) return "FB";
            if (lower.Contains("twitter") || lower.Contains("x.com")) return "𝕏";
            if (lower.Contains("instagram")) return "IG";
            if (lower.Contains("reddit")) return "RD";
            if (lower.Contains("amazon")) return "AZ";
            if (lower.Contains("netflix")) return "NF";
            if (lower.Contains("spotify")) return "SP";
            if (lower.Contains("seaart")) return "SA";
            if (lower.Contains("civitai")) return "CV";
            if (lower.Contains("pinterest")) return "PT";
            if (lower.Contains("discord")) return "DC";
            if (lower.Contains("chatgpt") || lower.Contains("openai")) return "AI";
            return "✦";
        }

        private string GetNewTabPageHtml() => """
<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>تب جدید</title>
    <style>
        :root {
            --bg-deep: #0b0f17;
            --card-bg: rgba(23, 30, 44, 0.65);
            --card-border: rgba(255, 255, 255, 0.08);
            --card-border-hover: rgba(56, 189, 248, 0.4);
            --accent-blue: #38bdf8;
            --accent-purple: #818cf8;
            --text-main: #f8fafc;
            --text-muted: #94a3b8;
            --text-dim: #64748b;
        }

        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
            user-select: none;
            -webkit-user-select: none;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Tahoma, 'Vazirmatn', sans-serif;
            background: radial-gradient(ellipse 80% 60% at 50% -10%, rgba(56, 189, 248, 0.15), transparent 70%),
                        radial-gradient(ellipse 60% 50% at 50% 110%, rgba(129, 140, 248, 0.1), transparent 70%),
                        var(--bg-deep);
            color: var(--text-main);
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            padding: 24px;
            overflow-x: hidden;
        }

        /* Ambient Glow Background Effect */
        .ambient-glow {
            position: fixed;
            top: 20%;
            left: 50%;
            transform: translate(-50%, -50%);
            width: 500px;
            height: 300px;
            background: radial-gradient(circle, rgba(56, 189, 248, 0.08) 0%, rgba(129, 140, 248, 0.03) 50%, transparent 80%);
            filter: blur(60px);
            pointer-events: none;
            z-index: 0;
        }

        .container {
            position: relative;
            z-index: 1;
            width: 100%;
            max-width: 780px;
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 32px;
            animation: fadeIn 0.7s cubic-bezier(0.16, 1, 0.3, 1);
        }

        @keyframes fadeIn {
            from {
                opacity: 0;
                transform: translateY(16px);
            }
            to {
                opacity: 1;
                transform: translateY(0);
            }
        }

        /* Clock & Date Header */
        .header {
            display: flex;
            flex-direction: column;
            align-items: center;
            text-align: center;
            gap: 6px;
        }

        .time-display {
            font-size: 58px;
            font-weight: 200;
            letter-spacing: -1px;
            color: #ffffff;
            font-variant-numeric: tabular-nums;
            text-shadow: 0 0 30px rgba(56, 189, 248, 0.25);
            line-height: 1.1;
        }

        .date-display {
            font-size: 14px;
            font-weight: 400;
            color: var(--text-muted);
            letter-spacing: 0.3px;
        }

        /* Search Section */
        .search-wrapper {
            width: 100%;
            max-width: 640px;
            position: relative;
        }

        .search-box {
            display: flex;
            align-items: center;
            gap: 12px;
            background: rgba(18, 24, 38, 0.75);
            border: 1px solid var(--card-border);
            backdrop-filter: blur(20px);
            -webkit-backdrop-filter: blur(20px);
            padding: 10px 14px 10px 20px;
            border-radius: 999px;
            box-shadow: 0 10px 30px -5px rgba(0, 0, 0, 0.4), 0 0 0 1px rgba(255, 255, 255, 0.04);
            transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1);
        }

        .search-box:focus-within {
            border-color: rgba(56, 189, 248, 0.5);
            background: rgba(22, 30, 48, 0.9);
            box-shadow: 0 12px 36px -4px rgba(56, 189, 248, 0.2), 0 0 0 2px rgba(56, 189, 248, 0.25);
            transform: translateY(-1px);
        }

        .search-engine-badge {
            display: flex;
            align-items: center;
            justify-content: center;
            width: 28px;
            height: 28px;
            border-radius: 50%;
            background: rgba(255, 255, 255, 0.06);
            color: var(--accent-blue);
            font-size: 13px;
            font-weight: 700;
            flex-shrink: 0;
        }

        .search-input {
            flex: 1;
            background: transparent;
            border: none;
            outline: none;
            color: #ffffff;
            font-size: 15px;
            font-family: inherit;
            user-select: text;
            -webkit-user-select: text;
        }

        .search-input::placeholder {
            color: var(--text-dim);
            font-weight: 300;
        }

        .search-btn {
            background: linear-gradient(135deg, var(--accent-blue), var(--accent-purple));
            border: none;
            outline: none;
            color: #ffffff;
            width: 36px;
            height: 36px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            cursor: pointer;
            transition: transform 0.2s, box-shadow 0.2s;
            flex-shrink: 0;
        }

        .search-btn:hover {
            transform: scale(1.06);
            box-shadow: 0 4px 14px rgba(56, 189, 248, 0.4);
        }

        .search-btn:active {
            transform: scale(0.96);
        }

        .search-btn svg {
            width: 16px;
            height: 16px;
            fill: none;
            stroke: currentColor;
            stroke-width: 2.2;
            stroke-linecap: round;
            stroke-linejoin: round;
        }

        /* Quick Links Grid */
        .shortcuts-grid {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 14px;
            width: 100%;
            max-width: 640px;
        }

        .shortcut-card {
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 12px 14px;
            background: var(--card-bg);
            border: 1px solid var(--card-border);
            border-radius: 14px;
            backdrop-filter: blur(16px);
            -webkit-backdrop-filter: blur(16px);
            text-decoration: none;
            color: var(--text-main);
            transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
            position: relative;
            overflow: hidden;
        }

        .shortcut-card::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: linear-gradient(135deg, rgba(255, 255, 255, 0.03), transparent);
            opacity: 0;
            transition: opacity 0.2s;
        }

        .shortcut-card:hover {
            transform: translateY(-2px);
            border-color: var(--card-border-hover);
            background: rgba(30, 41, 59, 0.8);
            box-shadow: 0 8px 20px -4px rgba(0, 0, 0, 0.35);
        }

        .shortcut-card:hover::before {
            opacity: 1;
        }

        .shortcut-icon-wrapper {
            width: 36px;
            height: 36px;
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 16px;
            font-weight: 700;
            flex-shrink: 0;
            box-shadow: 0 4px 10px rgba(0, 0, 0, 0.2);
        }

        .icon-google { background: linear-gradient(135deg, #4285f4, #34a853); color: #fff; }
        .icon-chatgpt { background: linear-gradient(135deg, #10a37f, #0d8a6b); color: #fff; }
        .icon-youtube { background: linear-gradient(135deg, #ff0000, #cc0000); color: #fff; }
        .icon-github { background: linear-gradient(135deg, #333333, #24292e); color: #fff; border: 1px solid rgba(255,255,255,0.15); }
        .icon-civitai { background: linear-gradient(135deg, #2563eb, #1d4ed8); color: #fff; }
        .icon-seaart { background: linear-gradient(135deg, #8b5cf6, #6d28d9); color: #fff; }
        .icon-pinterest { background: linear-gradient(135deg, #e60023, #ad081b); color: #fff; }
        .icon-settings { background: linear-gradient(135deg, #475569, #334155); color: #94a3b8; }

        .shortcut-info {
            display: flex;
            flex-direction: column;
            overflow: hidden;
            text-align: right;
        }

        .shortcut-title {
            font-size: 13px;
            font-weight: 500;
            color: #ffffff;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .shortcut-desc {
            font-size: 11px;
            color: var(--text-dim);
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        /* Footer & Shortcuts Help */
        .footer-hint {
            display: flex;
            align-items: center;
            gap: 12px;
            font-size: 12px;
            color: var(--text-dim);
            margin-top: 8px;
        }

        .key-badge {
            background: rgba(255, 255, 255, 0.07);
            border: 1px solid rgba(255, 255, 255, 0.1);
            border-radius: 6px;
            padding: 2px 7px;
            font-size: 11px;
            font-family: monospace;
            color: var(--text-muted);
        }

        @media (max-width: 640px) {
            .shortcuts-grid {
                grid-template-columns: repeat(2, 1fr);
            }
            .time-display {
                font-size: 44px;
            }
        }
    </style>
</head>
<body>
    <div class="ambient-glow"></div>

    <div class="container">
        <!-- Minimal Clock & Date Header -->
        <header class="header">
            <div class="time-display" id="time">--:--</div>
            <div class="date-display" id="date">در حال بارگذاری...</div>
        </header>

        <!-- Search Bar -->
        <div class="search-wrapper">
            <div class="search-box">
                <div class="search-engine-badge">G</div>
                <input 
                    type="text" 
                    class="search-input" 
                    id="searchInput" 
                    placeholder="جستجو در وب یا وارد کردن آدرس سایت..." 
                    autocomplete="off"
                    spellcheck="false"
                    autofocus
                />
                <button class="search-btn" id="searchBtn" title="جستجو">
                    <svg viewBox="0 0 24 24">
                        <circle cx="11" cy="11" r="8"></circle>
                        <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
                    </svg>
                </button>
            </div>
        </div>

        <!-- Shortcuts Quick Access Grid -->
        <div class="shortcuts-grid">
            <a class="shortcut-card" href="https://www.google.com">
                <div class="shortcut-icon-wrapper icon-google">G</div>
                <div class="shortcut-info">
                    <span class="shortcut-title">Google</span>
                    <span class="shortcut-desc">موتور جستجو</span>
                </div>
            </a>

            <a class="shortcut-card" href="https://chatgpt.com">
                <div class="shortcut-icon-wrapper icon-chatgpt">AI</div>
                <div class="shortcut-info">
                    <span class="shortcut-title">ChatGPT</span>
                    <span class="shortcut-desc">هوش مصنوعی</span>
                </div>
            </a>

            <a class="shortcut-card" href="https://www.youtube.com">
                <div class="shortcut-icon-wrapper icon-youtube">YT</div>
                <div class="shortcut-info">
                    <span class="shortcut-title">YouTube</span>
                    <span class="shortcut-desc">ویدیو و کلیپ</span>
                </div>
            </a>

            <a class="shortcut-card" href="https://github.com">
                <div class="shortcut-icon-wrapper icon-github">GH</div>
                <div class="shortcut-info">
                    <span class="shortcut-title">GitHub</span>
                    <span class="shortcut-desc">مخازن کد</span>
                </div>
            </a>

            <a class="shortcut-card" href="https://civitai.com">
                <div class="shortcut-icon-wrapper icon-civitai">CV</div>
                <div class="shortcut-info">
                    <span class="shortcut-title">Civitai</span>
                    <span class="shortcut-desc">مدل‌های AI</span>
                </div>
            </a>

            <a class="shortcut-card" href="https://www.seaart.ai">
                <div class="shortcut-icon-wrapper icon-seaart">SA</div>
                <div class="shortcut-info">
                    <span class="shortcut-title">SeaArt</span>
                    <span class="shortcut-desc">تولید تصویر هوش</span>
                </div>
            </a>

            <a class="shortcut-card" href="https://www.pinterest.com">
                <div class="shortcut-icon-wrapper icon-pinterest">PT</div>
                <div class="shortcut-info">
                    <span class="shortcut-title">Pinterest</span>
                    <span class="shortcut-desc">ایده و تصاویر</span>
                </div>
            </a>

            <a class="shortcut-card" href="imgsaver://settings">
                <div class="shortcut-icon-wrapper icon-settings">⚙</div>
                <div class="shortcut-info">
                    <span class="shortcut-title">تنظیمات</span>
                    <span class="shortcut-desc">تنظیمات مرورگر</span>
                </div>
            </a>
        </div>

        <!-- Minimal Keyboard Shortcut Hint -->
        <div class="footer-hint">
            <span>برای جستجوی سریع کلید</span>
            <span class="key-badge">Enter ↵</span>
            <span>را فشار دهید</span>
        </div>
    </div>

    <script>
        // Clock & Persian/Gregorian Date
        function updateClock() {
            const now = new Date();
            const hours = String(now.getHours()).padStart(2, '0');
            const minutes = String(now.getMinutes()).padStart(2, '0');
            document.getElementById('time').textContent = `${hours}:${minutes}`;

            const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
            try {
                document.getElementById('date').textContent = now.toLocaleDateString('fa-IR', options);
            } catch (e) {
                document.getElementById('date').textContent = now.toLocaleDateString(undefined, options);
            }
        }

        updateClock();
        setInterval(updateClock, 1000);

        // Search & Navigate logic
        const searchInput = document.getElementById('searchInput');
        const searchBtn = document.getElementById('searchBtn');

        function performSearch() {
            let query = searchInput.value.trim();
            if (!query) return;

            if (query === 'imgsaver://settings' || query === 'about:settings' || query === 'settings') {
                window.location.href = 'imgsaver://settings';
                return;
            }

            if (/^(https?:\/\/)/i.test(query)) {
                window.location.href = query;
            } else if (query.includes('.') && !query.includes(' ')) {
                window.location.href = 'https://' + query;
            } else {
                window.location.href = 'https://www.google.com/search?q=' + encodeURIComponent(query);
            }
        }

        searchBtn.addEventListener('click', performSearch);
        searchInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                performSearch();
            }
        });

        // Autofocus input
        window.addEventListener('DOMContentLoaded', () => {
            searchInput.focus();
        });
    </script>
</body>
</html>
""";
    }
}
