<?php
// بارگذاری تنظیمات از config.php
require_once 'config.php';

// تابع تبدیل تاریخ میلادی به شمسی (ساده)
function gregorian_to_jalali($gy, $gm, $gd)
{
    $g_days_in_month = array(31,28,31,30,31,30,31,31,30,31,30,31);
    $j_days_in_month = array(31,31,31,31,31,31,30,30,30,30,30,29);
    $gy = (int)$gy-1600;
    $gm = (int)$gm-1;
    $gd = (int)$gd-1;
    $g_day_no = 365*$gy+intval(($gy+3)/4)-intval(($gy+99)/100)+intval(($gy+399)/400);
    for ($i=0; $i<$gm; ++$i)
        $g_day_no += $g_days_in_month[$i];
    if ($gm>1 && (($gy+1600)%4==0 && (($gy+1600)%100!=0 || ($gy+1600)%400==0)))
        $g_day_no++;
    $g_day_no += $gd;
    $j_day_no = $g_day_no-79;
    $j_np = intval($j_day_no/12053);
    $j_day_no = $j_day_no%12053;
    $jy = 979+33*$j_np+4*intval($j_day_no/1461);
    $j_day_no %= 1461;
    if ($j_day_no >= 366) {
        $jy += intval(($j_day_no-366)/365);
        $j_day_no = ($j_day_no-366)%365;
    }
    for ($i=0; $i<11 && $j_day_no>=$j_days_in_month[$i]; ++$i)
        $j_day_no -= $j_days_in_month[$i];
    $jm = $i+1;
    $jd = $j_day_no+1;
    return [$jy, $jm, $jd];
}

function jalali_format($ymd) {
    list($gy, $gm, $gd) = explode('-', $ymd);
    list($jy, $jm, $jd) = gregorian_to_jalali($gy, $gm, $gd);
    $jm = str_pad($jm, 2, '0', STR_PAD_LEFT);
    $jd = str_pad($jd, 2, '0', STR_PAD_LEFT);
    return "$jy-$jm-$jd";
}

date_default_timezone_set('Asia/Tehran');

// استفاده از مسیر جدید از config.php
$upload_dir = GALLERY_PATH;
$temp_zip_dir = __DIR__ . '/temp-zip/';
if (!is_dir($temp_zip_dir)) mkdir($temp_zip_dir, 0755, true);

// خواندن همه فایل‌های عکس از مسیر جدید
$all_files = [];
if (is_dir($upload_dir)) {
    $files = scandir($upload_dir);
    foreach ($files as $file) {
        if ($file === '.' || $file === '..') continue;
        if (!is_valid_image($file)) continue;
        $full_path = $upload_dir . $file;
        if (is_file($full_path)) {
            $all_files[] = $full_path;
        }
    }
}

$days = [];
foreach ($all_files as $file) {
    $day = date('Y-m-d', filemtime($file));
    if (!isset($days[$day])) $days[$day] = [];
    $days[$day][] = $file;
}
krsort($days);

$search = isset($_GET['q']) ? trim($_GET['q']) : '';
if ($search !== '') {
    $days = array_filter($days, function($files, $day) use ($search) {
        return strpos($day, $search) !== false;
    }, ARRAY_FILTER_USE_BOTH);
}

$days_list = array_keys($days);
$total_days = count($days_list);
$per_page = 50;
$page = isset($_GET['page']) ? max(1, (int)$_GET['page']) : 1;
$start = ($page - 1) * $per_page;
$paged_days = array_slice($days_list, $start, $per_page);

if (isset($_POST['multi_zip']) && !empty($_POST['selected_days']) && is_array($_POST['selected_days'])) {
    $selected = array_filter($_POST['selected_days'], function($d) use ($days) { return isset($days[$d]); });
    sort($selected);
    $jalali_from = jalali_format($selected[0]);
    $jalali_to = jalali_format($selected[count($selected)-1]);
    $zip_name = 'از_' . $jalali_from . '_تا_' . $jalali_to . '_' . substr(md5(implode('_',$selected).rand()),0,6) . '.zip';
    $zip_path = $temp_zip_dir . $zip_name;
    $public_url = 'temp-zip/' . $zip_name;
    $tmp_zips = [];
    foreach ($selected as $d) {
        $jalali = jalali_format($d);
        $sub_zip = $temp_zip_dir . 'daily_' . $jalali . '_' . $d . '_' . substr(md5($d.rand()),0,4) . '.zip';
        $zip = new PharData($sub_zip);
        $zip->startBuffering();
        foreach ($days[$d] as $file) {
            $zip->addFile($file, basename($file));
            $txt = preg_replace('/\.(jpg|jpeg|png|gif|webp|bmp)$/i', '.txt', $file);
            if (file_exists($txt)) $zip->addFile($txt, basename($txt));
        }
        $zip->stopBuffering();
        $tmp_zips[] = $sub_zip;
    }
    $final_zip = new PharData($zip_path);
    $final_zip->startBuffering();
    foreach ($tmp_zips as $z) {
        $final_zip->addFile($z, basename($z));
    }
    $final_zip->stopBuffering();
    foreach ($tmp_zips as $z) @unlink($z);
    header('Location: ' . $public_url);
    exit;
}

