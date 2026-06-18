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
    <title>🎲 10 عکس تصادفی — گالری</title>
    <link rel="stylesheet" href="css/gallery-base-style.css">
    <link rel="stylesheet" href="css/gallery-components-style.css">
    <style>
        :root {
            --bg-primary: #0f0c29;
            --bg-secondary: #1a173b;
            --text-primary: #f0f0f0;
            --accent: #8a2be2;
            --border-radius: 16px;
        }
        body {
            background: var(--bg-primary);
            color: var(--text-primary);
            font-family: 'Segoe UI', Tahoma, sans-serif;
            margin: 0;
            padding: 0;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }
        h1 {
            text-align: center;
            margin: 20px 0;
            font-weight: 700;
            color: #e0d6ff;
        }
        .controls {
            text-align: center;
            margin: 20px 0;
        }
        .refresh-btn {
            background: linear-gradient(135deg, #6a11cb 0%, #2575fc 100%);
            color: white;
            border: none;
            padding: 12px 30px;
            font-size: 18px;
            border-radius: 50px;
            cursor: pointer;
            box-shadow: 0 4px 15px rgba(106, 17, 203, 0.4);
            transition: all 0.3s ease;
        }
        .refresh-btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(106, 17, 203, 0.6);
        }

        .random-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
            gap: 20px;
            margin-top: 20px;
        }

        .random-card {
            background: var(--bg-secondary);
            border-radius: var(--border-radius);
            overflow: hidden;
            box-shadow: 0 6px 16px rgba(0,0,0,0.3);
            transition: transform 0.3s ease;
        }
        .random-card:hover {
            transform: translateY(-5px);
        }

        .random-card img {
            width: 100%;
            height: 320px;
            object-fit: contain;
            background: #000;
            display: block;
        }

        .filename-display {
            padding: 12px;
            text-align: center;
            background: rgba(0,0,0,0.2);
            font-size: 14px;
            word-break: break-all;
        }

        .edit-section {
            display: none;
            padding: 12px;
            background: rgba(0,0,0,0.2);
            text-align: center;
        }
        .edit-input {
            width: 90%;
            padding: 8px 12px;
            border: 1px solid #555;
            border-radius: 8px;
            background: #222;
            color: white;
            font-size: 14px;
            text-align: center;
            direction: ltr;
            font-family: monospace;
            margin-bottom: 8px;
        }
        .edit-btns {
            display: flex;
            justify-content: center;
            gap: 8px;
        }
        .btn {
            padding: 6px 14px;
            border: none;
            border-radius: 6px;
            font-size: 14px;
            cursor: pointer;
        }
        .btn-save {
            background: #2e7d32;
            color: white;
        }
        .btn-cancel {
            background: #555;
            color: white;
        }
        .btn-edit {
            background: transparent;
            color: var(--accent);
            padding: 4px 8px;
            font-size: 13px;
            margin-top: 4px;
        }

        /* Modal */
        #imageModal {
            display: none;
            position: fixed;
            z-index: 2000;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            background: rgba(0,0,0,0.9);
            overflow: auto;
        }
        .modal-content {
            background: var(--bg-secondary);
            margin: 30px auto;
            padding: 20px;
            border-radius: var(--border-radius);
            max-width: 900px;
            position: relative;
            animation: fadeIn 0.3s;
        }
        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(20px); }
            to { opacity: 1; transform: translateY(0); }
        }
        .close-modal {
            position: absolute;
            top: 15px;
            left: 15px;
            color: white;
            font-size: 30px;
            cursor: pointer;
            z-index: 2001;
        }
        .modal-img {
            width: 100%;
            max-height: 60vh;
            object-fit: contain;
            border-radius: 12px;
            background: black;
        }
        .prompt-box {
            margin: 15px 0;
            padding: 12px;
            background: rgba(0,0,0,0.3);
            border-radius: 10px;
        }
        .prompt-box h4 {
            margin-top: 0;
            color: #aaa;
        }
        .copy-box {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
        }
        .copy-box pre {
            flex: 1;
            margin: 0;
            white-space: pre-wrap;
            word-break: break-word;
            font-family: monospace;
            font-size: 14px;
            color: #e0e0e0;
        }
        .copy-btn {
            background: #555;
            color: white;
            border: none;
            padding: 6px 12px;
            border-radius: 6px;
            cursor: pointer;
            margin-left: 10px;
        }
        .copy-btn:hover {
            background: var(--accent);
        }

        .notification {
            position: fixed;
            top: 20px;
            left: 50%;
            transform: translateX(-50%) translateY(-100px);
            background: #4CAF50;
            color: white;
            padding: 12px 24px;
            border-radius: 30px;
            z-index: 1000;
            opacity: 0;
            transition: all 0.4s ease;
        }
        .notification.show {
            opacity: 1;
            transform: translateX(-50%) translateY(0);
        }
        .loading {
            text-align: center;
            padding: 40px;
            color: #aaa;
        }
    </style>
