<?php
require_once '/storage/emulated/0/www/myproject/config.php'; // بارگذاری مسیر گالری

$log_file = __DIR__ . '/1upload_log.json';

if (!file_exists($log_file)) {
    die("فایل upload_log.json یافت نشد.");
}

$message = '';

if (isset($_POST['update_format'])) {
    $content = file_get_contents($log_file);
    $data = json_decode($content, true);

    if (!$data) {
        die("فایل JSON معتبر نیست.");
    }

    // تبدیل همه آیتم‌ها به فرمت جدید
    foreach ($data as $file => $info) {
        if (!isset($info['status'])) {
            if (isset($info['error'])) {
                $data[$file]['status'] = 'failed';
            } else {
                $data[$file]['status'] = 'sent';
            }
        }
    }

    // ذخیره مجدد فایل
    file_put_contents($log_file, json_encode($data, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
    $message = "فرمت upload_log.json با موفقیت به روز شد!";
}

if (isset($_POST['clean_test'])) {
    $content = file_get_contents($log_file);
    $data = json_decode($content, true);

    if (!$data) {
        die("فایل JSON معتبر نیست.");
    }

    $count = 0;
    foreach ($data as $file => $info) {
        if (stripos($file, 'test') !== false || mb_stripos($file, 'تست', 0, 'UTF-8') !== false) {
            unset($data[$file]);
            $count++;
        }
    }

    // ذخیره مجدد فایل
    file_put_contents($log_file, json_encode($data, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
    $message = "تعداد $count فایل تست از upload_log.json حذف شد.";
}

// بخش جدید: Sync تصاویر گالری با log و تنظیم وضعیت به 'sent'
if (isset($_POST['sync_gallery'])) {
    $content = file_get_contents($log_file);
    $data = json_decode($content, true);

    if (!$data) {
        $data = [];
    }

    $gallery_path = rtrim(GALLERY_PATH, '/') . '/';

    if (!is_dir($gallery_path)) {
        die("پوشه گالری یافت نشد: " . htmlspecialchars($gallery_path));
    }

    $image_files = glob($gallery_path . '*.{jpg,jpeg,png,gif,webp,bmp}', GLOB_BRACE);

    $added = 0;
    $updated = 0;
    foreach ($image_files as $file_path) {
        $filename = basename($file_path);
        if (!isset($data[$filename])) {
            $data[$filename] = [
                'status' => 'sent',
                'timestamp' => date('Y-m-d H:i:s')
            ];
            $added++;
        } else {
            // اگر وجود داشت، وضعیت را به 'sent' بروزرسانی کن
            $data[$filename]['status'] = 'sent';
            $updated++;
        }
    }

    file_put_contents($log_file, json_encode($data, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
    $message = "تعداد $added فایل جدید اضافه شد و $updated فایل موجود بروزرسانی شد (وضعیت: ارسال شده).";
}
?>

<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
    <meta charset="UTF-8">
    <title>مدیریت upload_log.json</title>
    <style>
        body {
            font-family: Tahoma, Arial, sans-serif;
            background: #f5f5f5;
            text-align: center;
            padding: 30px;
        }
        .container {
            background: white;
            max-width: 500px;
            margin: 0 auto;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }
        button {
            padding: 10px 20px;
            margin: 10px;
            font-size: 16px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
        }
        .update-btn {
            background: #4CAF50;
            color: white;
        }
        .clean-btn {
            background: #f44336;
            color: white;
        }
        .sync-btn {
            background: #2196F3;
            color: white;
        }
        .message {
            margin-top: 20px;
            padding: 10px;
            border-radius: 4px;
        }
        .success {
            background: #d4edda;
            color: #155724;
        }
    </style>
</head>
<body>
    <div class="container">
        <h2>مدیریت upload_log.json</h2>
        <form method="POST">
            <button type="submit" name="update_format" class="update-btn">🔄 بروزرسانی فرمت</button>
        </form>
        <form method="POST">
            <button type="submit" name="clean_test" class="clean-btn">🗑️ پاکسازی فایل‌های تست</button>
        </form>
        <form method="POST">
            <button type="submit" name="sync_gallery" class="sync-btn">🔄 همگام‌سازی گالری (وضعیت: ارسال شده)</button>
        </form>
        <?php if ($message): ?>
            <div class="message success"><?= htmlspecialchars($message) ?></div>
        <?php endif; ?>
    </div>
</body>
</html>