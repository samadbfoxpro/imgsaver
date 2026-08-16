using System;

namespace imgsaver
{
    public static class BrowserCombinerPageHelper
    {
        public const string CombinerUrl = "imgsaver://combiner";
        public const string AltCombinerUrl = "about:combiner";

        public static bool IsCombinerUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.Equals(CombinerUrl, StringComparison.OrdinalIgnoreCase) ||
                   url.Equals(AltCombinerUrl, StringComparison.OrdinalIgnoreCase) ||
                   url.Equals("combiner", StringComparison.OrdinalIgnoreCase) ||
                   url.Equals("webino://combiner", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetCombinerHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""fa"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>مدیریت هوشمند کمباینر پرامپت</title>
    <style>
        :root {
            color-scheme: dark;
            --bg-primary: #121316;
            --bg-secondary: #18191C;
            --bg-card: #1E1F23;
            --bg-card-hover: #26282E;
            --bg-input: #15161A;
            --bg-modal: #1C1D22;
            --accent: #38BDF8;
            --accent-hover: #0EA5E9;
            --accent-green: #10B981;
            --accent-purple: #A855F7;
            --text-primary: #F8FAFC;
            --text-secondary: #94A3B8;
            --text-muted: #64748B;
            --border: #2D3139;
            --border-highlight: #38BDF8;
            --danger: #EF4444;
            --danger-bg: #2B1515;
            --danger-border: #7F1D1D;
        }

        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Tahoma, sans-serif;
            user-select: none;
        }

        /* Custom Dark Scrollbars */
        ::-webkit-scrollbar {
            width: 7px;
            height: 7px;
        }
        ::-webkit-scrollbar-track {
            background: var(--bg-primary);
        }
        ::-webkit-scrollbar-thumb {
            background: #2D3139;
            border-radius: 4px;
        }
        ::-webkit-scrollbar-thumb:hover {
            background: #475569;
        }
        * {
            scrollbar-width: thin;
            scrollbar-color: #2D3139 var(--bg-primary);
        }

        body {
            background-color: var(--bg-primary);
            color: var(--text-primary);
            height: 100vh;
            display: flex;
            flex-direction: column;
            overflow: hidden;
        }

        /* Top Header Bar */
        .header {
            background-color: var(--bg-secondary);
            border-bottom: 1px solid var(--border);
            padding: 14px 28px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            flex-shrink: 0;
        }

        .header-title-box {
            display: flex;
            align-items: center;
            gap: 14px;
        }

