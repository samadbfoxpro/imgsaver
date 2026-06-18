<?php
// create-zip.php
header('Content-Type: text/event-stream');
header('Cache-Control: no-cache');
header('X-Accel-Buffering: no'); // برای Nginx

if (!isset($_GET['date'])) {
    echo "event: error\ndata: {\"message\": \"تاریخ مشخص نشده است.\"}\n\n";
    exit;
}

$target_date = $_GET['date'];
$upload_dirs = [
    'از کامپیوتر' => __DIR__ . '/../uploads/local/',
    'از لینک' => __DIR__ . '/../uploads/from-url/'
];

// جمع‌آوری فایل‌ها
$all_files = [];
foreach ($upload_dirs as $dir) {
    if (!is_dir($dir)) continue;
    $files = glob($dir . '*.{jpg,jpeg,png,gif,webp}', GLOB_BRACE);
    foreach ($files as $file) {
        if (date('Y-m-d', filemtime($file)) === $target_date) {
            $all_files[] = $file;
            $txt_file = preg_replace('/\.(jpg|jpeg|png|gif|webp)$/i', '.txt', $file);
            if (file_exists($txt_file)) {
                $all_files[] = $txt_file;
            }
        }
    }
}

if (empty($all_files)) {
    echo "event: error\ndata: {\"message\": \"هیچ فایلی برای این روز یافت نشد.\"}\n\n";
    exit;
}

$temp_dir = __DIR__ . '/temp-zip/';
if (!is_dir($temp_dir)) mkdir($temp_dir, 0755, true);

$random_suffix = substr(md5(uniqid()), 0, 8);
$zip_name = $target_date . '_gallery_' . $random_suffix . '.zip';
$zip_path = $temp_dir . $zip_name;

// ارسال وضعیت شروع
echo "event: progress\ndata: {\"percent\": 0, \"status\": \"در حال شروع...\", \"current\": 0, \"total\": " . count($all_files) . "}\n\n";
flush();

try {
    $phar = new PharData($zip_path);
    $phar->startBuffering();

    foreach ($all_files as $index => $file_path) {
        if (!file_exists($file_path)) continue;

        $local_name = basename($file_path);
        $counter = 1;
        $original_name = $local_name;
        while (isset($phar[$local_name])) {
            $ext = pathinfo($original_name, PATHINFO_EXTENSION);
            $name = pathinfo($original_name, PATHINFO_FILENAME);
            $local_name = $name . '_' . $counter . ($ext ? '.' . $ext : '');
            $counter++;
        }
        $phar->addFile($file_path, $local_name);

        // محاسبه پیشرفت
        $percent = round(($index + 1) / count($all_files) * 100, 1);
        $status = "در حال افزودن: " . basename($file_path);

        // ارسال وضعیت
        echo "event: progress\ndata: {\"percent\": $percent, \"status\": \"" . addslashes($status) . "\", \"current\": " . ($index + 1) . ", \"total\": " . count($all_files) . "}\n\n";
        flush();

        // کمی تأخیر برای تست (در عمل حذف شود)
        // usleep(100000);
    }

    $phar->stopBuffering();

    // ارسال وضعیت اتمام
    echo "event: complete\ndata: {\"zip_url\": \"temp-zip/" . addslashes($zip_name) . "\"}\n\n";
    flush();

} catch (Exception $e) {
    echo "event: error\ndata: {\"message\": \"" . addslashes($e->getMessage()) . "\"}\n\n";
    flush();
}
?>