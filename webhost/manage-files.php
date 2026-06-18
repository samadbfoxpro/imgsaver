<?php
// صفحه مدیریت فایل‌های آپلود شده (عکس و متن)
// نمایش لیست فایل‌ها با امکان حذف و ویرایش

$uploads_dir = __DIR__ . '/uploads/local/';
if (!is_dir($uploads_dir)) {
    die('پوشهٔ آپلود وجود ندارد.');
}

$files = array_diff(scandir($uploads_dir), ['.', '..']);
$items = [];

foreach ($files as $file) {
    $path = $uploads_dir . $file;
    if (!is_file($path)) continue;

    $type = pathinfo($file, PATHINFO_EXTENSION);
    $is_image = in_array(strtolower($type), ['jpg', 'jpeg', 'png', 'gif', 'webp']);
    $is_text = strtolower($type) === 'txt';
    $time = filemtime($path);
    $items[] = [
        'name' => $file,
        'type' => $is_image ? 'image' : ($is_text ? 'text' : 'other'),
        'time' => $time,
        'path' => $path
    ];
}

// مرتب‌سازی بر اساس زمان آپلود (جدیدترین بالا)
usort($items, function ($a, $b) {
    return $b['time'] - $a['time'];
});

// پارامترهای صفحه‌بندی و جستجو
$page = isset($_GET['page']) ? max(1, intval($_GET['page'])) : 1;
$per_page = 10;
$search_query = isset($_GET['q']) ? trim($_GET['q']) : '';

// فیلتر بر اساس جستجو
if ($search_query !== '') {
    $filtered_items = array_filter($items, function ($item) use ($search_query) {
        return mb_stripos($item['name'], $search_query) !== false;
    });
    $filtered_items = array_values($filtered_items); // ریست کلیدها
    $total_files = count($filtered_items);
    $paged_items = array_slice($filtered_items, ($page - 1) * $per_page, $per_page);
} else {
    $total_files = count($items);
    $paged_items = array_slice($items, ($page - 1) * $per_page, $per_page);
}

$total_pages = ceil($total_files / $per_page);

// تابع ساخت URL پایه برای صفحه‌بندی
function buildBaseUrl($search_query) {
    $url = '?';
    if ($search_query !== '') {
        $url .= 'q=' . urlencode($search_query) . '&';
    }
    return $url;
}

$base_url = buildBaseUrl($search_query);

// ======================
// پردازش‌های POST/GET
// ======================

// حذف فایل تکی
if (isset($_GET['delete_single']) && isset($_GET['file'])) {
    $requested_file = basename($_GET['file']);
    $del_path = $uploads_dir . $requested_file;

    // امنیت: فقط فایل‌هایی که واقعاً در لیست هستن قابل حذف‌اند
    $allowed_names = array_column($items, 'name');
    if (in_array($requested_file, $allowed_names) && is_file($del_path)) {
        unlink($del_path);
    }
    // redirect با حفظ جستجو و صفحه
    $redirect_url = 'manage-files.php' . ($search_query ? '?q=' . urlencode($search_query) : '') . ($page > 1 ? (strpos($redirect_url, '?') !== false ? '&' : '?') . 'page=' . $page : '');
    header('Location: ' . $redirect_url);
    exit;
}

// ویرایش فایل متنی
if (isset($_POST['edit_file']) && isset($_POST['new_content'])) {
    $edit_name = basename($_POST['edit_file']);
    $edit_path = $uploads_dir . $edit_name;

    // امنیت: فقط فایل‌های مجاز
    $allowed_names = array_column($items, 'name');
    if (in_array($edit_name, $allowed_names) && is_file($edit_path)) {
        $old_time = filemtime($edit_path);
        $raw = $_POST['new_content'];

        $positive = '';
        $negative = '';
        if (preg_match('/Positive Prompt\s*:(.*?)(?:Negative Prompt|$)/si', $raw, $pm)) {
            $positive = trim($pm[1]);
        }
        if (preg_match('/Negative Prompt\s*:(.*)/si', $raw, $nm)) {
            $negative = trim($nm[1]);
        }
        if ($positive === '' && $negative === '') {
            $lines = explode("\n", $raw);
            $positive = trim($lines[0] ?? '');
            $negative = trim($lines[1] ?? '');
        }
        $final = "Positive Prompt:\n" . $positive . "\n\nNegative Prompt:\n" . $negative;
        file_put_contents($edit_path, $final);
        touch($edit_path, $old_time);
    }
    header('Location: manage-files.php' . ($search_query ? '?q=' . urlencode($search_query) : ''));
    exit;
}
?>
<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>مدیریت فایل‌های آپلود شده</title>
    <link rel="stylesheet" href="css/styleindex.css">
    <link rel="stylesheet" href="css/manage-files.css">
    <link rel="stylesheet" href="css/gallery-base-style.css">
    <link rel="stylesheet" href="css/gallery-back-button-style.css">
    <link rel="stylesheet" href="css/gallery-components-style.css">
    <link rel="stylesheet" href="css/vertical-menu.css">
    <style>
        .search-box {
            width: 100%;
            max-width: 600px;
            padding: 12px;
            border-radius: 10px;
            border: 1.5px solid #bb86fc;
            background: #232d3f;
            color: white;
            font-size: 1.1rem;
            box-sizing: border-box;
        }
        .search-box::placeholder {
            color: #aaa;
        }
    </style>
