<?php
// ajax/manage-actions.php
header('Content-Type: application/json; charset=utf-8');
require_once '../config.php';

// حذف چک امنیتی
// if (!isset($_SERVER['HTTP_X_REQUESTED_WITH']) || strtolower($_SERVER['HTTP_X_REQUESTED_WITH']) !== 'xmlhttprequest') {
//     http_response_code(403);
//     echo json_encode(['error' => 'دسترسی غیرمجاز']);
//     exit;
// }

// دریافت فایل‌ها با جستجو
if ($_POST['action'] === 'get_files') {
    $search_query = trim($_POST['search'] ?? '');
    $page = max(1, (int)($_POST['page'] ?? 1));
    $limit = (int)($_POST['limit'] ?? 8); // تغییر اینجا
    $offset = ($page - 1) * $limit;

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

    // مرتب‌سازی بر اساس زمان آخرین تغییر (جدیدترین اول)
    usort($filtered_images, function($a, $b) {
        return filemtime(GALLERY_PATH . $b) - filemtime(GALLERY_PATH . $a);
    });

    // پیجینیشن
    $total = count($filtered_images);
    $paginated = array_slice($filtered_images, $offset, $limit);

    $result = [];
    foreach ($paginated as $filename) {
        $text_path = GALLERY_PATH . pathinfo($filename, PATHINFO_FILENAME) . '.txt';
        $has_txt = file_exists($text_path);
        $meta = [];
        if ($has_txt) {
            $content = file_get_contents($text_path);
            if (preg_match('/Positive Prompt\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $p)) {
                $positive = trim($p[1]);
            } else {
                $lines = explode("\n", $content);
                $positive = trim($lines[0] ?? '---');
            }

            if (preg_match('/Negative Prompt\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $n)) {
                $negative = trim($n[1]);
            } else {
                $lines = explode("\n", $content);
                $negative = trim($lines[1] ?? '---');
            }

            if (preg_match('/Description\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $d)) {
                $description = trim($d[1]);
            } else {
                $description = '';
            }
        } else {
            $positive = '---';
            $negative = '---';
            $description = '';
        }

        $result[] = [
            'filename' => $filename,
            'has_txt' => $has_txt,
            'positive' => $positive,
            'negative' => $negative,
            'description' => $description,
            'mtime' => date('Y-m-d H:i:s', filemtime(GALLERY_PATH . $filename))
        ];
    }

    echo json_encode([
        'success' => true,
        'files' => $result,
        'total' => $total,
        'page' => $page,
        'pages' => ceil($total / $limit),
        'limit' => $limit
    ]);
    exit;
}

// حذف فایل
if ($_POST['action'] === 'delete_file') {
    $filename = basename($_POST['filename'] ?? '');
    if (!$filename) {
        echo json_encode(['error' => 'نام فایل نامعتبر']);
        exit;
    }

    $path = GALLERY_PATH . $filename;
    if (!file_exists($path)) {
        echo json_encode(['error' => 'فایل یافت نشد']);
        exit;
    }

    $txt_path = preg_replace('/\.(jpg|jpeg|png|gif|webp|bmp)$/i', '.txt', $path);

    $errors = [];
    if (!unlink($path)) {
        $errors[] = 'حذف فایل تصویر ناموفق';
    }
    if (file_exists($txt_path) && !unlink($txt_path)) {
        $errors[] = 'حذف فایل متنی ناموفق';
    }

    if ($errors) {
        echo json_encode(['error' => implode(', ', $errors)]);
    } else {
        echo json_encode(['success' => true]);
    }
    exit;
}

// ویرایش فایل txt
if ($_POST['action'] === 'edit_txt') {
    $filename = basename($_POST['filename'] ?? '');
    $positive = trim($_POST['positive'] ?? '');
    $negative = trim($_POST['negative'] ?? '');
    $description = trim($_POST['description'] ?? '');

    if (!$filename) {
        echo json_encode(['error' => 'نام فایل نامعتبر']);
        exit;
    }

    $txt_path = GALLERY_PATH . pathinfo($filename, PATHINFO_FILENAME) . '.txt';

    $content = "Positive Prompt: $positive\n\nNegative Prompt: $negative";
    if ($description) {
        $content .= "\n\nDescription: $description";
    }

    if (file_put_contents($txt_path, $content) === false) {
        echo json_encode(['error' => 'خطا در ذخیره فایل']);
    } else {
        echo json_encode(['success' => true]);
    }
    exit;
}

echo json_encode(['error' => 'عملیات نامعتبر']);