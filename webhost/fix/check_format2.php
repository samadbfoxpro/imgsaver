<?php
$projectDir = __DIR__;
$unorganizedDir = $projectDir . DIRECTORY_SEPARATOR . 'unorganized';
$format1Dir = $projectDir . DIRECTORY_SEPARATOR . 'format1';
$format2Dir = $projectDir . DIRECTORY_SEPARATOR . 'unorganized2';

if (!is_dir($format1Dir)) {
    mkdir($format1Dir, 0755, true);
}
if (!is_dir($format2Dir)) {
    mkdir($format2Dir, 0755, true);
}

function findTextFiles($dir) {
    $result = [];
    if (!is_dir($dir)) return $result;
    $rii = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($dir));
    foreach ($rii as $file) {
        if ($file->isDir()) continue;
        $ext = strtolower($file->getExtension());
        if ($ext === 'txt' || $ext === 'text') {
            $result[] = [
                'name' => $file->getFilename(),
                'path' => $file->getPathname(),
                'size' => $file->getSize(),
                'modified' => date('Y-m-d H:i', $file->getMTime())
            ];
        }
    }
    return $result;
}

function analyzeContent($content) {
    $lines = preg_split('/\r?\n/', trim($content));
    $lines = array_reverse($lines);
    $index = 0;

    $negative = '';
    $positive = '';
    $description = '';

    // پیدا کردن بخش نگاتیو
    while ($index < count($lines)) {
        $line = trim($lines[$index]);
        if (empty($line)) {
            $index++;
            continue;
        }
        if (substr_count($line, ',') >= 3) {
            $negative = $line;
            break;
        } else {
            $description .= $line . "\n";
        }
        $index++;
    }

    // بخش مثبت بعد از نگاتیو
    $positiveStartIndex = $index + 1;
    while ($positiveStartIndex < count($lines)) {
        $line = trim($lines[$positiveStartIndex]);
        if (!empty($line)) {
            $positive .= $line . "\n";
        }
        $positiveStartIndex++;
    }

    return [
        'negative' => trim($negative),
        'positive' => trim($positive),
        'description' => trim($description)
    ];
}

if (isset($_POST['process_to_format1'])) {
    $files = findTextFiles($unorganizedDir);
    foreach ($files as $file) {
        $content = file_get_contents($file['path']);
        $result = analyzeContent($content);
        $positiveWords = count(array_filter(preg_split('/\s+/', $result['positive'])));
        $negativeWords = count(array_filter(preg_split('/\s+/', $result['negative'])));
        
        if ($positiveWords > $negativeWords && !empty($result['positive']) && !empty($result['negative'])) {
            $newFileName = $format1Dir . DIRECTORY_SEPARATOR . $file['name'];
            file_put_contents($newFileName, $content);
            unlink($file['path']);
        }
    }
}

if (isset($_POST['process_to_format2'])) {
    $files = findTextFiles($unorganizedDir);
    foreach ($files as $file) {
        $content = file_get_contents($file['path']);
        $result = analyzeContent($content);
        $newContent = "Positive Prompt:\n" . $result['positive'] . "\n\nNegative Prompt:\n" . $result['negative'] . "\n\nDescription:\n" . $result['description'];
        $newFileName = $format2Dir . DIRECTORY_SEPARATOR . $file['name'];
        file_put_contents($newFileName, $newContent);
        unlink($file['path']);
    }
}

$message = '';
if (isset($_POST['move_single_to_format1'])) {
    $fileName = isset($_POST['file_name']) ? $_POST['file_name'] : '';
    $fileName = basename($fileName);
    $sourcePath = $format2Dir . DIRECTORY_SEPARATOR . $fileName;
    if (!file_exists($sourcePath)) {
        $message = "فایل مورد نظر پیدا نشد: " . htmlspecialchars($fileName);
    } else {
        $content = file_get_contents($sourcePath);
        $result = analyzeContent($content);
        $positiveWords = count(array_filter(preg_split('/\s+/', $result['positive'])));
        $negativeWords = count(array_filter(preg_split('/\s+/', $result['negative'])));
        if (!empty($result['positive']) && !empty($result['negative']) && $positiveWords > $negativeWords) {
            $destPath = $format1Dir . DIRECTORY_SEPARATOR . $fileName;
            if (rename($sourcePath, $destPath)) {
                header('Location: ' . $_SERVER['REQUEST_URI']);
                exit;
            } else {
                $message = "خطا در انتقال فایل: " . htmlspecialchars($fileName);
            }
        } else {
            $message = "شرایط انتقال برقرار نیست: " . htmlspecialchars($fileName);
        }
    }
}

if (isset($_POST['batch_move_to_format1'])) {
    $files = findTextFiles($format2Dir);
    $errors = [];
    foreach ($files as $file) {
        $content = file_get_contents($file['path']);
        $result = analyzeContent($content);
        $positiveWords = count(array_filter(preg_split('/\s+/', $result['positive'])));
        $negativeWords = count(array_filter(preg_split('/\s+/', $result['negative'])));
        if (!empty($result['positive']) && !empty($result['negative']) && $positiveWords > $negativeWords) {
            $destPath = $format1Dir . DIRECTORY_SEPARATOR . $file['name'];
            if (!rename($file['path'], $destPath)) {
                $errors[] = $file['name'];
            }
        }
    }
    if (!empty($errors)) {
        $message = "خطا در انتقال فایل‌های زیر: " . implode(', ', $errors);
    }
}

