<?php
require_once __DIR__ . '/config.php';

if (!isset($_GET['file']) || !is_string($_GET['file'])) {
    http_response_code(400);
    die('❌ فایل مشخص نشده است.');
}

$file_path = $_GET['file'];

// اطمینان از اینکه فایل داخل GALLERY_PATH باشد (امنیت!)
$real_gallery = realpath(GALLERY_PATH);
$real_file = realpath($file_path);

if ($real_file === false || strpos($real_file, $real_gallery) !== 0) {
    http_response_code(403);
    die('❌ دسترسی غیرمجاز.');
}

if (!file_exists($real_file) || pathinfo($real_file, PATHINFO_EXTENSION) !== 'txt') {
    http_response_code(404);
    die('❌ فایل متنی یافت نشد.');
}

// تنظیم هدرهای دانلود
header('Content-Type: text/plain; charset=utf-8');
header('Content-Disposition: attachment; filename="' . basename($real_file) . '"');
header('Content-Length: ' . filesize($real_file));

readfile($real_file);
exit;