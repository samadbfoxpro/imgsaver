<?php
// ==================== بارگذاری تنظیمات از config.php ====================
require_once 'config.php';

date_default_timezone_set('Asia/Tehran');

// ==================== تابع خواندن اطلاعات از فایل .txt ====================
function read_image_metadata($image_path) {
    $filename = pathinfo($image_path, PATHINFO_FILENAME);
    $txt_path = dirname($image_path) . '/' . $filename . '.txt';

    $positive = '';
    $negative = '';
    $description = '';

    if (file_exists($txt_path)) {
        $content = file_get_contents($txt_path);

        // Positive Prompt
        if (preg_match('/Positive Prompt\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $p)) {
            $positive = trim($p[1]);
        } else {
            $lines = explode("\n", $content);
            $positive = trim($lines[0] ?? '');
        }

        // Negative Prompt
        if (preg_match('/Negative Prompt\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $n)) {
            $negative = trim($n[1]);
        } else {
            $lines = explode("\n", $content);
            $negative = trim($lines[1] ?? '');
        }

        // Description (توضیحات اختیاری)
        if (preg_match('/Description\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $d)) {
            $description = trim($d[1]);
        }
    }

    return compact('positive', 'negative', 'description');
}

// ==================== تابع نمایش تاریخ میلادی ====================
function persian_date($ymd) {
    // فقط تاریخ میلادی رو برمی‌گردونیم
    return $ymd;
}

// دریافت شماره صفحه (روز) از URL
$current_day_page = isset($_GET['page']) ? max(1, (int)$_GET['page']) : 1;

// ==================== خواندن همه فایل‌های عکس از مسیر گالری ====================
$all_images = [];
if (is_dir(GALLERY_PATH)) {
    $files = scandir(GALLERY_PATH);
    foreach ($files as $file) {
        if ($file === '.' || $file === '..') continue;
        if (!is_valid_image($file)) continue;

        $full_path = GALLERY_PATH . $file;
        $mtime = filemtime($full_path);
        $date = date('Y-m-d', $mtime);

        $all_images[] = [
            'filename' => $file,
            'path' => $full_path,
            'date' => $date,
            'mtime' => $mtime
        ];
    }
}

// مرتب‌سازی بر اساس تاریخ (جدیدترین اول)
usort($all_images, fn($a, $b) => $b['mtime'] <=> $a['mtime']);

// متغیرهای سرچ
$search_query = trim($_GET['q'] ?? '');
$search_results = [];

// ==================== سیستم جستجوی کامل در کل گالری (همه کلمات باید وجود داشته باشند) ====================
if ($search_query !== '') {
    $search_terms = array_filter(array_map('trim', explode(' ', $search_query)));

    foreach ($all_images as $img) {
        $text_path = GALLERY_PATH . pathinfo($img['filename'], PATHINFO_FILENAME) . '.txt';
        $content = '';
        if (file_exists($text_path)) {
            $content = file_get_contents($text_path);
        }

        $match = true;
        foreach ($search_terms as $term) {
            if (stripos($img['filename'], $term) === false && stripos($content, $term) === false) {
                $match = false;
                break;
            }
        }

        if ($match) {
            $metadata = read_image_metadata($img['path']);
            $search_results[] = [
                'path' => $img['path'],
                'dir' => GALLERY_PATH,
                'filename' => $img['filename'],
                'positive' => $metadata['positive'],
                'negative' => $metadata['negative'],
                'description' => $metadata['description']
            ];
        }
    }

    usort($search_results, function($a, $b) {
        return filemtime($b['path']) - filemtime($a['path']);
    });

    $current_day = null;
    $current_files = [];
    $days_list = [];
    $total_days = 0;
    $current_day_page = 1;
} else {
    // گروه‌بندی بر اساس روز
    $days = [];
    foreach ($all_images as $img) {
        $days[$img['date']][] = $img;
    }
    $days_list = array_keys($days);
    rsort($days_list); // جدیدترین روز اول
    $total_days = count($days_list);

    // تعیین روز فعلی
    $current_day_index = $current_day_page - 1;
    if ($current_day_index >= $total_days) {
        $current_day_index = max(0, $total_days - 1);
        $current_day_page = $current_day_index + 1;
    }

    $current_day = $days_list[$current_day_index] ?? null;
    $current_files = $current_day ? $days[$current_day] : [];
}

// ==================== پوشهٔ موقت برای فایل‌های ZIP ====================
$temp_web_dir = __DIR__ . '/../temp-zip/';
if (!is_dir($temp_web_dir)) {
    mkdir($temp_web_dir, 0755, true);
}

// ==================== پاک‌سازی دستی فایل‌های ZIP (با تأیید کاربر) ====================
if (isset($_GET['cleanup_zip']) && $_GET['cleanup_zip'] === 'confirm') {
    $deleted_count = 0;
    $zip_files = glob($temp_web_dir . '*.zip');
    foreach ($zip_files as $file) {
        if (unlink($file)) {
            $deleted_count++;
        }
    }
    header("Location: " . strtok($_SERVER['REQUEST_URI'], '?') . "?cleanup_success=1");
    exit;
}

// ==================== دانلود تمام فایل‌های یک روز به صورت ZIP ====================
if (isset($_GET['download_day_zip']) && isset($_GET['date'])) {
    $target_date = $_GET['date'];
    $all_files = [];

    if (is_dir(GALLERY_PATH)) {
        $files = scandir(GALLERY_PATH);
        foreach ($files as $file) {
            if ($file === '.' || $file === '..') continue;
            if (!is_valid_image($file)) continue;

            $full_path = GALLERY_PATH . $file;
            if (!is_file($full_path)) continue;

            if (date('Y-m-d', filemtime($full_path)) === $target_date) {
                $all_files[] = $full_path;
                $txt_file = preg_replace('/\.(jpg|jpeg|png|gif|webp|bmp)$/i', '.txt', $full_path);
                if (file_exists($txt_file)) {
                    $all_files[] = $txt_file;
                }
            }
        }
    }

    if (empty($all_files)) {
        die("هیچ فایلی برای روز $target_date یافت نشد.");
    }

    $base_name = 'gallery_' . $target_date;
    $unique_name = $base_name . '.zip';
    $zip_path = $temp_web_dir . $unique_name;
    $public_url = 'temp-zip/' . $unique_name;
    $counter = 1;
    while (file_exists($zip_path)) {
        $unique_name = $base_name . "_" . $counter . ".zip";
        $zip_path = $temp_web_dir . $unique_name;
        $public_url = 'temp-zip/' . $unique_name;
        $counter++;
    }

    try {
        $phar = new PharData($zip_path);
        $phar->startBuffering();

        foreach ($all_files as $file_path) {
            if (!file_exists($file_path)) continue;
            $local_name = basename($file_path);
            $phar->addFile($file_path, $local_name);
        }

        $phar->stopBuffering();

        header("Location: " . $public_url);
        exit;
    } catch (Exception $e) {
        if (file_exists($zip_path)) {
            @unlink($zip_path);
        }
        die("خطا در ایجاد فایل ZIP: " . htmlspecialchars($e->getMessage()));
    }
}

// ==================== حذف تکی فایل (تصویر + فایل متنی) ====================
if (isset($_GET['delete_single']) && isset($_GET['image'])) {
    $image_path = $_GET['image'];

    if (strpos($image_path, GALLERY_PATH) !== 0 || !file_exists($image_path)) {
        die("فایل معتبر نیست!");
    }

    unlink($image_path);

    $filename = pathinfo($image_path, PATHINFO_FILENAME);
    $text_path = GALLERY_PATH . $filename . '.txt';
    if (file_exists($text_path)) {
        unlink($text_path);
    }

    if (!empty($_GET['q'])) {
        header("Location: ?q=" . urlencode($_GET['q']));
    } else {
        header("Location: " . strtok($_SERVER["REQUEST_URI"], '?') . "?page=" . $current_day_page);
    }
    exit;
}

// ==================== حذف گروهی فایل‌ها ====================
if (isset($_POST['delete_selected']) && isset($_POST['selected_files'])) {
    $selected_files = $_POST['selected_files'];

    foreach ($selected_files as $image_path) {
        if (strpos($image_path, GALLERY_PATH) !== 0 || !file_exists($image_path)) continue;

        unlink($image_path);

        $filename = pathinfo($image_path, PATHINFO_FILENAME);
        $text_path = GALLERY_PATH . $filename . '.txt';
        if (file_exists($text_path)) {
            unlink($text_path);
        }
    }

    header("Location: " . strtok($_SERVER["REQUEST_URI"], '?') . "?page=" . $current_day_page);
    exit;
}

// ==================== بررسی وجود فایل ZIP برای نمایش دکمه ====================
$has_zip_files = !empty(glob($temp_web_dir . '*.zip'));

// ==================== ساخت ساختار سلسله‌مراتبی تاریخ میلادی (سال → ماه → روز) ====================
$year_month_day_tree = [];

if (is_dir(GALLERY_PATH)) {
    $files = scandir(GALLERY_PATH);
    foreach ($files as $file) {
        if ($file === '.' || $file === '..') continue;
        if (!is_valid_image($file)) continue;

        $full_path = GALLERY_PATH . $file;
        if (!is_file($full_path)) continue;

        $mtime = filemtime($full_path);
        $date = date('Y-m-d', $mtime);
        [$year, $month, $day] = explode('-', $date);

        // ساخت درخت: سال میلادی → ماه میلادی → روز میلادی → تاریخ میلادی
        $year_month_day_tree[$year][$month][$date] = $date;
    }
}

// مرتب‌سازی معکوس: جدیدترین سال/ماه/روز اول
krsort($year_month_day_tree);
foreach ($year_month_day_tree as $year => $months) {
    krsort($year_month_day_tree[$year]);
    foreach ($months as $month => $days) {
        krsort($year_month_day_tree[$year][$month]);
    }
}

// ==================== تبدیل تاریخ میلادی به شمسی ====================
function gregorian_to_jalali($gy, $gm, $gd) {
    $g_d_m = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];
    if ($gy > 1600) {
        $jy = 979;
        $gy -= 1600;
        $gm -= 1;
        $gd -= 1;
        $gy2 = ($gm > 2) ? ($gy + 1) : $gy;
        $days = (365 * $gy) + ((int)(($gy2 + 3) / 4)) - ((int)(($gy2 + 99) / 100)) + ((int)(($gy2 + 399) / 400)) - 80 + $gd + $g_d_m[$gm];
    } else {
        $jy = 0;
        $gy -= 1;
        $gm -= 1;
        $gd -= 1;
        $gy2 = ($gm > 2) ? ($gy + 1) : $gy;
        $days = (365 * $gy) + ((int)(($gy2 + 3) / 4)) - ((int)(($gy2 + 99) / 100)) + ((int)(($gy2 + 399) / 400)) - 80 + $gd + $g_d_m[$gm];
    }
    $jy += 33 * ((int)($days / 12053));
    $days %= 12053;
    $jy += 4 * ((int)($days / 1461));
    $days %= 1461;
    $jy += (int)(($days - 1) / 365);
    if ($days > 365 * (int)(($days - 1) / 365)) $days = ($days - 1) % 365;
    if ($days < 186) {
        $jm = 1 + (int)($days / 31);
        $jd = 1 + ($days % 31);
    } else {
        $jm = 7 + (int)(($days - 186) / 30);
        $jd = 1 + (($days - 186) % 30);
    }
    return [$jy, $jm, $jd];
}

// ==================== ساخت ساختار سلسله‌مراتبی تاریخ شمسی (سال → ماه → روز) ====================
$jalali_year_month_day_tree = [];

if (is_dir(GALLERY_PATH)) {
    $files = scandir(GALLERY_PATH);
    foreach ($files as $file) {
        if ($file === '.' || $file === '..') continue;
        if (!is_valid_image($file)) continue;

        $full_path = GALLERY_PATH . $file;
        if (!is_file($full_path)) continue;

        $mtime = filemtime($full_path);
        $gregorian_date = date('Y-m-d', $mtime);
        [$gy, $gm, $gd] = explode('-', $gregorian_date);

        // تبدیل به شمسی
        $jalali = gregorian_to_jalali((int)$gy, (int)$gm, (int)$gd);
        $jy = $jalali[0];
        $jm = $jalali[1];
        $jd = $jalali[2];

        // ساخت درخت: سال شمسی → ماه شمسی → تاریخ میلادی
        $jalali_year_month_day_tree[$jy][$jm][$gregorian_date] = $gregorian_date;
    }
}

// مرتب‌سازی معکوس: جدیدترین سال/ماه/روز اول
krsort($jalali_year_month_day_tree);
foreach ($jalali_year_month_day_tree as $year => $months) {
    krsort($jalali_year_month_day_tree[$year]);
    foreach ($months as $month => $days) {
        krsort($jalali_year_month_day_tree[$year][$month]);
    }
}

// ==================== ساختار برای جاوااسکریپت ====================
$jalali_year_month_day_tree_js = [];

foreach ($jalali_year_month_day_tree as $jy => $months) {
    foreach ($months as $jm => $days) {
        foreach ($days as $gregorian_date => $value) {
            $jalali_year_month_day_tree_js[$jy][$jm][] = $gregorian_date;
        }
    }
}