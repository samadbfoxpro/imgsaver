<?php
// ایجاد پوشه text اگر وجود نداشته باشد
$textDir = __DIR__ . '/text';
if (!is_dir($textDir)) {
    mkdir($textDir, 0777, true);
}

$message = '';
$messageType = '';

// اگر فرم ارسال شده باشد
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['files'])) {
    $selectedFiles = $_POST['files'];
    $successCount = 0;

    foreach ($selectedFiles as $filename) {
        $sourcePath = realpath($filename); // تبدیل مسیر نسبی به مطلق
        if (!file_exists($sourcePath) || !is_file($sourcePath)) {
            continue;
        }

        // تغییر پسوند به .txt
        $pathInfo = pathinfo($sourcePath);
        $newName = $pathInfo['filename'] . '.txt';
        $destPath = $textDir . '/' . $newName;

        // کپی محتوا
        $content = file_get_contents($sourcePath);
        if (file_put_contents($destPath, $content) !== false) {
            $successCount++;
        }
    }

    if ($successCount > 0) {
        $message = "$successCount فایل با موفقیت کپی شدند.";
        $messageType = 'success';
    } else {
        $message = "هیچ فایلی کپی نشد.";
        $messageType = 'error';
    }
}

// پیدا کردن تمام فایل‌های .php در تمام زیرپوشه‌ها
function getAllPhpFiles($dir) {
    $files = [];
    $iterator = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($dir));
    foreach ($iterator as $file) {
        if ($file->isFile() && $file->getExtension() === 'php') {
            $files[] = substr($file->getPathname(), strlen(__DIR__) + 1); // بدون مسیر کامل
        }
    }
    return $files;
}

$phpFiles = getAllPhpFiles(__DIR__);
?>

<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
    <meta charset="UTF-8">
    <title>کپی فایل‌های PHP</title>
    <style>
        body {
            font-family: Tahoma, sans-serif;
            background-color: #1e1e1e;
            color: #f0f0f0;
            text-align: center;
            padding: 30px;
        }
        .container {
            max-width: 800px;
            margin: 0 auto;
            background-color: #2d2d2d;
            padding: 25px;
            border-radius: 10px;
            box-shadow: 0 4px 15px rgba(0, 0, 0, 0.5);
        }
        h1 {
            color: #66d9ef;
            font-size: 1.8em;
        }
        .file-list {
            text-align: right;
            margin: 20px 0;
            max-height: 400px;
            overflow-y: auto;
            padding: 10px;
            background-color: #252526;
            border-radius: 5px;
        }
        .file-item {
            padding: 8px;
            border-bottom: 1px solid #444;
        }
        button {
            background-color: #66d9ef;
            color: #1e1e1e;
            border: none;
            padding: 12px 25px;
            font-size: 16px;
            border-radius: 5px;
            cursor: pointer;
            margin-top: 15px;
        }
        button:hover {
            background-color: #5bc0de;
        }
        .message {
            padding: 12px;
            margin: 15px 0;
            border-radius: 5px;
        }
        .success {
            background-color: #5cb85c;
            color: white;
        }
        .error {
            background-color: #d9534f;
            color: white;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>فایل‌های PHP پروژه</h1>

        <?php if ($message): ?>
            <div class="message <?= $messageType ?>"><?= htmlspecialchars($message) ?></div>
        <?php endif; ?>

        <form method="POST" action="">
            <div class="file-list">
                <?php if (empty($phpFiles)): ?>
                    <p>هیچ فایل .php ای یافت نشد.</p>
                <?php else: ?>
                    <?php foreach ($phpFiles as $file): ?>
                        <div class="file-item">
                            <label>
                                <input type="checkbox" name="files[]" value="<?= htmlspecialchars($file) ?>">
                                <?= htmlspecialchars($file) ?>
                            </label>
                        </div>
                    <?php endforeach; ?>
                <?php endif; ?>
            </div>

            <button type="submit">کپی فایل‌های انتخاب‌شده</button>
        </form>
    </div>
</body>
</html>