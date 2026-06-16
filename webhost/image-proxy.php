<?php
// image-proxy.php (نسخهٔ امن و هماهنگ با config.php)

require_once __DIR__ . '/config.php';

// گرفتن مسیر عکس از URL
$image_path = $_GET['img'] ?? '';

// اطمینان از اینکه مسیر از مسیر مجاز شروع می‌شه
if (strpos($image_path, GALLERY_PATH) !== 0) {
    http_response_code(403);
    die('دسترسی غیرمجاز');
}

if (!file_exists($image_path)) {
    http_response_code(404);
    die('عکس یافت نشد');
}

// تعیین نوع فایل
$finfo = finfo_open(FILEINFO_MIME_TYPE);
$mime = finfo_file($finfo, $image_path);
finfo_close($finfo);

// ارسال عکس
header("Content-Type: $mime");
readfile($image_path);