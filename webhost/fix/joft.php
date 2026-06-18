<?php
$projectDir = __DIR__;
$format2Dir = $projectDir . DIRECTORY_SEPARATOR . 'format2';
$format3Dir = $projectDir . DIRECTORY_SEPARATOR . 'format3';

// ایجاد پوشه‌ها
foreach ([$format2Dir, $format3Dir] as $dir) {
    if (!is_dir($dir)) {
        mkdir($dir, 0755, true);
    }
}

function extractBaseAndNumber($filename) {
    $name = pathinfo($filename, PATHINFO_FILENAME);
    if (preg_match('/^(.*?)(\d+)$/', $name, $matches)) {
        $base = $matches[1];
        $num = $matches[2];
        if ($base === '') {
            return [$name, ''];
        }
        return [$base, $num];
    }
    return [$name, ''];
}

function findFiles($dir, $extensions) {
    $result = [];
    if (!is_dir($dir)) return $result;
    $iterator = new RecursiveIteratorIterator(
        new RecursiveDirectoryIterator($dir, RecursiveDirectoryIterator::SKIP_DOTS)
    );
    foreach ($iterator as $file) {
        if ($file->isDir()) continue;
        $ext = strtolower($file->getExtension());
        if (in_array($ext, $extensions)) {
            $result[] = [
                'name' => $file->getFilename(),
                'path' => $file->getPathname(),
                'ext' => $ext
            ];
        }
    }
    return $result;
}

// 1. یکسان‌سازی نام‌ها
if (isset($_POST['normalize_names'])) {
    $textFiles = findFiles($format2Dir, ['txt', 'text']);
    $imageFiles = findFiles($format2Dir, ['jpg', 'jpeg', 'png', 'webp', 'gif']);

    $imageMap = [];
    foreach ($imageFiles as $img) {
        [$base, $num] = extractBaseAndNumber($img['name']);
        $imageMap[$base][$num] = $img;
    }

    $renamed = 0;
    foreach ($textFiles as $txt) {
        [$txtBase, $txtNum] = extractBaseAndNumber($txt['name']);
        if (isset($imageMap[$txtBase])) {
            $imgNumbers = array_keys($imageMap[$txtBase]);
            if (count($imgNumbers) === 1) {
                $imgNum = $imgNumbers[0];
                if ($txtNum !== $imgNum) {
                    $newName = $txtBase . $imgNum . '.' . $txt['ext'];
                    $newPath = $format2Dir . DIRECTORY_SEPARATOR . $newName;
                    if (!file_exists($newPath)) {
                        if (rename($txt['path'], $newPath)) {
                            $renamed++;
                        }
                    }
                }
            }
        }
    }
    $message = "<p style='color:#4caf50;'>✅ $renamed فایل متنی اصلاح شد.</p>";
}

// 2. انتقال جفت‌های صحیح به format3
if (isset($_POST['move_valid_pairs'])) {
    $textFiles = findFiles($format2Dir, ['txt', 'text']);
    $imageFiles = findFiles($format2Dir, ['jpg', 'jpeg', 'png', 'webp', 'gif']);

    // ساخت نقشه متنی: نام فایل بدون پسوند => فایل
    $textMap = [];
    foreach ($textFiles as $txt) {
        $basename = pathinfo($txt['name'], PATHINFO_FILENAME);
        $textMap[$basename] = $txt;
    }

    $moved = 0;
    foreach ($imageFiles as $img) {
        $imgBasename = pathinfo($img['name'], PATHINFO_FILENAME);
        // اگر فایل متنی با همان نام پایه وجود داشت
        if (isset($textMap[$imgBasename])) {
            $txtFile = $textMap[$imgBasename];

            // مقصد
            $imgTarget = $format3Dir . DIRECTORY_SEPARATOR . $img['name'];
            $txtTarget = $format3Dir . DIRECTORY_SEPARATOR . $txtFile['name'];

            // جابجایی
            if (rename($img['path'], $imgTarget) && rename($txtFile['path'], $txtTarget)) {
                $moved++;
                // جلوگیری از انتقال مجدد همان فایل متنی
                unset($textMap[$imgBasename]);
            }
        }
    }

    $message2 = "<p style='color:#2196f3;'>📤 $moved جفت به format3 منتقل شد.</p>";
}

// خواندن فایل‌ها برای نمایش جفت‌های نامتناظر
$textFiles = findFiles($format2Dir, ['txt', 'text']);
$imageFiles = findFiles($format2Dir, ['jpg', 'jpeg', 'png', 'webp', 'gif']);