</head>

    <?php include 'sidebar.php'; ?>
<body>
    <div class="notification" id="notif">عملیات موفق</div>
    <div class="container">
        <h1>🎲 10 عکس تصادفی از گالری</h1>
        
        <div class="controls">
            <button class="refresh-btn" onclick="loadRandomImages()">
                🔄 بارگذاری مجدد عکس‌های تصادفی
            </button>
        </div>

        <div id="randomGrid" class="random-grid">
            <div class="loading">در حال بارگذاری...</div>
        </div>
    </div>

    <!-- Modal -->
    <div id="imageModal">
        <span class="close-modal" onclick="closeModal()">&times;</span>
        <div class="modal-content">
            <img class="modal-img" id="modalImg" src="" alt="عکس">
            <div id="modalPrompts"></div>
        </div>
    </div>

    <script>
        let currentImages = [];

        function showNotification(msg, isError = false) {
            const notif = document.getElementById('notif');
            notif.textContent = msg;
            notif.style.background = isError ? '#f44336' : '#4CAF50';
            notif.classList.add('show');
            setTimeout(() => notif.classList.remove('show'), 2500);
        }

        function loadRandomImages() {
            fetch('ajax/random-actions.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: 'action=get_random_images'
            })
            .then(res => res.json())
            .then(data => {
                if (data.error) {
                    showNotification(data.error, true);
                    return;
                }
                currentImages = data.images;
                renderImages(data.images);
            })
            .catch(err => {
                console.error(err);
                showNotification('خطا در ارتباط با سرور', true);
            });
        }

        function renderImages(images) {
            const grid = document.getElementById('randomGrid');
            if (images.length === 0) {
                grid.innerHTML = '<div class="loading">هیچ عکسی برای نمایش وجود ندارد.</div>';
                return;
            }

            let html = '';
            images.forEach(img => {
                html += `
                    <div class="random-card" data-filename="${img.filename}" data-extension="${img.extension}">
                        <img src="${img.url}" 
                             alt="${img.filename}" 
                             loading="lazy"
                             onclick="openModal('${img.filename}')">
                        <div class="filename-display">
                            ${img.filename}
                            <br>
                            <button class="btn-edit" onclick="toggleEdit(this)">
                                ✏️ ویرایش نام
                            </button>
                        </div>
                        <div class="edit-section">
                            <input type="text" class="edit-input" value="${img.basename}">
                            <div class="edit-btns">
                                <button class="btn btn-save" onclick="saveRename(this)">ذخیره</button>
                                <button class="btn btn-cancel" onclick="toggleEdit(this)">لغو</button>
                            </div>
                        </div>
                    </div>
                `;
            });
            grid.innerHTML = html;
        }

        function toggleEdit(btn) {
            const card = btn.closest('.random-card');
            const display = card.querySelector('.filename-display');
            const editSection = card.querySelector('.edit-section');
            
            if (display.style.display === 'none') {
                // بازگشت به حالت نمایش
                display.style.display = 'block';
                editSection.style.display = 'none';
            } else {
                // نمایش فیلد ویرایش
                display.style.display = 'none';
                editSection.style.display = 'block';
                editSection.querySelector('.edit-input').focus();
            }
        }

        function saveRename(btn) {
            const card = btn.closest('.random-card');
            const input = card.querySelector('.edit-input');
            const oldFilename = card.dataset.filename;
            const extension = card.dataset.extension;
            const newBasename = input.value.trim();

            if (!newBasename) {
                showNotification('نام نمی‌تواند خالی باشد!', true);
                return;
            }

            const oldBasename = oldFilename.replace(/\.[^.]+$/, '');
            if (newBasename === oldBasename) {
                toggleEdit(btn);
                return;
            }

            fetch('ajax/random-actions.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: `action=rename_file&old_filename=${encodeURIComponent(oldFilename)}&new_basename=${encodeURIComponent(newBasename)}`
            })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    // به‌روزرسانی داده‌های کارت
                    card.dataset.filename = data.new_filename;
                    card.querySelector('.filename-display').innerHTML = 
                        data.new_filename + '<br><button class="btn-edit" onclick="toggleEdit(this)">✏️ ویرایش نام</button>';
                    toggleEdit(btn);
                    showNotification('نام فایل با موفقیت تغییر کرد!');
                } else {
                    showNotification(data.error || 'خطا در تغییر نام', true);
                }
            })
            .catch(err => {
                console.error(err);
                showNotification('خطا در ارتباط', true);
            });
        }

        function openModal(filename) {
            const img = currentImages.find(i => i.filename === filename);
            if (!img) return;

            const modalImg = document.getElementById('modalImg');
            const modalPrompts = document.getElementById('modalPrompts');

            modalImg.src = img.url + '&t=' + Date.now();
            modalImg.alt = filename;

            fetch(`ajax/read-metadata.php?filename=${encodeURIComponent(filename)}`)
                .then(res => res.json())
                .then(meta => {
                    let html = '';

                    if (meta.positive && meta.positive !== '---') {
                        html += `
                            <div class="prompt-box">
                                <h4>🔹 Prompt مثبت:</h4>
                                <div class="copy-box">
                                    <pre>${meta.positive}</pre>
                                    <button class="copy-btn" onclick="copyText(this, \`${meta.positive}\`)">📋 کپی</button>
                                </div>
                            </div>
                        `;
                    }

                    if (meta.negative && meta.negative !== '---') {
                        html += `
                            <div class="prompt-box">
                                <h4>🔸 Prompt منفی:</h4>
                                <div class="copy-box">
                                    <pre>${meta.negative}</pre>
                                    <button class="copy-btn" onclick="copyText(this, \`${meta.negative}\`)">📋 کپی</button>
                                </div>
                            </div>
                        `;
                    }

                    if (meta.description) {
                        html += `
                            <div class="prompt-box">
                                <h4>📝 توضیحات:</h4>
                                <pre>${meta.description}</pre>
                            </div>
                        `;
                    }

                    modalPrompts.innerHTML = html || '<p style="text-align:center;color:#aaa;">هیچ متادیتایی یافت نشد.</p>';
                })
                .catch(() => {
                    modalPrompts.innerHTML = '<p style="text-align:center;color:#f44336;">خطا در بارگذاری متادیتا</p>';
                });

            document.getElementById('imageModal').style.display = 'block';
        }

        function closeModal() {
            document.getElementById('imageModal').style.display = 'none';
        }

        function copyText(btn, text) {
            navigator.clipboard.writeText(text).then(() => {
                const original = btn.textContent;
                btn.textContent = '✓ کپی شد!';
                setTimeout(() => {
                    btn.textContent = original;
                }, 1500);
            });
        }

        window.onclick = function(event) {
            const modal = document.getElementById('imageModal');
            if (event.target === modal) {
                closeModal();
            }
        };

        document.addEventListener('DOMContentLoaded', loadRandomImages);
    </script>
</body>
</html>