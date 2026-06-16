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
    <title>🔧 مدیریت کامل گالری</title>
    <link rel="stylesheet" href="css/style.css">
    <style>
        /* اضافه کردن حالت انتخاب شده */
        .file-card.selected {
            border: 3px solid #f44336;
            box-shadow: 0 0 15px rgba(244, 67, 54, 0.5);
        }
        .select-checkbox {
            position: absolute;
            top: 10px;
            left: 10px;
            z-index: 10;
            display: none; /* پیش‌فرض مخفی */
        }
        .selection-active .select-checkbox {
            display: block; /* وقتی حالت انتخاب فعال باشه نشون بده */
        }
        .toggle-select-btn {
            background: #555;
            color: white;
            border: none;
            padding: 8px 16px;
            border-radius: 6px;
            cursor: pointer;
            font-family: 'IRANSans', sans-serif;
            margin: 0 10px 20px 0;
        }
        .toggle-select-btn.active {
            background: #f44336;
        }
        .bulk-actions {
            text-align: center;
            margin: 20px 0;
        }
        .bulk-delete-btn {
            background: #f44336;
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 6px;
            cursor: pointer;
            font-family: 'IRANSans', sans-serif;
        }
        .bulk-delete-btn:disabled {
            background: #555;
            cursor: not-allowed;
        }
    </style>
</head>

    <?php include 'sidebar.php'; ?>
