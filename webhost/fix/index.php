<?php
$projectDir = __DIR__;
$staticDir = $projectDir . DIRECTORY_SEPARATOR . 'static';
$format1Dir = $projectDir . DIRECTORY_SEPARATOR . 'format1';
$unorganizedDir = $projectDir . DIRECTORY_SEPARATOR . 'unorganized';

foreach ([$staticDir, $format1Dir, $unorganizedDir] as $dir) {
    if (!is_dir($dir)) {
        mkdir($dir, 0755, true);
    }
}

$uploadMessage = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_FILES['text_files'])) {
    $files = $_FILES['text_files'];
    $count = is_array($files['name']) ? count($files['name']) : 1;
    $uploaded = 0;

    for ($i = 0; $i < $count; $i++) {
        $name = is_array($files['name']) ? $files['name'][$i] : $files['name'];
        $tmp = is_array($files['tmp_name']) ? $files['tmp_name'][$i] : $files['tmp_name'];
        $error = is_array($files['error']) ? $files['error'][$i] : $files['error'];

        if ($error !== UPLOAD_ERR_OK) continue;

        $ext = strtolower(pathinfo($name, PATHINFO_EXTENSION));
        if (!in_array($ext, ['txt', 'text'])) continue;

        $safeName = preg_replace('/[\/\\\\:*?"<>|]+/', '_', $name);
        $safeName = trim($safeName, '_');
        if ($safeName === '') {
            $safeName = 'unnamed_file';
        }
        if ($ext !== '') {
            $safeName = pathinfo($safeName, PATHINFO_FILENAME) . '.' . $ext;
        }

        $target = $staticDir . DIRECTORY_SEPARATOR . $safeName;

        $counter = 1;
        $baseName = pathinfo($safeName, PATHINFO_FILENAME);
        while (file_exists($target)) {
            $target = $staticDir . DIRECTORY_SEPARATOR . $baseName . '_' . $counter . '.' . $ext;
            $counter++;
        }

        if (move_uploaded_file($tmp, $target)) {
            $uploaded++;
        }
    }

    $uploadMessage = $uploaded > 0 
        ? "<p style='color:#4caf50;'>✅ $uploaded فایل با موفقیت آپلود شد.</p>"
        : "<p style='color:#f44336;'>❌ هیچ فایل معتبری آپلود نشد.</p>";
}

function findTextFiles($dir) {
    $result = [];
    if (!is_dir($dir)) return $result;
    $iterator = new RecursiveIteratorIterator(
        new RecursiveDirectoryIterator($dir, RecursiveDirectoryIterator::SKIP_DOTS)
    );
    foreach ($iterator as $file) {
        if ($file->isDir()) continue;
        $ext = strtolower($file->getExtension());
        if ($ext === 'txt' || $ext === 'text') {
            $result[] = [
                'name' => $file->getFilename(),
                'path' => $file->getPathname(),
                'size' => $file->getSize(),
                'modified' => date('Y-m-d H:i', $file->getMTime())
            ];
        }
    }
    return $result;
}

// تابع isFormat1 دیگر استفاده نمی‌شود، اما برای احتیاط حذف نشده — می‌توانید آن را پاک کنید

if (isset($_POST['delete_all'])) {
    foreach ([$staticDir, $format1Dir, $unorganizedDir] as $dir) {
        if (!is_dir($dir)) continue;
        $files = findTextFiles($dir);
        foreach ($files as $file) {
            unlink($file['path']);
        }
    }
} elseif (isset($_POST['update'])) {
    // ✅ همه فایل‌های static مستقیماً به unorganized منتقل شوند — بدون شرط
    $staticFiles = findTextFiles($staticDir);
    foreach ($staticFiles as $file) {
        $target = $unorganizedDir . DIRECTORY_SEPARATOR . $file['name'];

        if (file_exists($target)) {
            unlink($target);
        }

        rename($file['path'], $target);
    }
}

$staticFiles = findTextFiles($staticDir);
$format1Files = findTextFiles($format1Dir);
$unorganizedFiles = findTextFiles($unorganizedDir);

