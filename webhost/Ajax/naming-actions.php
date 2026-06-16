<?php
// ajax/naming-actions.php
header('Content-Type: application/json; charset=utf-8');
require_once '../config.php';

// مسیر جدید فایل وضعیت در پوشهٔ jsons
$STATUS_FILE = PROJECT_ROOT . '/jsons/naming-status.json';

// بارگذاری وضعیت فعلی
function loadStatus() {
    global $STATUS_FILE;
    if (file_exists($STATUS_FILE)) {
        $json = file_get_contents($STATUS_FILE);
        $data = json_decode($json, true);
        return is_array($data) ? $data : [];
    }
    return [];
}

// ذخیره وضعیت
function saveStatus($data) {
    global $STATUS_FILE;
    $dir = dirname($STATUS_FILE);
    if (!is_dir($dir)) {
        mkdir($dir, 0755, true);
    }
    file_put_contents($STATUS_FILE, json_encode($data, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

// دریافت لیست فایل‌ها با پیجینیشن و جستجو
if ($_POST['action'] === 'get_files') {
    $page = max(1, (int)($_POST['page'] ?? 1));
    $limit = 50;
    $offset = ($page - 1) * $limit;
    $search_query = trim($_POST['search'] ?? '');

    $images = [];
    if (is_dir(GALLERY_PATH)) {
        $files = scandir(GALLERY_PATH);
        foreach ($files as $file) {
            if ($file === '.' || $file === '..') continue;
            if (!is_valid_image($file)) continue;
            $images[] = $file;
        }
    }

    // اگر جستجو نباشد، رفتار قبلی
    if ($search_query === '') {
        $filtered_images = $images;
    } else {
        // جدا کردن کلمات جستجو
        $search_terms = array_filter(array_map('trim', explode(' ', $search_query)));
        $filtered_images = [];

        foreach ($images as $filename) {
            $text_path = GALLERY_PATH . pathinfo($filename, PATHINFO_FILENAME) . '.txt';
            $content = '';
            if (file_exists($text_path)) {
                $content = file_get_contents($text_path);
            }

            $match = true;
            foreach ($search_terms as $term) {
                if (stripos($filename, $term) === false && stripos($content, $term) === false) {
                    $match = false;
                    break;
                }
            }

            if ($match) {
                $filtered_images[] = $filename;
            }
        }
    }

    // بارگذاری وضعیت
    $status = loadStatus();

    // اضافه کردن وضعیت به هر فایل
    $filesWithStatus = [];
    foreach ($filtered_images as $filename) {
        $filesWithStatus[] = [
            'filename' => $filename,
            'confirmed' => $status[$filename]['confirmed'] ?? false,
            'renamed' => $status[$filename]['renamed'] ?? false,
            'original_name' => $status[$filename]['original_name'] ?? $filename
        ];
    }

    // مرتب‌سازی: اول تأییدنشده‌ها، بعد تأییدشده‌ها
    usort($filesWithStatus, function($a, $b) {
        if ($a['confirmed'] == $b['confirmed']) return 0;
        return $a['confirmed'] ? 1 : -1;
    });

    // پیجینیشن
    $total = count($filesWithStatus);
    $paginated = array_slice($filesWithStatus, $offset, $limit);

    echo json_encode([
        'success' => true,
        'files' => $paginated,
        'total' => $total,
        'page' => $page,
        'pages' => ceil($total / $limit)
    ]);
    exit;
}

// تغییر نام فایل
if ($_POST['action'] === 'rename_file') {
    $old_filename = basename($_POST['old_filename'] ?? '');
    $new_basename = trim($_POST['new_basename'] ?? '');

    if (!$old_filename || !$new_basename) {
        echo json_encode(['error' => 'نام قدیمی یا جدید نامعتبر است']);
        exit;
    }

    $new_basename = preg_replace('/[<>:"\/\\|?*\x00-\x1f]/', '_', $new_basename);
    $new_basename = substr($new_basename, 0, 200);
    $old_ext = pathinfo($old_filename, PATHINFO_EXTENSION);
    $new_filename = $new_basename . '.' . $old_ext;

    $old_path = GALLERY_PATH . $old_filename;
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

    // تغییر نام فایل متنی
    $old_txt = preg_replace('/\.(jpg|jpeg|png|gif|webp|bmp)$/i', '.txt', $old_path);
    $new_txt = preg_replace('/\.(jpg|jpeg|png|gif|webp|bmp)$/i', '.txt', $final_new_path);
    if (file_exists($old_txt)) {
        rename($old_txt, $new_txt);
    }

    // به‌روزرسانی وضعیت
    $status = loadStatus();
    $status[$new_filename] = [
        'confirmed' => true,
        'renamed' => true,
        'original_name' => $old_filename
    ];
    // حذف ورودی قدیمی
    unset($status[$old_filename]);
    saveStatus($status);

    echo json_encode([
        'success' => true,
        'new_filename' => basename($final_new_path)
    ]);
    exit;
}

// تأیید/لغو تأیید فایل
if ($_POST['action'] === 'toggle_confirm') {
    $filename = basename($_POST['filename'] ?? '');
    if (!$filename) {
        echo json_encode(['error' => 'نام فایل نامعتبر']);
        exit;
    }

    $status = loadStatus();
    $current = $status[$filename] ?? ['confirmed' => false, 'renamed' => false, 'original_name' => $filename];
    $current['confirmed'] = !($current['confirmed'] ?? false);
    $status[$filename] = $current;
    saveStatus($status);

    echo json_encode(['success' => true, 'confirmed' => $current['confirmed']]);
    exit;
}

echo json_encode(['error' => 'عملیات نامعتبر']);