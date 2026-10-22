using System;

namespace imgsaver
{
    public static class BrowserCombinerDiffPageHelper
    {
        public const string DiffUrl = "imgsaver://diff";
        public const string AltDiffUrl = "about:diff";

        public static bool IsDiffUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.Equals(DiffUrl, StringComparison.OrdinalIgnoreCase) ||
                   url.Equals(AltDiffUrl, StringComparison.OrdinalIgnoreCase) ||
                   url.Equals("diff", StringComparison.OrdinalIgnoreCase) ||
                   url.Equals("webino://diff", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetDiffHtml(string initialTitle = "", string initialCustomText = "")
        {
            string safeTitle = System.Web.HttpUtility.JavaScriptStringEncode(initialTitle ?? "");
            string safeText = System.Web.HttpUtility.JavaScriptStringEncode(initialCustomText ?? "");

            return @"<!DOCTYPE html>
<html lang=""fa"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>استخراج تفاوت پرامپت (Prompt Diff Extractor)</title>
    <style>
        :root {
            color-scheme: dark;
            --bg-main: #0F1015;
            --bg-card: #17181F;
            --bg-input: #121318;
            --accent: #10B981;
            --accent-hover: #059669;
            --accent-blue: #38BDF8;
            --accent-purple: #A855F7;
            --text-main: #F8FAFC;
            --text-sub: #94A3B8;
            --border: #262833;
            --danger: #EF4444;
        }

        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, Tahoma, sans-serif;
        }

        body {
            background-color: var(--bg-main);
            color: var(--text-main);
            padding: 24px;
            display: flex;
            justify-content: center;
        }

        .container {
            width: 100%;
            max-width: 1050px;
            display: flex;
            flex-direction: column;
            gap: 20px;
        }

        .header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding-bottom: 16px;
            border-bottom: 1px solid var(--border);
        }