$mainFiles = [];
$iterator = new RecursiveIteratorIterator(
    new RecursiveDirectoryIterator($projectDir, RecursiveDirectoryIterator::SKIP_DOTS)
);
foreach ($iterator as $file) {
    if ($file->isDir()) continue;
    $ext = strtolower($file->getExtension());
    if (($ext === 'txt' || $ext === 'text')) {
        $path = $file->getPathname();
        if (strpos($path, $staticDir . DIRECTORY_SEPARATOR) === false &&
            strpos($path, $format1Dir . DIRECTORY_SEPARATOR) === false &&
            strpos($path, $unorganizedDir . DIRECTORY_SEPARATOR) === false) {
            $mainFiles[] = [
                'name' => $file->getFilename(),
                'path' => $path,
                'size' => $file->getSize(),
                'modified' => date('Y-m-d H:i', $file->getMTime()),
                'location' => 'main folder'
            ];
        }
    }
}

$files = array_merge(
    $mainFiles,
    array_map(fn($f) => array_merge($f, ['location' => 'static']), $staticFiles),
    array_map(fn($f) => array_merge($f, ['location' => 'format1']), $format1Files),
    array_map(fn($f) => array_merge($f, ['location' => 'unorganized']), $unorganizedFiles)
);
?>
<!DOCTYPE html>
<html lang="fa">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>مدیریت فایل‌های متنی</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: Tahoma, Arial, sans-serif; background: #121212; color: #e0e0e0; direction: rtl; }
        .container { width: 90%; max-width: 1200px; margin: 20px auto; background: #1e1e1e; padding: 20px; border-radius: 12px; box-shadow: 0 4px 16px rgba(0,0,0,0.5); }
        .header { text-align: center; margin-bottom: 20px; }
        .nav { display: flex; flex-wrap: wrap; justify-content: center; gap: 15px; margin-bottom: 20px; padding: 10px; }
        .nav a { color: #64b5f6; text-decoration: none; padding: 8px 16px; border-radius: 6px; background: #2d2d2d; transition: background 0.3s; }
        .nav a:hover { background: #3d3d3d; }
        .btn-group { display: flex; flex-wrap: wrap; justify-content: center; gap: 10px; margin: 15px 0; }
        .update-btn { padding: 10px 20px; font-size: 14px; background: #43a047; color: white; border: none; border-radius: 6px; cursor: pointer; transition: background 0.3s; }
        .update-btn:hover { background: #2e7d32; }
        .delete-btn { background: #d32f2f; }
        .delete-btn:hover { background: #b71c1c; }

        .upload-section {
            background: #252525; padding: 20px; border-radius: 10px; margin-bottom: 25px;
            border: 2px dashed #37474f;
        }
        .upload-section h2 {
            margin-bottom: 15px; color: #81d4fa;
        }
        .upload-form {
            display: flex; flex-wrap: wrap; gap: 15px; align-items: end;
        }
        .file-input-wrapper {
            flex: 1; min-width: 250px;
        }
        .file-input-wrapper input[type="file"] {
            width: 100%; padding: 10px; border-radius: 6px; border: 1px solid #444;
            background: #333; color: white;
        }
        .upload-btn {
            padding: 10px 20px; background: #0288d1; color: white; border: none;
            border-radius: 6px; cursor: pointer; font-size: 14px;
        }
        .upload-btn:hover { background: #0277bd; }
        .upload-message { margin-top: 10px; }
        .file-summary {
            margin-top: 10px;
            padding: 8px;
            background: #2c2c2c;
            border-radius: 6px;
            font-size: 13px;
            color: #bb86fc;
        }
        .section { margin: 20px 0; }
        .section h2 { color: #90a4ae; margin: 15px 0; padding-bottom: 8px; border-bottom: 1px solid #333; }
        .file-list { list-style: none; padding: 0; }
        .file-item { padding: 10px; margin: 8px 0; background: #2b2b2b; border-radius: 6px; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 10px; }
        .file-info { display: flex; align-items: center; gap: 10px; flex: 1; min-width: 200px; }
        .file-icon { color: #64b5f6; }
        .file-name { font-weight: bold; }
        .file-size { color: #9e9e9e; font-size: 12px; }
        @media (max-width: 768px) {
            .file-item { flex-direction: column; align-items: stretch; }
            .btn-group { flex-direction: column; align-items: center; }
            .update-btn { width: 100%; max-width: 250px; }
        }
    </style>
</head>
<body>
<?php include 'sidebar.php'; ?>
<div class="container">
    <div class="header">
        <h1>مدیریت فایل‌های متنی</h1>
    </div>

    <div class="upload-section">
        <h2>آپلود فایل‌های متنی به پوشه static</h2>
        <form id="uploadForm" method="post" enctype="multipart/form-data" class="upload-form">
            <div class="file-input-wrapper">
                <input type="file" id="fileInput" name="text_files[]" multiple webkitdirectory directory accept=".txt,.text">
                <small>فقط فایل‌های .txt و .text پردازش می‌شوند.</small>
                <div id="fileSummary" class="file-summary" style="display:none;"></div>
            </div>
            <button type="submit" class="upload-btn" id="uploadBtn" disabled>آپلود فایل‌ها</button>
        </form>
        <?php if (!empty($uploadMessage)): ?>
            <div class="upload-message"><?php echo $uploadMessage; ?></div>
        <?php endif; ?>
    </div>

    <div class="nav">
        <?php $base = dirname($_SERVER['SCRIPT_NAME']); ?>
        <a href="index.php">صفحه اصلی</a>
        <a href="/fix/remove_prompt.php">حذف Prompt</a>
        <a href="/fix/check_format2.php">فرمت جدید</a>
    </div>

    <div class="btn-group">
        <form method="post">
            <button type="submit" name="update" class="update-btn">بروزرسانی و دسته‌بندی</button>
        </form>
        <form method="post">
            <button type="submit" name="delete_all" class="update-btn delete-btn">حذف همه فایل‌ها</button>
        </form>
    </div>

    <?php
    $sections = [
        'main folder' => 'فایل‌های پوشه اصلی (غیرمدیریت‌شده)',
        'static' => 'فایل‌های static (ورودی شما)',
        'format1' => 'فایل‌های format1 (فرمت صحیح)',
        'unorganized' => 'فایل‌های unorganized (فرمت نامعتبر)'
    ];
    foreach ($sections as $loc => $title): ?>
    <div class="section">
        <h2><?php echo htmlspecialchars($title); ?></h2>
        <ul class="file-list">
            <?php foreach ($files as $file): ?>
                <?php if (($file['location'] ?? '') === $loc): ?>
                <li class="file-item">
                    <div class="file-info">
                        <span class="file-icon">📄</span>
                        <span class="file-name"><?php echo htmlspecialchars($file['name']); ?></span>
                        <span class="file-size">(<?php echo number_format($file['size']); ?> بایت، <?php echo $file['modified']; ?>)</span>
                    </div>
                </li>
                <?php endif; ?>
            <?php endforeach; ?>
        </ul>
    </div>
    <?php endforeach; ?>
</div>

<script>
document.getElementById('fileInput').addEventListener('change', function(e) {
    const files = Array.from(e.target.files);
    const validFiles = files.filter(file => {
        const ext = file.name.split('.').pop().toLowerCase();
        return ext === 'txt' || ext === 'text';
    });

    const totalSize = validFiles.reduce((sum, file) => sum + file.size, 0);
    const sizeMB = (totalSize / (1024 * 1024)).toFixed(2);

    const summary = document.getElementById('fileSummary');
    const uploadBtn = document.getElementById('uploadBtn');

    if (validFiles.length > 0) {
        summary.innerHTML = `✅ ${validFiles.length} فایل متنی انتخاب شد (حجم کل: ${sizeMB} مگابایت)`;
        summary.style.display = 'block';
        uploadBtn.disabled = false;

        const dt = new DataTransfer();
        validFiles.forEach(file => dt.items.add(file));
        e.target.files = dt.files;
    } else {
        summary.style.display = 'none';
        uploadBtn.disabled = true;
    }
});
</script>
</body>
</html>