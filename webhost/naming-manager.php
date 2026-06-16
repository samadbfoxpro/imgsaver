<?php
require_once 'config.php';
if (!is_dir(GALLERY_PATH)) {
    die('<h2 style="color:red; text-align:center; direction:rtl;">❌ پوشهٔ گالری یافت نشد!</h2>');
}
?>
<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>📝 مدیریت نام‌گذاری — گالری</title>
    <link rel="stylesheet" href="css/gallery-base-style.css">
    <link rel="stylesheet" href="css/gallery-components-style.css">
      <link rel="stylesheet" href="css/style.css">

    <style>
        /* ===== فونت Benyamin ===== */
        @font-face {
            font-family: 'IRANSans';
            src: url('fonts/IRANSans.ttf') format('truetype');
            font-weight: normal;
            font-style: normal;
        }

        :root {
            --bg-primary: #0f0c29;
            --bg-secondary: #1a173b;
            --text-primary: #f0f0f0;
            --accent: #8a2be2;
            --border-radius: 12px;
        }
        body {
            background: var(--bg-primary);
            color: var(--text-primary);
            font-family: 'Benyamin', 'Segoe UI', Tahoma, sans-serif;
            margin: 0;
            padding: 0;
        }
        .container {
            max-width: 1400px;
            margin: 0 auto;
            padding: 20px;
        }
        h1 {
            text-align: center;
            margin: 20px 0;
            color: #e0d6ff;
            font-family: 'Benyamin', sans-serif;
        }
        .search-box {
            width: 100%;
            max-width: 600px;
            margin: 15px auto;
            padding: 12px 20px;
            font-size: 16px;
            background: #222;
            color: white;
            border: 1px solid #555;
            border-radius: 30px;
            text-align: center;
            direction: rtl;
            font-family: 'Benyamin', sans-serif;
        }
        .search-box:focus {
            outline: none;
            border-color: var(--accent);
        }
        .pagination {
            text-align: center;
            margin: 20px 0;
            display: flex;
            justify-content: center;
            gap: 8px;
            flex-wrap: wrap;
        }
        .pagination button {
            background: #2c264a;
            color: white;
            border: 1px solid #555;
            padding: 6px 12px;
            border-radius: 6px;
            cursor: pointer;
        }
        .pagination button.active {
            background: var(--accent);
            border-color: var(--accent);
        }
        .files-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
            gap: 20px;
            margin-top: 20px;
        }
        .file-card {
            background: var(--bg-secondary);
            border-radius: var(--border-radius);
            overflow: hidden;
            box-shadow: 0 4px 10px rgba(0,0,0,0.3);
            display: flex;
            flex-direction: column;
            height: 100%;
        }
        .file-card.confirmed {
            border: 2px solid #4CAF50;
        }
        .thumb {
            width: 100%;
            height: 280px;
            object-fit: contain;
            background: #000;
            display: block;
        }
        .filename-display {
            padding: 10px;
            font-size: 14px;
            text-align: center;
            word-break: break-word;
            font-family: 'Benyamin', monospace;
            line-height: 1.4;
            flex-grow: 1;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        .actions {
            padding: 8px;
            display: flex;
            justify-content: center;
            gap: 6px;
            border-top: 1px solid rgba(255,255,255,0.1);
        }
        .btn {
            padding: 5px 10px;
            font-size: 12px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
        }
        .btn-edit {
            background: #1976d2;
            color: white;
        }
        .btn-confirm {
            background: #4CAF50;
            color: white;
        }
        .btn-confirm.active {
            background: #f44336;
        }

        /* ===== مودال ویرایش ===== */
        #editModal {
            display: none;
            position: fixed;
            z-index: 3000;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            background: rgba(0,0,0,0.85);
            overflow: auto;
        }
        .modal-content {
            background: var(--bg-secondary);
            margin: 50px auto;
            padding: 30px;
            border-radius: 20px;
            max-width: 600px;
            position: relative;
            animation: modalFadeIn 0.4s;
            box-shadow: 0 10px 30px rgba(0,0,0,0.5);
        }
        @keyframes modalFadeIn {
            from { opacity: 0; transform: scale(0.9); }
            to { opacity: 1; transform: scale(1); }
        }
        .close-modal {
            position: absolute;
            top: 15px;
            left: 15px;
            color: #aaa;
            font-size: 28px;
            cursor: pointer;
            z-index: 3001;
        }
        .modal-content h2 {
            text-align: center;
            margin-top: 0;
            color: #e0d6ff;
            font-family: 'Benyamin', sans-serif;
        }
        .modal-thumb {
            width: 100%;
            max-height: 300px;
            object-fit: contain;
            background: black;
            border-radius: 12px;
            margin: 15px 0;
            display: block;
        }
        .current-name {
            font-size: 16px;
            text-align: center;
            padding: 10px;
            background: rgba(0,0,0,0.3);
            border-radius: 8px;
            margin: 10px 0;
            word-break: break-all;
            font-family: 'Benyamin', monospace;
        }
        .edit-input {
            width: 100%;
            padding: 12px;
            font-size: 16px;
            background: #222;
            color: white;
            border: 1px solid #555;
            border-radius: 8px;
            text-align: center;
            direction: ltr;
            font-family: 'Benyamin', monospace;
            margin: 15px 0;
        }
        .edit-input:focus {
            outline: none;
            border-color: var(--accent);
        }
        .modal-btns {
            display: flex;
            justify-content: center;
            gap: 12px;
            margin-top: 15px;
        }
        .modal-btn {
            padding: 10px 20px;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            font-size: 16px;
        }
        .modal-btn-save {
            background: #2e7d32;
            color: white;
        }
        .modal-btn-cancel {
            background: #555;
            color: white;
        }

        .notification {
            position: fixed;
            top: 20px;
            left: 50%;
            transform: translateX(-50%) translateY(-100px);
            background: #4CAF50;
            color: white;
            padding: 10px 20px;
            border-radius: 20px;
            z-index: 1000;
            opacity: 0;
            transition: all 0.4s ease;
        }
        .notification.show {
            opacity: 1;
            transform: translateX(-50%) translateY(0);
        }
        .stats {
            text-align: center;
            margin: 15px 0;
            color: #aaa;
            font-size: 14px;
        }
    </style>