$groups = [];
foreach ($imageFiles as $img) {
    [$base, $num] = extractBaseAndNumber($img['name']);
    $key = $base . ($num !== '' ? $num : '');
    if (!isset($groups[$key])) $groups[$key] = ['images' => [], 'texts' => []];
    $groups[$key]['images'][] = $img['name'];
}
foreach ($textFiles as $txt) {
    [$base, $num] = extractBaseAndNumber($txt['name']);
    $key = $base . ($num !== '' ? $num : '');
    if (!isset($groups[$key])) $groups[$key] = ['images' => [], 'texts' => []];
    $groups[$key]['texts'][] = $txt['name'];
}

$mismatched = [];
foreach ($groups as $key => $group) {
    if (!empty($group['images']) && !empty($group['texts'])) {
        $allNames = array_merge($group['images'], $group['texts']);
        $nums = [];
        foreach ($allNames as $name) {
            [$_, $n] = extractBaseAndNumber($name);
            $nums[] = $n;
        }
        $uniqueNums = array_unique($nums);
        if (count($uniqueNums) > 1) {
            $mismatched[$key] = $group;
        }
    }
}
?>
<!DOCTYPE html>
<html lang="fa">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>مدیریت جفت‌ها — format2 به format3</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: Tahoma, Arial, sans-serif; background: #121212; color: #e0e0e0; direction: rtl; padding: 20px; }
        .container { max-width: 1000px; margin: 0 auto; }
        .header { text-align: center; margin-bottom: 25px; }
        .btn-section {
            display: flex;
            flex-wrap: wrap;
            justify-content: center;
            gap: 15px;
            margin: 20px 0;
        }
        .btn {
            padding: 12px 24px;
            font-size: 16px;
            color: white;
            border: none;
            border-radius: 6px;
            cursor: pointer;
            transition: background 0.3s;
        }
        .normalize-btn { background: #673ab7; }
        .normalize-btn:hover { background: #512da8; }
        .move-btn { background: #0288d1; }
        .move-btn:hover { background: #0277bd; }
        .message {
            text-align: center;
            margin: 15px 0;
            min-height: 24px;
        }
        .section { margin: 25px 0; }
        .section h2 {
            color: #90a4ae;
            margin-bottom: 15px;
            padding-bottom: 8px;
            border-bottom: 1px solid #333;
        }
        .pair-item {
            background: #1e1e1e;
            padding: 15px;
            margin: 10px 0;
            border-radius: 8px;
            display: flex;
            flex-wrap: wrap;
            gap: 15px;
            align-items: center;
        }
        .file-list {
            list-style: none;
            padding: 0;
        }
        .file-item {
            padding: 8px 0;
            font-family: monospace;
        }
        .text-file { color: #4caf50; }
        .image-file { color: #2196f3; }
    </style>
</head>
<body>
<div class="container">
    <div class="header">
        <h1>مدیریت جفت‌های متن و تصویر</h1>
    </div>

    <div class="btn-section">
        <form method="post">
            <button type="submit" name="normalize_names" class="btn normalize-btn">یکسان‌سازی نام‌ها</button>
        </form>
        <form method="post">
            <button type="submit" name="move_valid_pairs" class="btn move-btn">انتقال جفت‌های صحیح به format3</button>
        </form>
    </div>

    <?php if (isset($message) || isset($message2)): ?>
    <div class="message">
        <?php echo $message ?? ''; ?>
        <?php echo $message2 ?? ''; ?>
    </div>
    <?php endif; ?>

    <?php if (!empty($mismatched)): ?>
    <div class="section">
        <h2>جفت‌های نامتناظر (نیاز به یکسان‌سازی)</h2>
        <?php foreach ($mismatched as $key => $group): ?>
        <div class="pair-item">
            <div>
                <div><strong>تصاویر:</strong></div>
                <ul class="file-list">
                    <?php foreach ($group['images'] as $img): ?>
                    <li class="file-item image-file">🖼️ <?php echo htmlspecialchars($img); ?></li>
                    <?php endforeach; ?>
                </ul>
            </div>
            <div>
                <div><strong>فایل‌های متنی:</strong></div>
                <ul class="file-list">
                    <?php foreach ($group['texts'] as $txt): ?>
                    <li class="file-item text-file">📄 <?php echo htmlspecialchars($txt); ?></li>
                    <?php endforeach; ?>
                </ul>
            </div>
        </div>
        <?php endforeach; ?>
    </div>
    <?php else: ?>
    <div class="section">
        <p style="text-align:center; color:#9e9e9e;">هیچ جفت نامتناظری وجود ندارد.</p>
    </div>
    <?php endif; ?>
</div>
</body>
</html>