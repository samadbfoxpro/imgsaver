<?php
require_once __DIR__ . '/../config.php';

// استانداردسازی مسیر آپلود
$upload_dir = rtrim(GALLERY_PATH, '/') . '/';

$today_files = 0;
if (is_dir($upload_dir)) {
    $files = glob($upload_dir . '*.{jpg,jpeg,png,gif,webp}', GLOB_BRACE);
    $today = date('Y-m-d');
    foreach ($files as $file) {
        if (date('Y-m-d', filemtime($file)) === $today) {
            $today_files++;
        }
    }
}

$last_negative_prompt = '';

if (is_dir($upload_dir)) {
    // لیست تمام فایل‌های .txt
    $txt_files = glob($upload_dir . '*.txt');
    
    if (!empty($txt_files)) {
        // مرتب‌سازی بر اساس تاریخ آخرین ویرایش — جدیدترین اول
        usort($txt_files, function($a, $b) {
            return filemtime($b) - filemtime($a);
        });

        // خواندن اولین فایل (جدیدترین)
        $latest_file = $txt_files[0];
        $content = file_get_contents($latest_file);

        // استخراج Negative Prompt
        if (preg_match('/Negative Prompt\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $matches)) {
            $last_negative_prompt = trim($matches[1]);
        } else {
            // fallback: اگر فرمت خاصی نداشت — خط دوم رو بگیر
            $lines = explode("\n", $content);
            if (isset($lines[3])) { // معمولاً بعد از Positive و خط خالی
                $last_negative_prompt = trim($lines[3]);
            } elseif (isset($lines[1])) {
                $last_negative_prompt = trim($lines[1]);
            }
        }
    }
}

// فقط پردازش اگر فرم سابمیت شده
if ($_SERVER['REQUEST_METHOD'] == 'POST') {
    header('Content-Type: application/json; charset=utf-8');

    if (!is_dir($upload_dir)) {
        mkdir($upload_dir, 0777, true);
    }

    $filename = trim($_POST['filename']);
    if (empty($filename)) {
        echo json_encode(['success' => false, 'message' => 'نام فایل الزامی است!']);
        exit;
    }

    $positive = $_POST['positive_prompt'] ?? '';
    $negative = $_POST['negative_prompt'] ?? '';
    $optional_description = $_POST['optional_description'] ?? ''; // بخش جدید

    $image_path = '';
    $text_path = '';

    // حالت ۱: آپلود از فایل
    if (!empty($_FILES['image']['name']) && $_FILES['image']['error'] === UPLOAD_ERR_OK) {
        $image = $_FILES['image'];

        $image_ext = pathinfo($image['name'], PATHINFO_EXTENSION);
        $allowed_exts = ['jpg', 'jpeg', 'png', 'gif', 'webp'];
        if (!in_array(strtolower($image_ext), $allowed_exts)) {
            echo json_encode(['success' => false, 'message' => 'فرمت عکس مجاز نیست!']);
            exit;
        }

        $final_name = $filename;
        $image_path = $upload_dir . $final_name . '.' . $image_ext;
        $text_path = $upload_dir . $final_name . '.txt';

        $counter = 1;
        while (file_exists($image_path)) {
            $image_path = $upload_dir . $final_name . '_' . $counter . '.' . $image_ext;
            $text_path = $upload_dir . $final_name . '_' . $counter . '.txt';
            $counter++;
        }

        if (!move_uploaded_file($image['tmp_name'], $image_path)) {
            echo json_encode(['success' => false, 'message' => 'خطا در ذخیره عکس!']);
            exit;
        }

        // تنظیم زمان فایل بر اساس زمان ایران
        date_default_timezone_set('Asia/Tehran');
        touch($image_path, time());

    // حالت ۲: آپلود از کلیپ‌بورد
    } elseif (!empty($_POST['pasted_image'])) {
        $base64 = $_POST['pasted_image'];
        $data = explode(',', $base64);
        if (count($data) < 2) {
            echo json_encode(['success' => false, 'message' => 'داده تصویر نامعتبر است!']);
            exit;
        }

        $image_data = base64_decode($data[1]);
        if ($image_data === false) {
            echo json_encode(['success' => false, 'message' => 'خطا در دیکد کردن تصویر!']);
            exit;
        }

        $header = substr($data[0], strpos($data[0], '/') + 1, strpos($data[0], ';') - strpos($data[0], '/') - 1);
        $ext = '';
        switch ($header) {
            case 'jpeg':
            case 'jpg':
                $ext = 'jpg';
                break;
            case 'png':
                $ext = 'png';
                break;
            case 'gif':
                $ext = 'gif';
                break;
            case 'webp':
                $ext = 'webp';
                break;
            default:
                echo json_encode(['success' => false, 'message' => 'فرمت عکس پشتیبانی نمی‌شود: ' . $header]);
                exit;
        }

        $final_name = $filename;
        $image_path = $upload_dir . $final_name . '.' . $ext;
        $text_path = $upload_dir . $final_name . '.txt';

        $counter = 1;
        while (file_exists($image_path)) {
            $image_path = $upload_dir . $final_name . '_' . $counter . '.' . $ext;
            $text_path = $upload_dir . $final_name . '_' . $counter . '.txt';
            $counter++;
        }

        if (!file_put_contents($image_path, $image_data)) {
            echo json_encode(['success' => false, 'message' => 'خطا در ذخیره عکس پیست شده!']);
            exit;
        }

        // تنظیم زمان فایل بر اساس زمان ایران
        date_default_timezone_set('Asia/Tehran');
        touch($image_path, time());

    } else {
        echo json_encode(['success' => false, 'message' => 'لطفاً یک عکس انتخاب کنید یا پیست کنید!']);
        exit;
    }

    // ساخت محتوای فایل متنی
    $text_content = "Positive Prompt:\n" . $positive . "\n\nNegative Prompt:\n" . $negative;

    // اضافه کردن توضیحات اختیاری (اگر وجود داشته باشد)
    if (!empty($optional_description)) {
        $text_content .= "\n\nDescription:\n" . $optional_description;
    }

    if (file_put_contents($text_path, $text_content) === false) {
        echo json_encode(['success' => false, 'message' => 'خطا در ذخیره فایل متنی!']);
        exit;
    }

    // موفقیت — فقط نام فایل رو برگردون
    echo json_encode([
        'success' => true,
        'filename' => basename($image_path)
    ]);
    exit;
}
?>