</head>
<body>
    <!-- منوی عمودی گالری -->
    <?php include 'sidebar.php'; ?>

    <div class="manage-container">
        <div style="text-align: right; margin-bottom: 18px;">
            <a href="index.php" class="btn main-btn" style="background:#bb86fc; color:#232d3f; padding:8px 28px; border-radius:10px; font-weight:600; font-size:1.08rem; text-decoration:none; box-shadow:0 2px 8px #0004; transition:background 0.2s;">صفحه اصلی</a>
        </div>
        <h2>مدیریت فایل‌های آپلود شده</h2>

        <!-- فرم جستجو -->
        <form method="get" style="margin: 20px auto; max-width: 600px;">
            <input type="hidden" name="page" value="1">
            <input type="text" name="q" value="<?= htmlspecialchars($search_query) ?>" class="search-box" placeholder="🔍 جستجو در نام فایل...">
        </form>

        <!-- نمایش نتایج -->
        <?php if (empty($paged_items)): ?>
            <div style="text-align:center; padding:30px; color:#aaa;">
                فایلی یافت نشد.
            </div>
        <?php else: ?>
            <?php foreach ($paged_items as $item): ?>
            <div class="file-row" style="position:relative;">
                <!-- دکمه حذف در سمت چپ (در rtl = سمت چپ صفحه) -->
                <button class="delete-single-btn"
                    style="position:absolute; top:10px; left:10px; background:#cf6679; color:white; border:none; padding:0 18px; height:38px; border-radius:10px; cursor:pointer; font-size:1.08rem; display:flex; align-items:center; justify-content:center; z-index:10; transition:background 0.3s;"
                    onclick="return confirm('آیا مطمئنید می‌خواهید این فایل را حذف کنید؟') && (location.href='?delete_single=1&file=<?= urlencode($item['name']) ?><?= $search_query ? '&q=' . urlencode($search_query) . '&page=' . $page : '&page=' . $page ?>');">
                    حذف
                </button>

                <?php if ($item['type'] === 'image'): ?>
                    <img src="uploads/local/<?= htmlspecialchars($item['name']) ?>" alt="<?= htmlspecialchars($item['name']) ?>">
                <?php endif; ?>

                <div class="file-info">
                    <div class="file-time"><?= date('Y/m/d H:i', $item['time']) ?></div>
                    <div class="file-name"><?= htmlspecialchars($item['name']) ?></div>
                    <?php if ($item['type'] === 'text'): ?>
                        <?php if (isset($_POST['edit_file']) && $_POST['edit_file'] === $item['name']): ?>
                            <form method="post" style="margin-top:8px;" autocomplete="off">
                                <textarea name="new_content" class="edit-area"><?php echo htmlspecialchars(file_get_contents($item['path'])); ?></textarea>
                                <input type="hidden" name="edit_file" value="<?= htmlspecialchars($item['name']) ?>">
                                <button type="submit" class="edit-btn">ذخیره تغییرات</button>
                            </form>
                        <?php else: ?>
                            <form method="post" style="display:inline;" autocomplete="off">
                                <input type="hidden" name="edit_file" value="<?= htmlspecialchars($item['name']) ?>">
                                <button type="submit" class="edit-btn">ویرایش متن</button>
                            </form>
                        <?php endif; ?>
                    <?php endif; ?>
                </div>
            </div>
            <?php endforeach; ?>

            <!-- صفحه‌بندی -->
            <?php if ($total_pages > 1): ?>
            <div class="pagination" style="margin: 32px 0; display: flex; justify-content: center; align-items: center; gap: 12px; font-size:1.08rem;">
                <?php if ($page > 1): ?>
                    <a href="<?= $base_url ?>page=<?= $page - 1 ?>" class="page-btn" style="background:#bb86fc;color:#232d3f;padding:8px 18px;border-radius:8px;text-decoration:none;font-weight:bold;">« قبلی</a>
                <?php else: ?>
                    <span class="page-btn disabled" style="background:#232d3f;color:#aaa;padding:8px 18px;border-radius:8px;">« قبلی</span>
                <?php endif; ?>

                <span style="color:#bb86fc;font-weight:bold;">صفحه <?= $page ?> از <?= $total_pages ?></span>

                <?php if ($page < $total_pages): ?>
                    <a href="<?= $base_url ?>page=<?= $page + 1 ?>" class="page-btn" style="background:#bb86fc;color:#232d3f;padding:8px 18px;border-radius:8px;text-decoration:none;font-weight:bold;">بعدی »</a>
                <?php else: ?>
                    <span class="page-btn disabled" style="background:#232d3f;color:#aaa;padding:8px 18px;border-radius:8px;">بعدی »</span>
                <?php endif; ?>
            </div>
            <?php endif; ?>
        <?php endif; ?>
    </div>
</body>
</html>