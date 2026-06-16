<?php
// ajax/random-actions.php
// نسخهٔ ساده‌شده برای استفادهٔ شخصی (بدون چک AJAX)
header('Content-Type: application/json; charset=utf-8');
require_once '../config.php';

// فقط اجازه دسترسی اگر فایل config.php بارگذاری شده باشد (برای جلوگیری از اجرای مستقیم در برخی موارد)
if (!defined('GALLERY_PATH')) {
    http_response_code(500);
    echo json_encode(['error' => 'خطا در بارگذاری تنظیمات']);
    exit;
}

// عملیات: دریافت 10 عکس تصادفی
if ($_POST['action'] === 'get_random_images') {
    $images = [];
    if (is_dir(GALLERY_PATH)) {
        $files = scandir(GALLERY_PATH);
        foreach ($files as $file) {
            if ($file === '.' || $file === '..') continue;
            if (!is_valid_image($file)) continue;
            $images[] = $file;
        }
    }

    if (empty($images)) {
        echo json_encode(['error' => 'هیچ عکسی وجود ندارد']);
        exit;
    }

    shuffle($images);
    // بازگرداندن 5 عکس تصادفی (قبلاً 10 بود)
    $random_images = array_slice($images, 0, 5);

    $result = [];
    foreach ($random_images as $filename) {
        $path = GALLERY_PATH . $filename;
        $result[] = [
            'filename' => $filename,
            'basename' => pathinfo($filename, PATHINFO_FILENAME),
            'extension' => pathinfo($filename, PATHINFO_EXTENSION),
            'url' => 'image-proxy.php?img=' . urlencode($path)
        ];
    }

    echo json_encode(['success' => true, 'images' => $result]);
    exit;
}

// عملیات: تغییر نام فایل
if ($_POST['action'] === 'rename_file') {
    $old_filename = basename($_POST['old_filename'] ?? '');
    $new_basename = trim($_POST['new_basename'] ?? '');

    if (!$old_filename || !$new_basename) {
        echo json_encode(['error' => 'نام قدیمی یا جدید نامعتبر است']);
        exit;
    }

    // جلوگیری از کاراکترهای خطرناک (فقط برای جلوگیری از خطا در سیستم‌فایل)
    $new_basename = preg_replace('/[<>:"\/\\|?*\x00-\x1f]/', '_', $new_basename);
    $new_basename = substr($new_basename, 0, 200); // محدودیت طول

    $old_path = GALLERY_PATH . $old_filename;
    $old_ext = pathinfo($old_filename, PATHINFO_EXTENSION);
    $new_filename = $new_basename . '.' . $old_ext;
    $new_path = GALLERY_PATH . $new_filename;

    if (!file_exists($old_path)) {
        echo json_encode(['error' => 'فایل اصلی یافت نشد']);
        exit;
    }

    // جلوگیری از نام تکراری
    $counter = 1;
    $final_new_path = $new_path;
    while (file_exists($final_new_path) && $final_new_path !== $old_path) {
        $final_new_path = GALLERY_PATH . $new_basename . '_' . $counter . '.' . $old_ext;
        $counter++;
    }

    // تغییر نام فایل تصویر
    if (!rename($old_path, $final_new_path)) {
        echo json_encode(['error' => 'خطا در تغییر نام فایل تصویر']);
        exit;
    }

    // تغییر نام فایل متنی (اگر وجود داشت)
    $old_txt = preg_replace('/\.(jpg|jpeg|png|gif|webp|bmp)$/i', '.txt', $old_path);
    $new_txt = preg_replace('/\.(jpg|jpeg|png|gif|webp|bmp)$/i', '.txt', $final_new_path);
    if (file_exists($old_txt)) {
        rename($old_txt, $new_txt);
    }

    echo json_encode([
        'success' => true,
        'new_filename' => basename($final_new_path),
        'new_url' => 'image-proxy.php?img=' . urlencode($final_new_path)
    ]);
    exit;
}

echo json_encode(['error' => 'عملیات نامعتبر']);