        .header-title {
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .header-icon {
            font-size: 28px;
            background: linear-gradient(135deg, #10B981, #38BDF8);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }

        .header h1 {
            font-size: 20px;
            font-weight: 700;
        }

        .header p {
            font-size: 13px;
            color: var(--text-sub);
            margin-top: 4px;
        }

        .grid-2 {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 16px;
        }

        .card {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 12px;
            padding: 16px;
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        .card-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .card-title {
            font-size: 14px;
            font-weight: 600;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .badge {
            font-size: 11px;
            padding: 2px 8px;
            border-radius: 6px;
            background: rgba(56, 189, 248, 0.15);
            color: var(--accent-blue);
        }

        .badge-green {
            background: rgba(16, 185, 129, 0.15);
            color: var(--accent);
        }

        .badge-purple {
            background: rgba(168, 85, 247, 0.15);
            color: var(--accent-purple);
        }

        textarea {
            width: 100%;
            height: 140px;
            background: var(--bg-input);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 12px;
            color: #E2E8F0;
            font-size: 13px;
            line-height: 1.5;
            resize: vertical;
            outline: none;
            direction: ltr;
            text-align: left;
            transition: border-color 0.2s;
        }

        textarea:focus {
            border-color: var(--accent);
        }

        input[type=""text""] {
            background: var(--bg-input);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 8px 12px;
            color: #E2E8F0;
            font-size: 13px;
            outline: none;
            width: 100%;
            transition: border-color 0.2s;
        }

        input[type=""text""]:focus {
            border-color: var(--accent);
        }

        .options-bar {
            display: flex;
            flex-wrap: wrap;
            gap: 14px;
            align-items: center;
            padding: 12px 16px;
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 12px;
            font-size: 13px;
        }

        .options-bar label {
            display: flex;
            align-items: center;
            gap: 6px;
            cursor: pointer;
            color: var(--text-sub);
            user-select: none;
        }

        .options-bar label:hover {
            color: var(--text-main);
        }

        .options-bar input[type=""checkbox""] {
            accent-color: var(--accent);
            width: 16px;
            height: 16px;
        }

        .result-box {
            background: #0D1C17;
            border: 1px solid #10B981;
            border-radius: 12px;
            padding: 18px;
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        .result-actions {
            display: flex;
            justify-content: flex-end;
            gap: 10px;
        }

        button {
            cursor: pointer;
            border: none;
            border-radius: 8px;
            padding: 8px 16px;
            font-size: 13px;
            font-weight: 600;
            display: flex;
            align-items: center;
            gap: 6px;
            transition: all 0.2s;
        }

        .btn-primary {
            background: var(--accent);
            color: #064E3B;
            font-weight: 700;
        }

        .btn-primary:hover {
            background: #34D399;
            transform: translateY(-1px);
        }

        .btn-secondary {
            background: #20242E;
            color: #E2E8F0;
            border: 1px solid var(--border);
        }

        .btn-secondary:hover {
            background: #2D3342;
        }

        .btn-danger {
            background: rgba(239, 68, 68, 0.15);
            color: var(--danger);
        }

        .btn-danger:hover {
            background: rgba(239, 68, 68, 0.25);
        }

        .tags-container {
            display: flex;
            flex-wrap: wrap;
            gap: 6px;
            max-height: 120px;
            overflow-y: auto;
            direction: ltr;
        }

        .tag-pill {
            background: rgba(16, 185, 129, 0.2);
            border: 1px solid #10B981;
            color: #A7F3D0;
            padding: 3px 8px;
            border-radius: 6px;
            font-size: 12px;
            display: inline-flex;
            align-items: center;
            gap: 4px;
        }

        .toast {
            position: fixed;
            bottom: 24px;
            left: 50%;
            transform: translateX(-50%);
            background: #10B981;
            color: #064E3B;
            font-weight: 700;
            padding: 10px 20px;
            border-radius: 8px;
            display: none;
            box-shadow: 0 4px 14px rgba(0, 0, 0, 0.4);
            animation: fadeIn 0.2s ease;
            z-index: 100;
        }

        @keyframes fadeIn {
            from { opacity: 0; transform: translate(-50%, 10px); }
            to { opacity: 1; transform: translate(-50%, 0); }
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""header-title"">
                <span class=""header-icon"">⚡</span>
                <div>
                    <h1>استخراج هوشمند تفاوت پرامپت (Prompt Diff)</h1>
                    <p>مقایسه دو نسخه پرامپت و استخراج بخش‌های اضافه یا تغییریافته برای درج در متن سفارشی کمباینر</p>
                </div>
            </div>
            <div style=""display:flex; gap:8px;"">
                <button class=""btn-secondary"" onclick=""pasteFromClipboard(1)"">📋 پیست در نسخه کامل</button>
                <button class=""btn-secondary"" onclick=""pasteFromClipboard(2)"">📋 پیست در نسخه پایه</button>
            </div>
        </div>

        <div class=""grid-2"">
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">
                        <span>نسخه کامل / پرامپت اصلی (Full Prompt)</span>
                    </span>
                    <span class=""badge badge-green"" id=""countFull"">۰ تگ</span>
                </div>
                <textarea id=""txtFull"" placeholder=""پرامپت کامل یا غنی‌شده را اینجا وارد یا پیست کنید..."" oninput=""calculateDiff()""></textarea>
            </div>

            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">
                        <span>نسخه ناقص / پرامپت پایه (Base / Incomplete)</span>
                    </span>
                    <span class=""badge badge-purple"" id=""countBase"">۰ تگ</span>
                </div>
                <textarea id=""txtBase"" placeholder=""پرامپت پایه یا ناقصی که می‌خواهید تفاوتش با نسخه کامل پیدا شود..."" oninput=""calculateDiff()""></textarea>
            </div>
        </div>

        <div class=""options-bar"">
            <span style=""font-weight:600; color:var(--text-main);"">تنظیمات استخراج:</span>
            <label>
                <input type=""checkbox"" id=""optExactOrder"" checked onchange=""calculateDiff()"">
                حفظ ترتیب تگ‌ها از متن اصلی
            </label>
            <label>
                <input type=""checkbox"" id=""optCaseSensitive"" onchange=""calculateDiff()"">
                حساس به بزرگی/کوچکی حروف (Case Sensitive)
            </label>
            <label>
                <input type=""checkbox"" id=""optWordLevel"" onchange=""calculateDiff()"">
                استخراج بر اساس کلمات (علاوه بر تگ‌های کاما)
            </label>
            <button class=""btn-danger"" style=""margin-right:auto; padding:4px 10px; font-size:12px;"" onclick=""clearInputs()"">پاکسازی فیلدها</button>
        </div>

        <div class=""result-box"">
            <div class=""card-header"">
                <span class=""card-title"" style=""color:#A7F3D0; font-size:15px;"">
                    <span>✨ تفاوت استخراج شده (Extracted Difference)</span>
                </span>
                <span class=""badge badge-green"" id=""countDiff"">۰ تگ استخراج شد</span>
            </div>

            <div class=""tags-container"" id=""tagsPreview""></div>

            <div>
                <label style=""font-size:12px; color:#A7F3D0; margin-bottom:4px; display:block;"">متن تفاوت نهایی (قابل ویرایش):</label>
                <textarea id=""txtResult"" style=""height:90px; border-color:#059669; color:#A7F3D0; font-weight:600;""></textarea>
            </div>

            <div style=""display:grid; grid-template-columns: 1fr auto; gap:12px; align-items:flex-end;"">
                <div>
                    <label style=""font-size:12px; color:var(--text-sub); margin-bottom:4px; display:block;"">عنوان نمایشی در مینی کلیپ (Title):</label>
                    <input type=""text"" id=""txtTitle"" placeholder=""مثال: استایل اضافه، افکت نوری، لباس... (اختیاری)"">
                </div>
                <div class=""result-actions"">
                    <button class=""btn-secondary"" onclick=""copyResult()"">📋 کپی متن</button>
                    <button class=""btn-primary"" onclick=""applyToCombiner()"">⚡ اعمال مستقیم به کمباینر</button>
                </div>
            </div>
        </div>
    </div>

    <div class=""toast"" id=""toast"">عملیات با موفقیت انجام شد!</div>

    <script>
        const initialTitle = """ + safeTitle + @""";
        const initialCustomText = """ + safeText + @""";

        window.onload = function() {
            if (initialTitle) document.getElementById('txtTitle').value = initialTitle;
            if (initialCustomText) {
                document.getElementById('txtResult').value = initialCustomText;
                document.getElementById('txtFull').value = initialCustomText;
            }
        };

        function showToast(msg) {
            const t = document.getElementById('toast');
            t.innerText = msg;
            t.style.display = 'block';
            setTimeout(() => { t.style.display = 'none'; }, 2200);
        }

        async function pasteFromClipboard(target) {
            try {
                const text = await navigator.clipboard.readText();
                if (target === 1) document.getElementById('txtFull').value = text;
                else if (target === 2) document.getElementById('txtBase').value = text;
                calculateDiff();
                showToast('متن از کلیپ‌بورد پیست شد');
            } catch (e) {
                alert('دسترسی به کلیپ‌بورد امکان‌پذیر نیست.');
            }
        }

        function clearInputs() {
            document.getElementById('txtFull').value = '';
            document.getElementById('txtBase').value = '';
            document.getElementById('txtResult').value = '';
            document.getElementById('tagsPreview').innerHTML = '';
            updateCounts(0, 0, 0);
        }

        function updateCounts(full, base, diff) {
            document.getElementById('countFull').innerText = full + ' تگ';
            document.getElementById('countBase').innerText = base + ' تگ';
            document.getElementById('countDiff').innerText = diff + ' تگ استخراج شد';
        }

        function extractTags(text, wordLevel) {
            if (!text || !text.trim()) return [];
            let delims = [',', ';', '\n', '\r'];
            if (wordLevel) delims.push(' ');
            
            return text.split(new RegExp('[' + delims.join('') + ']+'))
                       .map(t => t.trim())
                       .filter(t => t.length > 0);
        }

        function calculateDiff() {
            const fullText = document.getElementById('txtFull').value;
            const baseText = document.getElementById('txtBase').value;
            const caseSensitive = document.getElementById('optCaseSensitive').checked;
            const wordLevel = document.getElementById('optWordLevel').checked;

            const fullTags = extractTags(fullText, wordLevel);
            const baseTags = extractTags(baseText, wordLevel);

            const baseSet = new Set(baseTags.map(t => caseSensitive ? t : t.toLowerCase()));

            const diffTags = [];
            const seen = new Set();

            for (const tag of fullTags) {
                const key = caseSensitive ? tag : tag.toLowerCase();
                if (!baseSet.has(key) && !seen.has(key)) {
                    seen.add(key);
                    diffTags.push(tag);
                }
            }

            updateCounts(fullTags.length, baseTags.length, diffTags.length);

            // Render tags preview
            const previewContainer = document.getElementById('tagsPreview');
            previewContainer.innerHTML = '';
            diffTags.forEach(t => {
                const pill = document.createElement('span');
                pill.className = 'tag-pill';
                pill.innerText = t;
                previewContainer.appendChild(pill);
            });

            document.getElementById('txtResult').value = diffTags.join(', ');
        }

        function copyResult() {
            const res = document.getElementById('txtResult').value;
            if (!res) return;
            navigator.clipboard.writeText(res);
            showToast('متن تفاوت در کلیپ‌بورد کپی شد!');
        }

        function applyToCombiner() {
            const customText = document.getElementById('txtResult').value.trim();
            const customTitle = document.getElementById('txtTitle').value.trim();

            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                    type: 'applyDiffToCombiner',
                    customText: customText,
                    customTitle: customTitle
                });
                showToast('تفاوت به کمباینر مرورگر اعمال شد!');
            } else {
                navigator.clipboard.writeText(customText);
                alert('متن در کلیپ‌بورد کپی شد.');
            }
        }
    </script>
</body>
</html>";
        }
    }
}