if (isset($_GET['zip']) && isset($_GET['date'])) {
    $target_date = $_GET['date'];
    if (isset($days[$target_date])) {
        $jalali = jalali_format($target_date);
        $zip_name = 'daily_' . $jalali . '_' . $target_date . '_' . substr(md5($target_date . rand()), 0, 6) . '.zip';
        $zip_path = $temp_zip_dir . $zip_name;
        $public_url = 'temp-zip/' . $zip_name;
        $zip = new PharData($zip_path);
        $zip->startBuffering();
        foreach ($days[$target_date] as $file) {
            $zip->addFile($file, basename($file));
            $txt = preg_replace('/\.(jpg|jpeg|png|gif|webp|bmp)$/i', '.txt', $file);
            if (file_exists($txt)) $zip->addFile($txt, basename($txt));
        }
        $zip->stopBuffering();
        header('Location: ' . $public_url);
        exit;
    }
}
?>
<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>آپلودهای روزانه</title>
    <style>
        body {
            background: #1e1e1e;
            color: #d4d4d4;
            font-family: 'Vazirmatn', Tahoma, sans-serif;
            margin: 0;
            padding: 0;
        }
        .container {
            max-width: 600px;
            margin: 15px auto;
            padding: 16px;
        }
        h1 {
            color: #4fc3f7;
            font-size: 1.5rem;
            text-align: center;
            margin: 10px 0 20px;
        }
        .search-bar {
            width: 100%;
            padding: 12px;
            border-radius: 8px;
            border: 1px solid #333;
            background: #252525;
            color: #fff;
            font-size: 1rem;
            margin-bottom: 20px;
        }
        .day-item {
            background: #2a2d32;
            border-radius: 8px;
            padding: 16px;
            margin-bottom: 12px;
            display: flex;
            flex-direction: column;
            gap: 10px;
        }
        .day-header {
            display: flex;
            align-items: center;
            gap: 10px;
        }
        .day-checkbox {
            transform: scale(1.3);
        }
        .day-info {
            flex: 1;
        }
        .day-label {
            font-size: 1.1rem;
            color: #b3e5fc;
        }
        .day-label small {
            display: block;
            font-size: 0.9rem;
            color: #888;
        }
        .day-count {
            color: #ffd54f;
            font-size: 0.95rem;
        }
        .zip-btn {
            background: #4fc3f7;
            color: #23272e;
            border: none;
            border-radius: 6px;
            padding: 10px;
            font-size: 1rem;
            font-weight: bold;
            cursor: pointer;
            text-align: center;
        }
        .zip-btn:hover {
            background: #0288d1;
            color: #fff;
        }
        #multi-zip-btn {
            background: #bb86fc;
            color: #23272e;
            border: none;
            border-radius: 6px;
            padding: 12px;
            font-size: 1rem;
            font-weight: bold;
            cursor: pointer;
            width: 100%;
            margin: 16px 0;
            display: none;
        }
        .pagination {
            display: flex;
            justify-content: center;
            gap: 8px;
            margin-top: 20px;
            flex-wrap: wrap;
        }
        .pagination-btn {
            padding: 8px 14px;
            background: #2a2d32;
            color: #b3e5fc;
            text-decoration: none;
            border-radius: 6px;
            font-size: 0.95rem;
        }
        .pagination-btn.active {
            background: #4fc3f7;
            color: #23272e;
        }
    </style>
</head>
<body>
        <?php include 'sidebar.php'; ?>

    <div class="container">
        <button onclick="window.history.back()" style="background:#cf6679;color:#fff;border:none;padding:10px;border-radius:6px;width:100%;font-size:1rem;font-weight:bold;">⬅️ بازگشت</button>
        <h1>آپلودهای روزانه</h1>
        <form method="get">
            <input type="text" class="search-bar" name="q" value="<?= htmlspecialchars($search) ?>" placeholder="جستجو...">
        </form>

        <form id="multi-download-form" method="post">
            <button type="submit" name="multi_zip" id="multi-zip-btn">📦 دانلود یکجا</button>
            <div class="days-container">
            <?php if (empty($paged_days)): ?>
                <div style="text-align:center;color:#ffb300;margin:30px 0;">هیچ روزی یافت نشد.</div>
            <?php else:
                foreach ($paged_days as $day):
                    $count = count($days[$day]);
                    $jalali = jalali_format($day);
            ?>
                <div class="day-item">
                    <div class="day-header">
                        <input type="checkbox" name="selected_days[]" value="<?= htmlspecialchars($day) ?>" class="day-checkbox">
                        <div class="day-info">
                            <span class="day-label">📅 <?= htmlspecialchars($jalali) ?></span>
                            <small><?= htmlspecialchars($day) ?></small>
                            <div class="day-count">تعداد: <?= $count ?></div>
                        </div>
                    </div>
                    <a href="?date=<?= urlencode($day) ?>&zip=1<?= $search ? '&q=' . urlencode($search) : '' ?>" class="zip-btn">دانلود ZIP</a>
                </div>
            <?php endforeach; endif; ?>
            </div>
        </form>

        <div class="pagination">
        <?php
        $total_pages = ceil($total_days / $per_page);
        if ($total_pages > 1) {
            for ($i = 1; $i <= $total_pages; $i++) {
                $active = $i === $page ? 'active' : '';
                $q = $search ? '&q=' . urlencode($search) : '';
                echo "<a href='?page=$i$q' class='pagination-btn $active'>$i</a>";
            }
        }
        ?>
        </div>
    </div>
    <script>
    const checkboxes = document.querySelectorAll('.day-checkbox');
    const multiBtn = document.getElementById('multi-zip-btn');
    checkboxes.forEach(cb => {
        cb.addEventListener('change', () => {
            const anyChecked = Array.from(checkboxes).some(c => c.checked);
            multiBtn.style.display = anyChecked ? 'block' : 'none';
        });
    });
    </script>
</body>
</html>