        .logo-icon {
            width: 38px;
            height: 38px;
            background: linear-gradient(135deg, #0284C7, #06B6D4);
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            box-shadow: 0 4px 14px rgba(2, 132, 199, 0.35);
        }

        .logo-icon svg {
            width: 22px;
            height: 22px;
            fill: white;
        }

        .header-text h1 {
            font-size: 16.5px;
            font-weight: 700;
            color: #FFFFFF;
            letter-spacing: -0.2px;
        }

        .header-text p {
            font-size: 12px;
            color: var(--text-secondary);
            margin-top: 2px;
        }

        .header-actions {
            display: flex;
            align-items: center;
            gap: 10px;
        }

        /* Main Workspace: 2-Column Split */
        .workspace {
            flex: 1;
            display: grid;
            grid-template-columns: 330px 1fr;
            padding: 18px 28px;
            gap: 18px;
            overflow: hidden;
        }

        /* Panel Container */
        .panel {
            background-color: var(--bg-secondary);
            border: 1px solid var(--border);
            border-radius: 12px;
            display: flex;
            flex-direction: column;
            overflow: hidden;
            box-shadow: 0 4px 20px rgba(0, 0, 0, 0.25);
        }

        .panel-header {
            padding: 12px 18px;
            border-bottom: 1px solid var(--border);
            display: flex;
            align-items: center;
            justify-content: space-between;
            background: rgba(255, 255, 255, 0.015);
        }

        .panel-header-title {
            font-size: 13.5px;
            font-weight: 700;
            display: flex;
            align-items: center;
            gap: 8px;
            color: var(--text-primary);
        }

        .panel-body {
            flex: 1;
            padding: 14px 16px;
            overflow-y: auto;
            display: flex;
            flex-direction: column;
            gap: 8px;
        }

        /* Category Items */
        .folder-card {
            background-color: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 10px 14px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            cursor: pointer;
            transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
            gap: 8px;
        }

        .folder-card:hover {
            background-color: var(--bg-card-hover);
            border-color: #475569;
        }

        .folder-card.active {
            background-color: #172E48;
            border-color: var(--accent);
            box-shadow: 0 0 0 1px var(--accent);
        }

        .folder-card-info {
            display: flex;
            align-items: center;
            gap: 8px;
            min-width: 0;
            flex: 1;
        }

        .folder-name {
            font-size: 13px;
            font-weight: 600;
            color: var(--text-primary);
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .folder-card.active .folder-name {
            color: #38BDF8;
        }

        .folder-badge {
            font-size: 11px;
            font-weight: bold;
            padding: 2px 7px;
            border-radius: 10px;
            background: #262930;
            color: var(--text-secondary);
            white-space: nowrap;
        }

        .folder-card.active .folder-badge {
            background: #0284C7;
            color: #FFFFFF;
        }

        .folder-comma-tag {
            display: flex;
            align-items: center;
            gap: 4px;
            background: #111827;
            border: 1px solid #0284C7;
            padding: 2px 6px;
            border-radius: 6px;
            font-size: 11px;
            color: #38BDF8;
            font-weight: bold;
            white-space: nowrap;
        }

        .folder-comma-input {
            width: 36px !important;
            height: 22px !important;
            padding: 0 2px !important;
            text-align: center !important;
            font-weight: bold !important;
            font-size: 11.5px !important;
            background: #1E293B !important;
            border: 1px solid #38BDF8 !important;
            color: #FFFFFF !important;
            border-radius: 4px !important;
        }

        .folder-actions {
            display: flex;
            align-items: center;
            gap: 4px;
            opacity: 0;
            transition: opacity 0.15s ease;
        }

        .folder-card:hover .folder-actions {
            opacity: 1;
        }

        /* Right Panel: Snippets and Settings */
        .detail-container {
            display: flex;
            flex-direction: column;
            height: 100%;
            gap: 12px;
        }

        /* Folder Settings Banner */
        .folder-options-card {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 10px;
            padding: 12px 18px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            flex-wrap: wrap;
            gap: 12px;
        }

        /* Snippet List */
        .snippet-grid {
            flex: 1;
            overflow-y: auto;
            display: flex;
            flex-direction: column;
            gap: 8px;
            padding-right: 2px;
        }

        .snippet-card {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 12px 16px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 14px;
            transition: all 0.15s ease;
        }

        .snippet-card:hover {
            background: var(--bg-card-hover);
            border-color: #475569;
        }

        .snippet-content {
            flex: 1;
            min-width: 0;
        }

        .snippet-title {
            font-size: 13.5px;
            font-weight: 700;
            color: #38BDF8;
            margin-bottom: 4px;
        }

        .snippet-text {
            font-size: 12px;
            color: var(--text-secondary);
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            line-height: 1.4;
        }

        .snippet-actions {
            display: flex;
            align-items: center;
            gap: 4px;
            flex-shrink: 0;
            opacity: 0;
            transition: opacity 0.15s ease;
        }

        .snippet-card:hover .snippet-actions {
            opacity: 1;
        }

        /* Placement Rules Bottom Card */
        .rules-card {
            background-color: var(--bg-secondary);
            border-top: 1px solid var(--border);
            padding: 12px 28px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            flex-shrink: 0;
            flex-wrap: wrap;
            gap: 12px;
        }

        .rules-group {
            display: flex;
            align-items: center;
            gap: 16px;
            flex-wrap: wrap;
        }

        .rules-title {
            font-size: 12.5px;
            font-weight: 700;
            color: var(--text-primary);
            display: flex;
            align-items: center;
            gap: 6px;
        }

        .radio-label {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 12px;
            color: var(--text-secondary);
            cursor: pointer;
            font-weight: 500;
        }

        .radio-label:hover {
            color: var(--text-primary);
        }

        .radio-label input[type=""radio""] {
            accent-color: #38BDF8;
        }

        /* Buttons */
        .btn {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 7px 14px;
            border-radius: 7px;
            font-size: 12.5px;
            font-weight: 600;
            cursor: pointer;
            border: 1px solid transparent;
            transition: all 0.15s;
            outline: none;
        }

        .btn-primary {
            background: linear-gradient(135deg, #0284C7, #0369A1);
            color: white;
            box-shadow: 0 2px 8px rgba(2, 132, 199, 0.25);
        }

        .btn-primary:hover {
            background: linear-gradient(135deg, #0369A1, #075985);
        }

        .btn-secondary {
            background-color: #262930;
            color: var(--text-primary);
            border-color: #383C45;
        }

        .btn-secondary:hover {
            background-color: #323640;
            border-color: #4B515D;
        }

        .btn-danger {
            background-color: var(--danger-bg);
            color: #F87171;
            border-color: var(--danger-border);
        }

        .btn-danger:hover {
            background-color: #451A1A;
            color: #FECACA;
        }

        .btn-icon {
            padding: 6px;
            border-radius: 6px;
            background: transparent;
            border: none;
            color: var(--text-secondary);
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: all 0.15s;
        }

        .btn-icon:hover {
            background: rgba(255, 255, 255, 0.08);
            color: var(--text-primary);
        }

        .btn-icon-danger:hover {
            background: rgba(239, 68, 68, 0.15);
            color: #F87171;
        }

        .btn-icon svg {
            width: 15px;
            height: 15px;
            fill: currentColor;
        }

        /* Inputs */
        input[type=""number""], input[type=""text""], textarea, select {
            background-color: var(--bg-input);
            border: 1px solid var(--border);
            color: var(--text-primary);
            padding: 8px 12px;
            border-radius: 7px;
            font-size: 13px;
            outline: none;
            width: 100%;
            transition: border-color 0.2s, box-shadow 0.2s;
            user-select: text;
        }

        input[type=""number""]:focus, input[type=""text""]:focus, textarea:focus, select:focus {
            border-color: var(--accent);
            box-shadow: 0 0 0 1px var(--accent);
        }

        textarea {
            resize: vertical;
            min-height: 85px;
            line-height: 1.5;
            font-family: 'Consolas', 'Segoe UI', Tahoma, sans-serif;
        }

        /* ============================================================ */
        /* MODERN MODAL DIALOGS                                         */
        /* ============================================================ */
        .modal-overlay {
            position: fixed;
            top: 0;
            left: 0;
            width: 100vw;
            height: 100vh;
            background: rgba(0, 0, 0, 0.75);
            backdrop-filter: blur(5px);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 1000;
            opacity: 0;
            pointer-events: none;
            transition: opacity 0.2s ease;
        }

        .modal-overlay.show {
            opacity: 1;
            pointer-events: auto;
        }

        .modal-box {
            background: var(--bg-modal);
            border: 1px solid #36393E;
            border-radius: 14px;
            width: 500px;
            max-width: 92vw;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.7);
            transform: scale(0.95) translateY(10px);
            transition: transform 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);
            overflow: hidden;
            display: flex;
            flex-direction: column;
        }

        .modal-overlay.show .modal-box {
            transform: scale(1) translateY(0);
        }

        .modal-header {
            padding: 16px 22px;
            border-bottom: 1px solid var(--border);
            display: flex;
            align-items: center;
            justify-content: space-between;
            background: rgba(255, 255, 255, 0.02);
        }

        .modal-header-title {
            font-size: 15px;
            font-weight: 700;
            color: #FFFFFF;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .modal-header-title svg {
            width: 20px;
            height: 20px;
            fill: var(--accent);
        }

        .modal-close-btn {
            background: transparent;
            border: none;
            color: var(--text-secondary);
            font-size: 16px;
            cursor: pointer;
            width: 28px;
            height: 28px;
            border-radius: 6px;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: all 0.15s;
        }

        .modal-close-btn:hover {
            background: rgba(255, 255, 255, 0.1);
            color: white;
        }

        .modal-body {
            padding: 20px 22px;
            display: flex;
            flex-direction: column;
            gap: 16px;
        }

        .form-group {
            display: flex;
            flex-direction: column;
            gap: 6px;
        }

        .form-label {
            font-size: 12.5px;
            font-weight: 600;
            color: var(--text-primary);
            display: flex;
            align-items: center;
            justify-content: space-between;
        }

        .form-label-hint {
            font-size: 11px;
            color: var(--text-muted);
            font-weight: normal;
        }

        .preview-box {
            background: #141518;
            border: 1px dashed var(--border);
            border-radius: 8px;
            padding: 10px 14px;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .preview-badge {
            background: #0284C7;
            color: white;
            font-size: 11.5px;
            font-weight: bold;
            padding: 4px 10px;
            border-radius: 6px;
            box-shadow: 0 2px 6px rgba(2, 132, 199, 0.3);
            white-space: nowrap;
        }

        .preview-text {
            font-size: 11.5px;
            color: var(--text-secondary);
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .modal-footer {
            padding: 14px 22px;
            background: rgba(0, 0, 0, 0.2);
            border-top: 1px solid var(--border);
            display: flex;
            align-items: center;
            justify-content: flex-end;
            gap: 10px;
        }

        /* Toast notification */
        .toast {
            position: fixed;
            bottom: 24px;
            left: 50%;
            transform: translateX(-50%) translateY(100px);
            background-color: #0284C7;
            color: white;
            padding: 10px 22px;
            border-radius: 8px;
            font-size: 13px;
            font-weight: 600;
            box-shadow: 0 6px 20px rgba(0,0,0,0.5);
            opacity: 0;
            transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
            z-index: 2000;
        }

        .toast.show {
            transform: translateX(-50%) translateY(0);
            opacity: 1;
        }
    </style>
</head>
<body>

    <!-- Header Bar -->
    <div class=""header"">
        <div class=""header-title-box"">
            <div class=""logo-icon"">
                <svg viewBox=""0 0 24 24""><path d=""M20.5,11H19V7A2,2 0 0,0 17,5H13V3.5A2.5,2.5 0 0,0 10.5,1A2.5,2.5 0 0,0 8,3.5V5H4A2,2 0 0,0 2,7V11H3.5A2.5,2.5 0 0,1 6,13.5A2.5,2.5 0 0,1 3.5,16H2V20A2,2 0 0,0 4,22H8V20.5A2.5,2.5 0 0,1 10.5,18A2.5,2.5 0 0,1 13,20.5V22H17A2,2 0 0,0 19,20V16H20.5A2.5,2.5 0 0,0 23,13.5A2.5,2.5 0 0,0 20.5,11Z""/></svg>
            </div>
            <div class=""header-text"">
                <h1>مدیریت هوشمند کمباینر پرامپت (Prompt Combiner Manager)</h1>
                <p>پیکربندی دسته‌ها، دکمه‌های پرامپت، قوانین ترکیب خودکار و شخصی‌سازی نوار ابزار</p>
            </div>
        </div>
        <div class=""header-actions"">
            <button class=""btn btn-secondary"" onclick=""requestRefreshData()"">
                <svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M17.65,6.35C16.2,4.9 14.21,4 12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20C15.73,20 18.84,17.45 19.73,14H17.65C16.83,16.33 14.61,18 12,18A6,6 0 0,1 6,12A6,6 0 0,1 12,6C13.66,6 15.14,6.69 16.22,7.78L13,11H20V4L17.65,6.35Z""/></svg>
                بروزرسانی
            </button>
            <button class=""btn btn-primary"" onclick=""saveDataToBackend()"">
                <svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M15,9H5V5H15M12,19A3,3 0 0,1 9,16A3,3 0 0,1 12,13A3,3 0 0,1 15,16A3,3 0 0,1 12,19M17,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V7L17,3Z""/></svg>
                ذخیره و اعمال
            </button>
        </div>
    </div>

    <!-- Main Workspace -->
    <div class=""workspace"">
        <!-- Left Panel: Categories -->
        <div class=""panel"">
            <div class=""panel-header"">
                <div class=""panel-header-title"">
                    <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""#60A5FA""><path d=""M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z""/></svg>
                    <span>دسته‌بندی‌ها (Categories)</span>
                </div>
                <button class=""btn btn-primary"" style=""padding: 4px 10px; font-size: 11.5px;"" onclick=""openAddCategoryModal()"">+ دسته جدید</button>
            </div>
            <div class=""panel-body"" id=""categoryListContainer"">
                <!-- Dynamically Rendered -->
            </div>
        </div>

        <!-- Right Panel: Snippets for active category -->
        <div class=""panel"">
            <div class=""panel-header"">
                <div class=""panel-header-title"" id=""lblActiveCategoryHeader"">
                    <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""#A78BFA""><path d=""M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M19,19H5V5H19V19M17,17H7V15H17V17M17,13H7V11H17V13M17,9H7V7H17V9Z""/></svg>
                    <span>دکمه‌های پرامپت دسته</span>
                </div>
                <button class=""btn btn-primary"" style=""padding: 4px 12px; font-size: 11.5px;"" onclick=""openAddSnippetModal()"">+ افزودن دکمه پرامپت (Add Snippet)</button>
            </div>
            <div class=""panel-body"">
                <div class=""detail-container"">
                    <!-- Folder Options Bar -->
                    <div class=""folder-options-card"" id=""pnlCategoryOptions"" style=""display:none;"">
                        <!-- Dynamic Folder Placement Rule -->
                        <div id=""pnlFolderSpecificPlacement"" style=""display:flex; align-items:center; gap:8px;"">
                            <span style=""font-size:12.5px; font-weight:bold; color:#38BDF8;"">📍 بعد از کامای شماره:</span>
                            <input type=""number"" id=""txtActiveFolderCommaNum"" min=""1"" max=""99"" value=""1"" style=""width:48px; height:26px; text-align:center; font-weight:bold; font-size:13px; padding:2px; color:#38BDF8;"" onchange=""updateActiveFolderComma(this.value)"">
                        </div>
                    </div>

                    <!-- Snippet Items Container -->
                    <div class=""snippet-grid"" id=""snippetGridContainer"">
                        <!-- Dynamically Rendered -->
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Bottom Placement Rules Bar -->
    <div class=""rules-card"">
        <div class=""rules-group"">
            <div class=""rules-title"">
                <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""#F59E0B""><path d=""M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z""/></svg>
                <span>قانون جای‌گذاری پرامپت‌ها:</span>
            </div>
            <label class=""radio-label"">
                <input type=""radio"" name=""radPlacementRule"" value=""0"" id=""radRuleComma"" onchange=""changeGlobalPlacement(0)""> بعد از کامای شماره
                <input type=""number"" id=""txtCommaNum"" min=""1"" max=""99"" value=""1"" style=""width:48px; text-align:center; font-weight:bold;"" onchange=""saveDataToBackend()"">
            </label>
            <label class=""radio-label"">
                <input type=""radio"" name=""radPlacementRule"" value=""1"" id=""radRuleStart"" onchange=""changeGlobalPlacement(1)""> ابتدای پرامپت (Prepend)
            </label>
            <label class=""radio-label"">
                <input type=""radio"" name=""radPlacementRule"" value=""2"" id=""radRuleEnd"" onchange=""changeGlobalPlacement(2)""> انتهای پرامپت (Append)
            </label>
            <label class=""radio-label"" style=""color:#34D399; font-weight:bold;"">
                <input type=""radio"" name=""radPlacementRule"" value=""3"" id=""radRulePerFolder"" onchange=""changeGlobalPlacement(3)""> قانون اختصاصی هر دسته (Per-Folder)
            </label>
        </div>
        <div style=""display:flex; align-items:center; gap:8px;"">
            <label style=""display:flex; align-items:center; gap:6px; font-size:12px; font-weight:600; color:#38BDF8; cursor:pointer;"">
                <input type=""checkbox"" id=""chkStandalone"" onchange=""saveDataToBackend()""> فعال بودن کمباینر در کل سیستم (بدون نیاز به باز بودن مینی‌کلیپ‌بورد)
            </label>
        </div>
    </div>

    <!-- ============================================================ -->
    <!-- MODAL 1: ADD / EDIT SNIPPET DIALOG                           -->
    <!-- ============================================================ -->
    <div class=""modal-overlay"" id=""modalSnippetOverlay"">
        <div class=""modal-box"">
            <div class=""modal-header"">
                <div class=""modal-header-title"">
                    <svg viewBox=""0 0 24 24""><path d=""M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M19,19H5V5H19V19M17,17H7V15H17V17M17,13H7V11H17V13M17,9H7V7H17V9Z""/></svg>
                    <span id=""modalSnippetHeaderTitle"">افزودن دکمه پرامپت جدید</span>
                </div>
                <button class=""modal-close-btn"" onclick=""closeSnippetModal()"">✕</button>
            </div>
            <div class=""modal-body"">
                <!-- Category Select -->
                <div class=""form-group"">
                    <label class=""form-label"">
                        <span>دسته‌بندی مربوطه</span>
                    </label>
                    <select id=""modalSnippetFolderSelect""></select>
                </div>

                <!-- Snippet Button Title -->
                <div class=""form-group"">
                    <label class=""form-label"">
                        <span>عنوان دکمه (کوتاه)</span>
                        <span class=""form-label-hint"">متن نمایش داده شده روی دکمه در نوار ابزار</span>
                    </label>
                    <input type=""text"" id=""modalSnippetTitle"" placeholder=""مثال: Masterpiece 8K"" oninput=""updateSnippetLivePreview()"">
                </div>

                <!-- Snippet Text -->
                <div class=""form-group"">
                    <label class=""form-label"">
                        <span>متن پرامپت کامل</span>
                        <span class=""form-label-hint"">متنی که هنگام ترکیب در پرامپت قرار می‌گیرد</span>
                    </label>
                    <textarea id=""modalSnippetText"" placeholder=""مثال: (masterpiece, best quality, ultra-detailed, 8k resolution)"" oninput=""updateSnippetLivePreview()""></textarea>
                </div>

                <!-- Live Preview -->
                <div class=""form-group"">
                    <label class=""form-label""><span>پیش‌نمایش زنده دکمه در نوار</span></label>
                    <div class=""preview-box"">
                        <div class=""preview-badge"" id=""modalSnippetPreviewBadge"">عنوان دکمه</div>
                        <div class=""preview-text"" id=""modalSnippetPreviewText"">متن پرامپت کامل</div>
                    </div>
                </div>
            </div>
            <div class=""modal-footer"">
                <button class=""btn btn-secondary"" onclick=""closeSnippetModal()"">انصراف</button>
                <button class=""btn btn-primary"" onclick=""saveSnippetFromModal()"">
                    <svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M15,9H5V5H15M12,19A3,3 0 0,1 9,16A3,3 0 0,1 12,13A3,3 0 0,1 15,16A3,3 0 0,1 12,19M17,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V7L17,3Z""/></svg>
                    ذخیره دکمه
                </button>
            </div>
        </div>
    </div>

    <!-- ============================================================ -->
    <!-- MODAL 2: ADD / EDIT CATEGORY DIALOG                          -->
    <!-- ============================================================ -->
    <div class=""modal-overlay"" id=""modalCategoryOverlay"">
        <div class=""modal-box"">
            <div class=""modal-header"">
                <div class=""modal-header-title"">
                    <svg viewBox=""0 0 24 24""><path d=""M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z""/></svg>
                    <span id=""modalCategoryHeaderTitle"">ساخت دسته‌بندی جدید</span>
                </div>
                <button class=""modal-close-btn"" onclick=""closeCategoryModal()"">✕</button>
            </div>
            <div class=""modal-body"">
                <div class=""form-group"">
                    <label class=""form-label"">
                        <span>نام دسته‌بندی</span>
                    </label>
                    <input type=""text"" id=""modalCategoryName"" placeholder=""مثال: کیفیت تصویر (Quality)"" autofocus>
                </div>

                <div class=""form-group"">
                    <label class=""form-label"">
                        <span>شماره کاما برای این دسته (در صورت فعال بودن قانون اختصاصی)</span>
                        <span class=""form-label-hint"">درج متن پس از کامای Nام</span>
                    </label>
                    <input type=""number"" id=""modalCategoryCommaIndex"" min=""1"" max=""99"" value=""1"" style=""width:80px; text-align:center; font-weight:bold;"">
                </div>

                <div class=""form-group"">
                    <label style=""display:flex; align-items:center; gap:10px; font-size:13px; font-weight:600; color:#34D399; cursor:pointer; background:#141518; padding:12px 14px; border-radius:8px; border:1px solid var(--border);"">
                        <input type=""checkbox"" id=""modalCategoryCustomMode"" style=""width:16px; height:16px; accent-color:#10B981;"">
                        <div>
                            <div>حالت تایپ مستقیم متن دستی (Custom Text Mode)</div>
                            <div style=""font-size:11px; color:var(--text-muted); font-weight:normal; margin-top:2px;"">در این حالت به جای دکمه، یک باکس متنی زنده در نوار کمباینر باز می‌شود</div>
                        </div>
                    </label>
                </div>
            </div>
            <div class=""modal-footer"">
                <button class=""btn btn-secondary"" onclick=""closeCategoryModal()"">انصراف</button>
                <button class=""btn btn-primary"" onclick=""saveCategoryFromModal()"">
                    <svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M15,9H5V5H15M12,19A3,3 0 0,1 9,16A3,3 0 0,1 12,13A3,3 0 0,1 15,16A3,3 0 0,1 12,19M17,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V7L17,3Z""/></svg>
                    ذخیره دسته‌بندی
                </button>
            </div>
        </div>
    </div>

    <!-- ============================================================ -->
    <!-- MODAL 3: CONFIRM DELETE DIALOG                               -->
    <!-- ============================================================ -->
    <div class=""modal-overlay"" id=""modalConfirmOverlay"">
        <div class=""modal-box"" style=""width:420px;"">
            <div class=""modal-header"">
                <div class=""modal-header-title"" style=""color:#F87171;"">
                    <svg viewBox=""0 0 24 24"" fill=""#EF4444""><path d=""M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10M11,16V18H13V16""/></svg>
                    <span id=""modalConfirmTitle"">حذف آیتم</span>
                </div>
                <button class=""modal-close-btn"" onclick=""closeConfirmModal()"">✕</button>
            </div>
            <div class=""modal-body"">
                <p id=""modalConfirmMessage"" style=""font-size:13.5px; color:var(--text-secondary); line-height:1.6;""></p>
            </div>
            <div class=""modal-footer"">
                <button class=""btn btn-secondary"" onclick=""closeConfirmModal()"">انصراف</button>
                <button class=""btn btn-danger"" id=""btnModalConfirmDelete"">
                    <svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z""/></svg>
                    حذف قطعی
                </button>
            </div>
        </div>
    </div>

    <div class=""toast"" id=""toast"">تغییرات ذخیره شد</div>

    <script>
        let dataState = {
            IsEnabled: true,
            PlacementMode: 0,
            CommaIndex: 1,
            ActiveFolderId: '',
            ActiveItemIds: [],
            IsStandaloneGlobalEnabled: true,
            Folders: [],
            Items: []
        };

        let selectedFolderId = '';
        let currentEditingSnippetId = null;
        let currentEditingCategoryId = null;

        function showToast(text) {
            const t = document.getElementById('toast');
            t.textContent = text || 'تغییرات ذخیره شد';
            t.classList.add('show');
            setTimeout(() => t.classList.remove('show'), 2000);
        }

        function loadDataFromCSharp(data) {
            if (!data) return;
            dataState = data;

            // Placement mode
            const mode = dataState.PlacementMode || 0;
            if (mode === 1) document.getElementById('radRuleStart').checked = true;
            else if (mode === 2) document.getElementById('radRuleEnd').checked = true;
            else if (mode === 3) document.getElementById('radRulePerFolder').checked = true;
            else document.getElementById('radRuleComma').checked = true;

            document.getElementById('txtCommaNum').value = dataState.CommaIndex || 1;
            document.getElementById('chkStandalone').checked = !!dataState.IsStandaloneGlobalEnabled;

            if (dataState.Folders && dataState.Folders.length > 0) {
                selectedFolderId = dataState.ActiveFolderId || dataState.Folders[0].Id;
            } else {
                selectedFolderId = '';
            }

            renderCategories();
            renderSnippets();
        }

        function saveDataToBackend() {
            dataState.CommaIndex = parseInt(document.getElementById('txtCommaNum').value) || 1;
            dataState.IsStandaloneGlobalEnabled = document.getElementById('chkStandalone').checked;
            dataState.ActiveFolderId = selectedFolderId;

            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                    type: 'saveCombinerData',
                    data: dataState
                });
            }
            showToast('تنظیمات کمباینر ذخیره شد');
        }

        function changeGlobalPlacement(mode) {
            dataState.PlacementMode = mode;
            saveDataToBackend();
            renderCategories();
            renderSnippets();
        }

        function updateFolderComma(folderId, val) {
            const num = parseInt(val) || 1;
            const folder = (dataState.Folders || []).find(f => f.Id === folderId);
            if (folder) {
                folder.CommaIndex = num;
                saveDataToBackend();
                renderCategories();
                renderSnippets();
            }
        }

        function updateActiveFolderComma(val) {
            if (selectedFolderId) updateFolderComma(selectedFolderId, val);
        }

        function renderCategories() {
            const container = document.getElementById('categoryListContainer');
            container.innerHTML = '';

            const isPerFolder = (dataState.PlacementMode === 3);

            if (!dataState.Folders || dataState.Folders.length === 0) {
                container.innerHTML = '<div style=""text-align:center; color:var(--text-muted); padding:20px; font-size:12.5px;"">هیچ دسته‌ای یافت نشد.</div>';
                return;
            }

            // Ensure folders sorted by Order
            dataState.Folders.sort((a, b) => (a.Order || 0) - (b.Order || 0));

            dataState.Folders.forEach((f, idx) => {
                const count = (dataState.Items || []).filter(i => i.FolderId === f.Id).length;
                const card = document.createElement('div');
                card.className = 'folder-card' + (f.Id === selectedFolderId ? ' active' : '');
                card.draggable = true;
                
                card.ondragstart = (e) => {
                    e.dataTransfer.effectAllowed = 'move';
                    e.dataTransfer.setData('text/plain', f.Id);
                    card.style.opacity = '0.5';
                };
                card.ondragend = () => {
                    card.style.opacity = '1';
                };
                card.ondragover = (e) => {
                    e.preventDefault();
                    e.dataTransfer.dropEffect = 'move';
                    card.style.borderColor = '#38BDF8';
                };
                card.ondragleave = () => {
                    card.style.borderColor = '';
                };
                card.ondrop = (e) => {
                    e.preventDefault();
                    card.style.borderColor = '';
                    const draggedFolderId = e.dataTransfer.getData('text/plain');
                    if (draggedFolderId && draggedFolderId !== f.Id) {
                        const fromIdx = dataState.Folders.findIndex(x => x.Id === draggedFolderId);
                        const toIdx = dataState.Folders.findIndex(x => x.Id === f.Id);
                        if (fromIdx !== -1 && toIdx !== -1) {
                            const moved = dataState.Folders.splice(fromIdx, 1)[0];
                            dataState.Folders.splice(toIdx, 0, moved);
                            dataState.Folders.forEach((item, index) => item.Order = index);
                            saveDataToBackend();
                            renderCategories();
                        }
                    }
                };

                card.onclick = () => {
                    selectedFolderId = f.Id;
                    renderCategories();
                    renderSnippets();
                };

                card.innerHTML = `
                    <div class=""folder-card-info"">
                        <span style=""color:#64748B; font-size:14px; cursor:grab; padding:0 2px;"">⋮⋮</span>
                        <div class=""folder-name"">${escapeHtml(f.Name)}</div>
                    </div>
                    <div style=""display:flex; align-items:center; gap:6px;"">
                        <span class=""folder-badge"">${f.IsCustomInput ? 'متن دستی' : count + ' پرامپت'}</span>
                        <div class=""folder-actions"">
                            <button class=""btn-icon"" title=""ویرایش دسته"" onclick=""event.stopPropagation(); openEditCategoryModal('${f.Id}')"">
                                <svg viewBox=""0 0 24 24""><path d=""M20.71,7.04C21.1,6.65 21.1,6 20.71,5.63L18.37,3.29C18,2.9 17.35,2.9 16.96,3.29L15.12,5.12L18.87,8.87M3,17.25V21H6.75L17.81,9.93L14.06,6.18L3,17.25Z""/></svg>
                            </button>
                            <button class=""btn-icon"" title=""حذف این دسته"" onclick=""event.stopPropagation(); openDeleteCategoryModal('${f.Id}')"">
                                <svg viewBox=""0 0 24 24""><path d=""M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z""/></svg>
                            </button>
                        </div>
                    </div>
                `;
                container.appendChild(card);
            });
        }

        function renderSnippets() {
            const headerTitle = document.getElementById('lblActiveCategoryHeader');
            const container = document.getElementById('snippetGridContainer');
            const customModeChk = document.getElementById('chkIsCustomTextMode');
            const pnlSpecificPlacement = document.getElementById('pnlFolderSpecificPlacement');
            const txtActiveFolderComma = document.getElementById('txtActiveFolderCommaNum');

            const isPerFolder = (dataState.PlacementMode === 3);

            const currentFolder = dataState.Folders ? dataState.Folders.find(f => f.Id === selectedFolderId) : null;
            if (!currentFolder) {
                headerTitle.innerHTML = '<span>دکمه‌های پرامپت (دسته‌ای انتخاب نشده)</span>';
                container.innerHTML = '<div style=""text-align:center; color:var(--text-muted); padding:30px; font-size:13px;"">لطفاً یک دسته‌بندی را از ستون راست انتخاب کنید.</div>';
                if (pnlSpecificPlacement) pnlSpecificPlacement.style.display = 'none';
                return;
            }

            headerTitle.innerHTML = `
                <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""#A78BFA""><path d=""M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M19,19H5V5H19V19M17,17H7V15H17V17M17,13H7V11H17V13M17,9H7V7H17V9Z""/></svg>
                <span>دکمه‌های پرامپت دسته: <strong style=""color:#38BDF8;"">${escapeHtml(currentFolder.Name)}</strong></span>
            `;
            if (customModeChk) customModeChk.checked = !!currentFolder.IsCustomInput;

            const pnlCategoryOptions = document.getElementById('pnlCategoryOptions');
            if (pnlCategoryOptions) {
                pnlCategoryOptions.style.display = isPerFolder ? 'flex' : 'none';
                if (isPerFolder && txtActiveFolderComma) {
                    txtActiveFolderComma.value = currentFolder.CommaIndex || 1;
                }
            }

            container.innerHTML = '';

            if (currentFolder.IsCustomInput) {
                container.innerHTML = `
                    <div style=""padding: 30px; text-align: center; color: #34D399; font-size: 13.5px; font-weight: bold; background: var(--bg-card); border-radius: 10px; border: 1px solid var(--border);"">
                        ✍️ حالت تایپ مستقیم متن برای این دسته فعال است.<br>
                        <span style=""font-size: 12px; font-weight: normal; color: var(--text-secondary); margin-top: 6px; display: block;"">
                            هنگامی که این دسته در نوار کمباینر انتخاب شود، یک کادر متنی زنده جهت تایپ سریع متن دلخواه قرار می‌گیرد.
                        </span>
                    </div>
                `;
                return;
            }

            // Get items for current folder sorted by Order
            const items = (dataState.Items || [])
                .filter(i => i.FolderId === selectedFolderId)
                .sort((a, b) => (a.Order || 0) - (b.Order || 0));

            if (items.length === 0) {
                container.innerHTML = '<div style=""text-align:center; color:var(--text-muted); padding:30px; font-size:13px;"">این دسته‌بندی هنوز دکمه‌ای ندارد. برای ایجاد دکمه پرامپت روی <strong>+ افزودن دکمه پرامپت</strong> کلیک کنید.</div>';
                return;
            }

            items.forEach((it, sIdx) => {
                const card = document.createElement('div');
                card.className = 'snippet-card';
                card.draggable = true;

                card.ondragstart = (e) => {
                    e.dataTransfer.effectAllowed = 'move';
                    e.dataTransfer.setData('text/plain', it.Id);
                    card.style.opacity = '0.5';
                };
                card.ondragend = () => {
                    card.style.opacity = '1';
                };
                card.ondragover = (e) => {
                    e.preventDefault();
                    e.dataTransfer.dropEffect = 'move';
                    card.style.borderColor = '#38BDF8';
                };
                card.ondragleave = () => {
                    card.style.borderColor = '';
                };
                card.ondrop = (e) => {
                    e.preventDefault();
                    card.style.borderColor = '';
                    const draggedId = e.dataTransfer.getData('text/plain');
                    if (draggedId && draggedId !== it.Id) {
                        const targetFolderItems = (dataState.Items || [])
                            .filter(x => x.FolderId === selectedFolderId)
                            .sort((a, b) => (a.Order || 0) - (b.Order || 0));

                        const fromIndex = targetFolderItems.findIndex(x => x.Id === draggedId);
                        const toIndex = targetFolderItems.findIndex(x => x.Id === it.Id);
                        if (fromIndex !== -1 && toIndex !== -1) {
                            const moved = targetFolderItems.splice(fromIndex, 1)[0];
                            targetFolderItems.splice(toIndex, 0, moved);
                            
                            targetFolderItems.forEach((x, idx) => {
                                x.Order = idx;
                            });

                            syncFolderItemsOrder(selectedFolderId, targetFolderItems);
                            saveDataToBackend();
                            renderSnippets();
                        }
                    }
                };

                const isFirst = (sIdx === 0);
                const isLast = (sIdx === items.length - 1);

                card.innerHTML = `
                    <div style=""display:flex; align-items:center; gap:10px; flex:1; min-width:0;"">
                        <span style=""color:#64748B; font-size:15px; cursor:grab; padding:0 4px; line-height:1;"">⋮⋮</span>
                        <div class=""snippet-content"">
                            <div class=""snippet-title"">${escapeHtml(it.Title || it.Text)}</div>
                            <div class=""snippet-text"">${escapeHtml(it.Text)}</div>
                        </div>
                    </div>
                    <div class=""snippet-actions"">
                        <!-- Up Button -->
                        <button class=""btn-icon"" title=""انتقال به بالا"" onclick=""event.stopPropagation(); moveSnippet('${it.Id}', -1);"" style=""${isFirst ? 'opacity:0.25; cursor:not-allowed;' : 'opacity:1; cursor:pointer;'}"">
                            <svg viewBox=""0 0 24 24""><path d=""M7.41,15.41L12,10.83L16.59,15.41L18,14L12,8L6,14L7.41,15.41Z""/></svg>
                        </button>
                        <!-- Down Button -->
                        <button class=""btn-icon"" title=""انتقال به پایین"" onclick=""event.stopPropagation(); moveSnippet('${it.Id}', 1);"" style=""${isLast ? 'opacity:0.25; cursor:not-allowed;' : 'opacity:1; cursor:pointer;'}"">
                            <svg viewBox=""0 0 24 24""><path d=""M7.41,8.59L12,13.17L16.59,8.59L18,10L12,16L6,10L7.41,8.59Z""/></svg>
                        </button>
                        <!-- Edit Button (Vector SVG) -->
                        <button class=""btn-icon"" title=""ویرایش دکمه پرامپت"" onclick=""event.stopPropagation(); openEditSnippetModal('${it.Id}')"">
                            <svg viewBox=""0 0 24 24""><path d=""M20.71,7.04C21.1,6.65 21.1,6 20.71,5.63L18.37,3.29C18,2.9 17.35,2.9 16.96,3.29L15.12,5.12L18.87,8.87M3,17.25V21H6.75L17.81,9.93L14.06,6.18L3,17.25Z""/></svg>
                        </button>
                        <!-- Delete Button (Vector SVG - same color as edit) -->
                        <button class=""btn-icon"" title=""حذف این دکمه"" onclick=""event.stopPropagation(); openDeleteSnippetModal('${it.Id}')"">
                            <svg viewBox=""0 0 24 24""><path d=""M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z""/></svg>
                        </button>
                    </div>
                `;
                container.appendChild(card);
            });
        }

        function syncFolderItemsOrder(folderId, sortedFolderItems) {
            const otherItems = (dataState.Items || []).filter(i => i.FolderId !== folderId);
            dataState.Items = [...otherItems, ...sortedFolderItems];
        }

        function moveSnippet(itemId, direction) {
            const targetFolderItems = (dataState.Items || [])
                .filter(i => i.FolderId === selectedFolderId)
                .sort((a, b) => (a.Order || 0) - (b.Order || 0));

            const index = targetFolderItems.findIndex(i => i.Id === itemId);
            if (index === -1) return;
            const newIndex = index + direction;
            if (newIndex < 0 || newIndex >= targetFolderItems.length) return;

            const moved = targetFolderItems.splice(index, 1)[0];
            targetFolderItems.splice(newIndex, 0, moved);

            targetFolderItems.forEach((it, idx) => it.Order = idx);
            syncFolderItemsOrder(selectedFolderId, targetFolderItems);
            saveDataToBackend();
            renderSnippets();
        }

        /* ============================================================ */
        /* MODAL LOGIC: CATEGORIES                                      */
        /* ============================================================ */
        function openAddCategoryModal() {
            currentEditingCategoryId = null;
            document.getElementById('modalCategoryHeaderTitle').textContent = 'ساخت دسته‌بندی جدید';
            document.getElementById('modalCategoryName').value = '';
            document.getElementById('modalCategoryCommaIndex').value = '1';
            document.getElementById('modalCategoryCustomMode').checked = false;
            document.getElementById('modalCategoryOverlay').classList.add('show');
            setTimeout(() => document.getElementById('modalCategoryName').focus(), 50);
        }

        function openEditCategoryModal(folderId) {
            const folder = (dataState.Folders || []).find(f => f.Id === folderId);
            if (!folder) return;
            currentEditingCategoryId = folderId;
            document.getElementById('modalCategoryHeaderTitle').textContent = 'ویرایش دسته‌بندی';
            document.getElementById('modalCategoryName').value = folder.Name || '';
            document.getElementById('modalCategoryCommaIndex').value = folder.CommaIndex || 1;
            document.getElementById('modalCategoryCustomMode').checked = !!folder.IsCustomInput;
            document.getElementById('modalCategoryOverlay').classList.add('show');
            setTimeout(() => {
                const el = document.getElementById('modalCategoryName');
                el.focus();
                el.select();
            }, 50);
        }

        function closeCategoryModal() {
            document.getElementById('modalCategoryOverlay').classList.remove('show');
        }

        function saveCategoryFromModal() {
            const name = document.getElementById('modalCategoryName').value.trim();
            if (!name) {
                alert('لطفاً نام دسته‌بندی را وارد کنید.');
                return;
            }
            const isCustom = document.getElementById('modalCategoryCustomMode').checked;
            const commaIndex = parseInt(document.getElementById('modalCategoryCommaIndex').value) || 1;

            if (currentEditingCategoryId) {
                const folder = dataState.Folders.find(f => f.Id === currentEditingCategoryId);
                if (folder) {
                    folder.Name = name;
                    folder.IsCustomInput = isCustom;
                    folder.CommaIndex = commaIndex;
                }
            } else {
                const newId = 'folder_' + Date.now();
                dataState.Folders.push({
                    Id: newId,
                    Name: name,
                    Order: dataState.Folders.length,
                    PlacementMode: 0,
                    CommaIndex: commaIndex,
                    IsCustomInput: isCustom,
                    CustomInputText: '',
                    IsCustomInputActive: false
                });
                selectedFolderId = newId;
            }

            closeCategoryModal();
            saveDataToBackend();
            renderCategories();
            renderSnippets();
        }

        function openDeleteCategoryModal(folderId) {
            const folder = dataState.Folders.find(f => f.Id === folderId);
            if (!folder) return;

            document.getElementById('modalConfirmTitle').textContent = 'حذف دسته‌بندی';
            document.getElementById('modalConfirmMessage').innerHTML = `آیا از حذف دسته‌بندی <strong style=""color:#F87171;"">${escapeHtml(folder.Name)}</strong> و تمامی دکمه‌های پرامپت درون آن اطمینان دارید؟`;
            document.getElementById('btnModalConfirmDelete').onclick = () => {
                dataState.Folders = dataState.Folders.filter(f => f.Id !== folderId);
                dataState.Items = dataState.Items.filter(i => i.FolderId !== folderId);
                if (selectedFolderId === folderId) {
                    selectedFolderId = dataState.Folders.length > 0 ? dataState.Folders[0].Id : '';
                }
                closeConfirmModal();
                saveDataToBackend();
                renderCategories();
                renderSnippets();
            };
            document.getElementById('modalConfirmOverlay').classList.add('show');
        }

        function toggleCustomTextMode() {
            const folder = dataState.Folders.find(f => f.Id === selectedFolderId);
            if (!folder) return;
            folder.IsCustomInput = document.getElementById('chkIsCustomTextMode').checked;
            saveDataToBackend();
            renderSnippets();
            renderCategories();
        }

        /* ============================================================ */
        /* MODAL LOGIC: SNIPPETS                                        */
        /* ============================================================ */
        function populateFolderSelect(targetFolderId) {
            const sel = document.getElementById('modalSnippetFolderSelect');
            sel.innerHTML = '';
            (dataState.Folders || []).forEach(f => {
                const opt = document.createElement('option');
                opt.value = f.Id;
                opt.textContent = f.Name;
                if (f.Id === targetFolderId) opt.selected = true;
                sel.appendChild(opt);
            });
        }

        function openAddSnippetModal() {
            if (!selectedFolderId) {
                alert('لطفاً ابتدا یک دسته‌بندی را انتخاب کنید.');
                return;
            }
            currentEditingSnippetId = null;
            document.getElementById('modalSnippetHeaderTitle').textContent = 'افزودن دکمه پرامپت جدید';
            populateFolderSelect(selectedFolderId);
            document.getElementById('modalSnippetTitle').value = '';
            document.getElementById('modalSnippetText').value = '';
            updateSnippetLivePreview();
            document.getElementById('modalSnippetOverlay').classList.add('show');
            setTimeout(() => document.getElementById('modalSnippetTitle').focus(), 50);
        }

        function openEditSnippetModal(itemId) {
            const item = (dataState.Items || []).find(i => i.Id === itemId);
            if (!item) return;

            currentEditingSnippetId = itemId;
            document.getElementById('modalSnippetHeaderTitle').textContent = 'ویرایش دکمه پرامپت';
            populateFolderSelect(item.FolderId || selectedFolderId);
            document.getElementById('modalSnippetTitle').value = item.Title || '';
            document.getElementById('modalSnippetText').value = item.Text || '';
            updateSnippetLivePreview();
            document.getElementById('modalSnippetOverlay').classList.add('show');
            setTimeout(() => {
                const el = document.getElementById('modalSnippetTitle');
                el.focus();
                el.select();
            }, 50);
        }

        function closeSnippetModal() {
            document.getElementById('modalSnippetOverlay').classList.remove('show');
        }

        function updateSnippetLivePreview() {
            const title = document.getElementById('modalSnippetTitle').value.trim();
            const text = document.getElementById('modalSnippetText').value.trim();

            document.getElementById('modalSnippetPreviewBadge').textContent = title || 'عنوان دکمه';
            document.getElementById('modalSnippetPreviewText').textContent = text || 'متن پرامپت کامل';
        }

        function saveSnippetFromModal() {
            const folderId = document.getElementById('modalSnippetFolderSelect').value || selectedFolderId;
            const title = document.getElementById('modalSnippetTitle').value.trim();
            const text = document.getElementById('modalSnippetText').value.trim();

            if (!text && !title) {
                alert('لطفاً متن پرامپت یا عنوان دکمه را وارد کنید.');
                return;
            }

            const finalTitle = title || text;
            const finalText = text || title;

            if (currentEditingSnippetId) {
                const item = dataState.Items.find(i => i.Id === currentEditingSnippetId);
                if (item) {
                    item.FolderId = folderId;
                    item.Title = finalTitle;
                    item.Text = finalText;
                }
            } else {
                const newId = 'item_' + Date.now();
                dataState.Items.push({
                    Id: newId,
                    FolderId: folderId,
                    Title: finalTitle,
                    Text: finalText,
                    Order: dataState.Items.filter(i => i.FolderId === folderId).length
                });
            }

            closeSnippetModal();
            saveDataToBackend();
            renderSnippets();
            renderCategories();
        }

        function openDeleteSnippetModal(itemId) {
            const item = dataState.Items.find(i => i.Id === itemId);
            if (!item) return;

            document.getElementById('modalConfirmTitle').textContent = 'حذف دکمه پرامپت';
            document.getElementById('modalConfirmMessage').innerHTML = `آیا از حذف دکمه پرامپت <strong style=""color:#F87171;"">«${escapeHtml(item.Title || item.Text)}»</strong> اطمینان دارید؟`;
            document.getElementById('btnModalConfirmDelete').onclick = () => {
                dataState.Items = dataState.Items.filter(i => i.Id !== itemId);
                if (dataState.ActiveItemIds) {
                    dataState.ActiveItemIds = dataState.ActiveItemIds.filter(id => id !== itemId);
                }
                closeConfirmModal();
                saveDataToBackend();
                renderSnippets();
                renderCategories();
            };
            document.getElementById('modalConfirmOverlay').classList.add('show');
        }

        function closeConfirmModal() {
            document.getElementById('modalConfirmOverlay').classList.remove('show');
        }

        function requestRefreshData() {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ type: 'getCombinerData' });
            }
        }

        function escapeHtml(str) {
            if (!str) return '';
            return String(str)
                .split('&').join('&amp;')
                .split('<').join('&lt;')
                .split('>').join('&gt;')
                .split('""').join('&quot;');
        }

        // Global Keyboard Handler for Modals
        window.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                closeSnippetModal();
                closeCategoryModal();
                closeConfirmModal();
            } else if (e.key === 'Enter') {
                if (document.getElementById('modalCategoryOverlay').classList.contains('show')) {
                    e.preventDefault();
                    saveCategoryFromModal();
                } else if (document.getElementById('modalSnippetOverlay').classList.contains('show')) {
                    if (e.ctrlKey || document.activeElement.id === 'modalSnippetTitle') {
                        e.preventDefault();
                        saveSnippetFromModal();
                    }
                }
            }
        });

        // Bridge to C#
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.addEventListener('message', event => {
                const msg = event.data;
                if (!msg) return;
                if (msg.type === 'initCombinerData') {
                    loadDataFromCSharp(msg.data);
                }
            });

            // Initial load request
            requestRefreshData();
        }
    </script>
</body>
</html>";
        }
    }
}