$unorganizedFiles = findTextFiles($unorganizedDir);
$format1Files = findTextFiles($format1Dir);
$format2Files = findTextFiles($format2Dir);
?>
<!DOCTYPE html>
<html lang="fa">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>مدیریت فایل‌های متنی</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: Tahoma, Arial, sans-serif; background: #121212; color: #e0e0e0; direction: rtl; }
        .container { width: 90%; max-width: 1200px; margin: 30px auto; background: #1e1e1e; padding: 20px; border-radius: 12px; box-shadow: 0 4px 16px rgba(0,0,0,0.5); }
        .header { text-align: center; margin-bottom: 20px; }
        .nav { display: flex; flex-wrap: wrap; justify-content: center; gap: 15px; margin-bottom: 20px; padding: 10px; }
        .nav a { color: #64b5f6; text-decoration: none; padding: 8px 16px; border-radius: 6px; background: #2d2d2d; transition: background 0.3s; }
        .nav a:hover { background: #3d3d3d; }
        .btn-group { display: flex; flex-wrap: wrap; justify-content: center; gap: 10px; margin: 15px 0; }
        .update-btn { padding: 10px 20px; font-size: 14px; background: #43a047; color: white; border: none; border-radius: 6px; cursor: pointer; transition: background 0.3s; }
        .update-btn:hover { background: #2e7d32; }
        .format1-btn { background: #1976d2; }
        .format1-btn:hover { background: #1565c0; }
        .file-btn { padding: 6px 12px; font-size: 12px; background: #1976d2; color: white; border: none; border-radius: 4px; cursor: pointer; }
        .file-btn:hover { background: #1565c0; }
        .message { width: 90%; max-width: 800px; margin: 15px auto; padding: 12px; background: #43a047; color: white; border-radius: 6px; text-align: center; }
        .section { margin: 20px 0; }
        .section h2 { color: #90a4ae; margin: 15px 0; padding-bottom: 8px; border-bottom: 1px solid #333; }
        .file-list { list-style: none; padding: 0; }
        .file-item { padding: 10px; margin: 8px 0; background: #2b2b2b; border-radius: 6px; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 10px; }
        .file-info { display: flex; align-items: center; gap: 10px; flex: 1; min-width: 200px; }
        .file-icon { color: #64b5f6; }
        .file-name { font-weight: bold; }
        .file-size { color: #9e9e9e; font-size: 12px; }
        .file-actions { display: flex; gap: 8px; }
        @media (max-width: 768px) {
            .file-item { flex-direction: column; align-items: stretch; }
            .file-actions { justify-content: center; }
            .btn-group { flex-direction: column; align-items: center; }
            .update-btn { width: 100%; max-width: 250px; }
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>مدیریت فایل‌های متنی</h1>
        </div>
        <div class="nav">
            <button onclick="window.history.back();" class="update-btn" style="background:#1976d2; color:white; border:none; border-radius:6px; padding:10px 20px; cursor:pointer;">بازگشت به صفحه قبل</button>
        </div>
        <div class="btn-group">
            <form method="post">
                <button type="submit" name="process_to_format1" class="update-btn format1-btn">انتقال به فرمت 1</button>
            </form>
            <form method="post">
                <button type="submit" name="process_to_format2" class="update-btn">پردازش به unorganized2</button>
            </form>
        </div>
        <?php if (!empty($message)): ?>
            <div class="message"><?php echo $message; ?></div>
        <?php endif; ?>
        <div class="section">
            <h2>فایل‌های unorganized</h2>
            <ul class="file-list">
                <?php foreach ($unorganizedFiles as $file): ?>
                <li class="file-item">
                    <div class="file-info">
                        <span class="file-icon">📄</span>
                        <span class="file-name"><?php echo htmlspecialchars($file['name']); ?></span>
                        <span class="file-size">(<?php echo $file['size']; ?> bytes)</span>
                    </div>
                </li>
                <?php endforeach; ?>
            </ul>
        </div>
        <div class="btn-group">
            <form method="post">
                <button type="submit" name="batch_move_to_format1" class="update-btn format1-btn">انتقال جمعی به فرمت 1</button>
            </form>
        </div>
        <div class="section">
            <h2>فایل‌های format1</h2>
            <ul class="file-list">
                <?php foreach ($format1Files as $file): ?>
                <li class="file-item">
                    <div class="file-info">
                        <span class="file-icon">📄</span>
                        <span class="file-name"><?php echo htmlspecialchars($file['name']); ?></span>
                        <span class="file-size">(<?php echo $file['size']; ?> bytes)</span>
                    </div>
                </li>
                <?php endforeach; ?>
            </ul>
        </div>
        <div class="section">
            <h2>فایل‌های unorganized2</h2>
            <ul class="file-list">
                <?php foreach ($format2Files as $file): ?>
                <li class="file-item">
                    <div class="file-info">
                        <span class="file-icon">📄</span>
                        <span class="file-name"><?php echo htmlspecialchars($file['name']); ?></span>
                        <span class="file-size">(<?php echo $file['size']; ?> bytes)</span>
                    </div>
                    <div class="file-actions">
                        <form method="post" style="display:inline-block;">
                            <input type="hidden" name="file_name" value="<?php echo htmlspecialchars($file['name']); ?>">
                            <button type="submit" name="move_single_to_format1" class="file-btn">انتقال به فرمت 1</button>
                        </form>
                    </div>
                </li>
                <?php endforeach; ?>
            </ul>
        </div>
    </div>
</body>
</html>