<body>
    <div class="notification" id="notif">عملیات موفق</div>
    <div class="container">
        <h1>🔧 مدیریت کامل گالری</h1>
        
        <input type="text" class="search-box" id="searchBox" placeholder="جستجو در نام فایل یا متادیتا (با فاصله: همه کلمات)" oninput="handleSearchInput()">

        <div style="text-align: center; margin: 10px 0;">
            <button class="toggle-select-btn" id="toggleSelectBtn" onclick="toggleSelectionMode()">✅ فعال‌سازی انتخاب</button>
            <button class="bulk-delete-btn" id="bulkDeleteBtn" onclick="deleteSelected()" disabled>🗑️ حذف انتخاب‌شده‌ها</button>
        </div>
        
        <div class="pagination" id="paginationTop"></div>

        <div class="files-grid" id="filesGrid">
            <div class="loading">در حال بارگذاری...</div>
        </div>

        <div class="pagination" id="paginationBottom"></div>
    </div>

    <!-- Modal ویرایش -->
    <div id="editModal">
        <div class="modal-content">
            <span class="close-modal" onclick="closeEditModal()">&times;</span>
            <img class="modal-img" id="modalImg" src="" alt="عکس">
            <div class="modal-form">
                <label>Positive Prompt:</label>
                <textarea id="editPositive" placeholder="مثلاً a beautiful landscape..."></textarea>

                <label>Negative Prompt:</label>
                <textarea id="editNegative" placeholder="مثلاً blurry, low quality..."></textarea>

                <label>Description (اختیاری):</label>
                <textarea id="editDescription" placeholder="توضیحات..."></textarea>
            </div>
            <div class="modal-btns">
                <button class="modal-btn modal-btn-cancel" onclick="closeEditModal()">لغو</button>
                <button class="modal-btn modal-btn-save" onclick="saveEdit()">ذخیره</button>
            </div>
        </div>
    </div>

    <script>
        let currentPage = 1;
        let totalPages = 1;
        let currentFileToEdit = null;
        let currentSearchQuery = '';
        let selectedFiles = new Set();
        let isSelectionMode = false; // حالت انتخاب فعال/غیرفعال
        const LIMIT_PER_PAGE = 8; // تعداد فایل در هر صفحه

        function showNotification(msg, isError = false) {
            const notif = document.getElementById('notif');
            notif.textContent = msg;
            notif.style.background = isError ? '#f44336' : '#4CAF50';
            notif.classList.add('show');
            setTimeout(() => notif.classList.remove('show'), 2500);
        }

        function toggleSelectionMode() {
            isSelectionMode = !isSelectionMode;
            const btn = document.getElementById('toggleSelectBtn');
            const grid = document.getElementById('filesGrid');
            if (isSelectionMode) {
                btn.textContent = '❌ غیرفعال‌سازی انتخاب';
                btn.classList.add('active');
                grid.classList.add('selection-active');
            } else {
                btn.textContent = '✅ فعال‌سازی انتخاب';
                btn.classList.remove('active');
                grid.classList.remove('selection-active');
            }
            updateBulkDeleteButton();
        }

        function handleSearchInput() {
            clearTimeout(this.searchTimeout);
            this.searchTimeout = setTimeout(() => {
                const query = document.getElementById('searchBox').value.trim();
                if (query === currentSearchQuery) return;
                currentSearchQuery = query;
                loadFiles(1, query); // وقتی جستجو می‌کنیم، به صفحه اول برو
            }, 500);
        }

        function loadFiles(page = 1, search = '') {
            fetch('ajax/manage-actions.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: `action=get_files&page=${page}&limit=${LIMIT_PER_PAGE}&search=${encodeURIComponent(search)}`
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
            })
            .catch(err => {
                console.error(err);
                showNotification('خطا در بارگذاری', true);
            });
        }

        function renderFiles(files) {
            const grid = document.getElementById('filesGrid');
            if (files.length === 0) {
                grid.innerHTML = '<div class="loading">هیچ فایلی یافت نشد.</div>';
                return;
            }

            let html = '';
            files.forEach(file => {
                const isSelected = selectedFiles.has(file.filename) ? 'selected' : '';
                html += `
                    <div class="file-card ${isSelected}" data-filename="${file.filename}" onclick="handleCardClick(this)">
                        <input type="checkbox" class="select-checkbox" onchange="toggleSelect(this)" ${isSelected ? 'checked' : ''}>
                        <img class="thumb" src="image-proxy.php?img=${encodeURIComponent(GALLERY_PATH + file.filename)}" alt="${file.filename}">
                        <div class="file-info">
                            <strong>${file.filename}</strong>
                            <small>آخرین تغییر: ${file.mtime}</small>
                            <small>P: ${file.positive.substring(0, 40)}${file.positive.length > 40 ? '...' : ''}</small>
                            <small>N: ${file.negative.substring(0, 40)}${file.negative.length > 40 ? '...' : ''}</small>
                        </div>
                        <div class="file-actions">
                            <button class="btn btn-edit" onclick="event.stopPropagation(); openEditModal('${file.filename}', \`${file.positive}\`, \`${file.negative}\`, \`${file.description}\`)">ویرایش</button>
                            <button class="btn btn-delete" onclick="event.stopPropagation(); deleteFile('${file.filename}')">حذف</button>
                        </div>
                    </div>
                `;
            });
            grid.innerHTML = html;
            updateBulkDeleteButton();
        }

        function handleCardClick(card) {
            if (!isSelectionMode) {
                return;
            }
            const checkbox = card.querySelector('.select-checkbox');
            checkbox.checked = !checkbox.checked;
            toggleSelect(checkbox);
        }

        function toggleSelect(checkbox) {
            const card = checkbox.closest('.file-card');
            const filename = card.dataset.filename;

            if (checkbox.checked) {
                selectedFiles.add(filename);
                card.classList.add('selected');
            } else {
                selectedFiles.delete(filename);
                card.classList.remove('selected');
            }
            updateBulkDeleteButton();
        }

        function updateBulkDeleteButton() {
            const btn = document.getElementById('bulkDeleteBtn');
            btn.disabled = selectedFiles.size === 0;
        }

        function deleteSelected() {
            if (selectedFiles.size === 0) return;
            if (!confirm(`آیا از حذف ${selectedFiles.size} فایل اطمینان دارید؟`)) return;

            const promises = [];
            for (const filename of selectedFiles) {
                promises.push(
                    fetch('ajax/manage-actions.php', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                        body: `action=delete_file&filename=${encodeURIComponent(filename)}`
                    })
                    .then(res => res.json())
                );
            }

            Promise.all(promises)
            .then(results => {
                let successCount = 0;
                let errorCount = 0;
                results.forEach(res => {
                    if (res.success) successCount++;
                    else errorCount++;
                });

                if (errorCount > 0) {
                    showNotification(`موفق: ${successCount}، خطا: ${errorCount}`, errorCount > 0);
                } else {
                    showNotification(`${successCount} فایل با موفقیت حذف شدند!`);
                }

                selectedFiles.clear();
                loadFiles(currentPage, currentSearchQuery);
            })
            .catch(err => {
                console.error(err);
                showNotification('خطا در ارتباط', true);
            });
        }

        function renderPagination() {
            if (totalPages <= 1) {
                document.getElementById('paginationTop').innerHTML = '';
                document.getElementById('paginationBottom').innerHTML = '';
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

            render('paginationTop');
            render('paginationBottom');
        }

        function openEditModal(filename, positive, negative, description) {
            currentFileToEdit = filename;
            document.getElementById('modalImg').src = 'image-proxy.php?img=' + encodeURIComponent(GALLERY_PATH + filename) + '&t=' + Date.now();
            document.getElementById('editPositive').value = positive;
            document.getElementById('editNegative').value = negative;
            document.getElementById('editDescription').value = description;
            document.getElementById('editModal').style.display = 'block';
        }

        function closeEditModal() {
            document.getElementById('editModal').style.display = 'none';
            currentFileToEdit = null;
        }

        function saveEdit() {
            const positive = document.getElementById('editPositive').value;
            const negative = document.getElementById('editNegative').value;
            const description = document.getElementById('editDescription').value;

            fetch('ajax/manage-actions.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: `action=edit_txt&filename=${encodeURIComponent(currentFileToEdit)}&positive=${encodeURIComponent(positive)}&negative=${encodeURIComponent(negative)}&description=${encodeURIComponent(description)}`
            })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    closeEditModal();
                    showNotification('فایل متنی با موفقیت ذخیره شد!');
                    loadFiles(currentPage, currentSearchQuery);
                } else {
                    showNotification(data.error || 'خطا در ذخیره', true);
                }
            })
            .catch(err => {
                console.error(err);
                showNotification('خطا در ارتباط', true);
            });
        }

        function deleteFile(filename) {
            if (!confirm('آیا از حذف این فایل اطمینان دارید؟')) return;

            fetch('ajax/manage-actions.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: `action=delete_file&filename=${encodeURIComponent(filename)}`
            })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    showNotification('فایل با موفقیت حذف شد!');
                    loadFiles(currentPage, currentSearchQuery);
                } else {
                    showNotification(data.error || 'خطا در حذف', true);
                }
            })
            .catch(err => {
                console.error(err);
                showNotification('خطا در ارتباط', true);
            });
        }

        window.onclick = function(event) {
            const modal = document.getElementById('editModal');
            if (event.target === modal) {
                closeEditModal();
            }
        };

        document.addEventListener('DOMContentLoaded', () => {
            loadFiles();
        });
    </script>
    <script>
        const GALLERY_PATH = <?= json_encode(GALLERY_PATH) ?>;
    </script>
</body>
</html>