</head>

    <?php include 'sidebar.php'; ?>
<body>
    <div class="notification" id="notif">عملیات موفق</div>
    <div class="container">
        <h1>📝 مدیریت نام‌گذاری فایل‌ها</h1>
        
        <input type="text" class="search-box" id="searchBox" placeholder="جستجو در نام فایل یا متادیتا (با فاصله: همه کلمات)" oninput="handleSearchInput()">
        
        <div class="stats" id="stats">در حال بارگذاری...</div>

        <div class="pagination" id="pagination"></div>

        <div class="files-grid" id="filesGrid">
            <div style="text-align:center; padding:40px; color:#aaa;">در حال بارگذاری...</div>
        </div>

        <div class="pagination" id="pagination2"></div>
    </div>

    <!-- مودال ویرایش نام -->
    <div id="editModal">
        <div class="modal-content">
            <span class="close-modal" onclick="closeEditModal()">&times;</span>
            <h2>ویرایش نام فایل</h2>
            <img class="modal-thumb" id="modalThumb" src="" alt="عکس">
            <div class="current-name" id="currentNameDisplay">نام فعلی</div>
            <input type="text" class="edit-input" id="editInput" placeholder="نام جدید را وارد کنید..." onkeydown="if(event.key==='Enter') saveRenameFromModal()">
            <div class="modal-btns">
                <button class="modal-btn modal-btn-save" onclick="saveRenameFromModal()">ذخیره</button>
                <button class="modal-btn modal-btn-cancel" onclick="closeEditModal()">لغو</button>
            </div>
        </div>
    </div>

    <script>
        let currentPage = 1;
        let totalPages = 1;
        let currentFileToEdit = null;
        let currentSearchQuery = '';

        function showNotification(msg, isError = false) {
            const notif = document.getElementById('notif');
            notif.textContent = msg;
            notif.style.background = isError ? '#f44336' : '#4CAF50';
            notif.classList.add('show');
            setTimeout(() => notif.classList.remove('show'), 2500);
        }

        function handleSearchInput() {
            clearTimeout(this.searchTimeout);
            this.searchTimeout = setTimeout(() => {
                const query = document.getElementById('searchBox').value.trim();
                if (query === currentSearchQuery) return;
                currentSearchQuery = query;
                loadFiles(1, query);
            }, 500);
        }

        function loadFiles(page = 1, search = '') {
            fetch('ajax/naming-actions.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: `action=get_files&page=${page}&search=${encodeURIComponent(search)}`
            })
            .then(res => res.json())
            .then(data => {
                if (data.error) {
                    showNotification(data.error, true);
                    return;
                }

                currentPage = data.page;
                totalPages = data.pages;
                renderFiles(data.files);
                renderPagination();
                updateStats(data.total, search);
            })
            .catch(err => {
                console.error(err);
                showNotification('خطا در بارگذاری', true);
            });
        }

        function updateStats(total, search = '') {
            let msg = `کل فایل‌ها: ${total}`;
            if (search) {
                msg = `نتایج جستجو برای "${search}": ${total}`;
            } else {
                fetch('ajax/naming-actions.php', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: 'action=get_files&page=1'
                })
                .then(res => res.json())
                .then(data => {
                    const confirmed = data.files.filter(f => f.confirmed).length;
                    document.getElementById('stats').textContent = 
                        `کل فایل‌ها: ${data.total} — تأییدشده: ${confirmed} — باقی‌مانده: ${data.total - confirmed}`;
                });
                return;
            }
            document.getElementById('stats').textContent = msg;
        }

        function renderFiles(files) {
            const grid = document.getElementById('filesGrid');
            if (files.length === 0) {
                grid.innerHTML = '<div style="text-align:center; padding:40px; color:#aaa;">هیچ نتیجه‌ای یافت نشد.</div>';
                return;
            }

            let html = '';
            files.forEach(file => {
                const confirmedClass = file.confirmed ? 'confirmed' : '';
                html += `
                    <div class="file-card ${confirmedClass}" data-filename="${file.filename}">
                        <img class="thumb" src="image-proxy.php?img=${encodeURIComponent(GALLERY_PATH + file.filename)}" alt="${file.filename}">
                        <div class="filename-display">${file.filename}</div>
                        <div class="actions">
                            <button class="btn btn-edit" onclick="openEditModal('${file.filename}')">ویرایش</button>
                            <button class="btn btn-confirm ${file.confirmed ? 'active' : ''}" 
                                    onclick="toggleConfirm('${file.filename}', this)">
                                ${file.confirmed ? '❌ لغو' : '✅ تأیید'}
                            </button>
                        </div>
                    </div>
                `;
            });
            grid.innerHTML = html;
        }

        function renderPagination() {
            if (totalPages <= 1) {
                document.getElementById('pagination').innerHTML = '';
                document.getElementById('pagination2').innerHTML = '';
                return;
            }

            const render = (containerId) => {
                let html = '';
                const start = Math.max(1, currentPage - 2);
                const end = Math.min(totalPages, currentPage + 2);

                if (currentPage > 1) {
                    html += `<button onclick="loadFiles(${currentPage - 1}, currentSearchQuery)">قبلی</button>`;
                }

                for (let i = start; i <= end; i++) {
                    html += `<button class="${i === currentPage ? 'active' : ''}" onclick="loadFiles(${i}, currentSearchQuery)">${i}</button>`;
                }

                if (currentPage < totalPages) {
                    html += `<button onclick="loadFiles(${currentPage + 1}, currentSearchQuery)">بعدی</button>`;
                }

                document.getElementById(containerId).innerHTML = html;
            };

            render('pagination');
            render('pagination2');
        }

        function openEditModal(filename) {
            currentFileToEdit = filename;
            const basename = filename.replace(/\.[^.]+$/, '');
            const ext = filename.split('.').pop();

            document.getElementById('modalThumb').src = 'image-proxy.php?img=' + encodeURIComponent(GALLERY_PATH + filename) + '&t=' + Date.now();
            document.getElementById('currentNameDisplay').textContent = filename;
            document.getElementById('editInput').value = basename;
            document.getElementById('editInput').focus();
            document.getElementById('editModal').style.display = 'block';
        }

        function closeEditModal() {
            document.getElementById('editModal').style.display = 'none';
            currentFileToEdit = null;
        }

        function saveRenameFromModal() {
            const newBasename = document.getElementById('editInput').value.trim();
            if (!newBasename) {
                showNotification('نام نمی‌تواند خالی باشد!', true);
                return;
            }

            const oldFilename = currentFileToEdit;
            const oldBasename = oldFilename.replace(/\.[^.]+$/, '');
            if (newBasename === oldBasename) {
                closeEditModal();
                return;
            }

            fetch('ajax/naming-actions.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: `action=rename_file&old_filename=${encodeURIComponent(oldFilename)}&new_basename=${encodeURIComponent(newBasename)}`
            })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    closeEditModal();
                    showNotification('نام با موفقیت تغییر کرد!');
                    loadFiles(currentPage, currentSearchQuery); // رفرش صفحه جاری + جستجو
                } else {
                    showNotification(data.error || 'خطا در تغییر نام', true);
                }
            })
            .catch(err => {
                console.error(err);
                showNotification('خطا در ارتباط', true);
            });
        }

        function toggleConfirm(filename, btn) {
            fetch('ajax/naming-actions.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: `action=toggle_confirm&filename=${encodeURIComponent(filename)}`
            })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    const card = btn.closest('.file-card');
                    if (data.confirmed) {
                        card.classList.add('confirmed');
                        btn.textContent = '❌ لغو';
                        btn.classList.add('active');
                    } else {
                        card.classList.remove('confirmed');
                        btn.textContent = '✅ تأیید';
                        btn.classList.remove('active');
                    }
                    updateStats(document.getElementById('stats').textContent.split(' ')[2]); // تخمینی
                } else {
                    showNotification(data.error || 'خطا', true);
                }
            })
            .catch(err => {
                console.error(err);
                showNotification('خطا در ارتباط', true);
            });
        }

        // بستن مودال با کلیک روی پس‌زمینه
        window.onclick = function(event) {
            const modal = document.getElementById('editModal');
            if (event.target === modal) {
                closeEditModal();
            }
        };

        // شروع
        document.addEventListener('DOMContentLoaded', () => {
            loadFiles();
        });
    </script>
    <script>
        // تعریف GALLERY_PATH برای جاوااسکریپت
        const GALLERY_PATH = <?= json_encode(GALLERY_PATH) ?>;
    </script>
</body>
</html>