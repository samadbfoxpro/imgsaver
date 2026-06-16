<?php include __DIR__ . '/includes/zip-manager-logic.php'; ?>
<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>مدیریت فایل‌های فشرده temp-zip</title>
    <link rel="stylesheet" href="css/zip-manager-style.css">
    
    <link rel="stylesheet" href="css/gallery-base-style.css">
    <link rel="stylesheet" href="css/gallery-back-button-style.css">
    <link rel="stylesheet" href="css/gallery-components-style.css">
</head>
<body>
    <!-- منوی عمودی گالری -->
    <?php include 'sidebar.php'; ?>


    <div class="container">
        <form method="post" style="margin-bottom: 18px; text-align:center;">
            <button type="submit" name="action" value="delete_all" class="icon-btn delete-btn" style="background:#ff4d4f;color:#fff;font-size:1.1em;padding:10px 32px;border-radius:10px;box-shadow:0 2px 8px #ff4d4f33;min-width:180px;max-width:100%;margin-bottom:10px;">
                Delete all 🗑️   
            </button>
        </form>
        <h1>مدیریت فایل‌های فشرده temp-zip</h1>
        <?php if (empty($files)): ?>
            <div class="no-files">هیچ فایل فشرده‌ای در پوشه temp-zip وجود ندارد.</div>
        <?php else: ?>
            <?php foreach ($files as $file): ?>
                <div class="file-row">
                    <div class="file-row-top">
                        <span class="file-name-mobile"><?= htmlspecialchars($file) ?></span>
                    </div>
                    <div class="file-actions-col">
                        <a href="temp-zip/<?= htmlspecialchars($file) ?>" download class="icon-btn download-btn" title="دانلود فایل">⬇️</a>
                        <form method="post" class="rename-form" style="display:inline;">
                            <input type="hidden" name="filename" value="<?= htmlspecialchars($file) ?>">
                            <input type="hidden" name="newname" value="">
                            <button type="button" class="icon-btn rename-btn" title="تغییر نام" onclick="openRenameDialog(this, '<?= htmlspecialchars($file) ?>')">✏️</button>
                        </form>
                        <form method="post" style="display:inline;">
                            <input type="hidden" name="filename" value="<?= htmlspecialchars($file) ?>">
                            <button type="submit" name="action" value="delete" class="icon-btn delete-btn" title="حذف" onclick="return confirm('حذف این فایل؟')">🗑️</button>

                        </form>
                    </div>
                </div>
            <?php endforeach; ?>
        <?php endif; ?>
    </div>
    <!-- Modal Rename Dialog -->
    <div id="renameModal" class="modal-bg" style="display:none;">
      <div class="modal-content">
        <h2>تغییر نام فایل</h2>
        <form id="modalRenameForm">
          <input type="text" id="modalNewName" autocomplete="off" maxlength="60" required>
          <span class="modal-ext">.zip</span>
          <input type="hidden" id="modalOldName">
          <div class="modal-actions">
            <button type="submit">تأیید</button>
            <button type="button" onclick="closeRenameModal()">انصراف</button>
          </div>
        </form>
        <div id="modalError" class="modal-error"></div>
      </div>
    </div>
    <script src="js/zip-manager.js"></script>
</body>
</html>
