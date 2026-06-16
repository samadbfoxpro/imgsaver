<?php
$unorganizedDir = __DIR__ . DIRECTORY_SEPARATOR . 'unorganized';
$result = [];

function removePrompts($text) {
    $text = str_replace('Positive Prompt:', '', $text);
    $text = str_replace('Negative Prompt:', '', $text);
    $text = str_replace('Description:', '', $text);
    return $text;
}

if (isset($_POST['run'])) {
    $rii = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($unorganizedDir));
    foreach ($rii as $file) {
        if ($file->isDir()) continue;
        $content = file_get_contents($file->getPathname());
        $newContent = removePrompts($content);
        file_put_contents($file->getPathname(), $newContent);
        $result[] = $file->getFilename();
    }
}
?>
<!DOCTYPE html>
<html lang="fa">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>حذف عبارت‌های Prompt</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: Tahoma, Arial, sans-serif; background: #121212; color: #e0e0e0; direction: rtl; }
        .container { width: 90%; max-width: 800px; margin: 30px auto; background: #1e1e1e; padding: 25px; border-radius: 12px; box-shadow: 0 4px 16px rgba(0,0,0,0.5); }
        .header { text-align: center; margin-bottom: 25px; }
        .nav { display: flex; justify-content: center; gap: 15px; margin-bottom: 20px; padding: 10px; }
        .nav a { color: #64b5f6; text-decoration: none; padding: 8px 16px; border-radius: 6px; background: #2d2d2d; transition: background 0.3s; }
        .nav a:hover { background: #3d3d3d; }
        .run-btn { display: block; margin: 20px auto; padding: 12px 30px; font-size: 16px; background: #1976d2; color: white; border: none; border-radius: 6px; cursor: pointer; transition: background 0.3s; }
        .run-btn:hover { background: #1565c0; }
        .result { margin-top: 20px; }
        .result h3 { color: #90a4ae; margin-bottom: 15px; }
        .result ul { list-style: none; padding: 0; }
        .result li { padding: 10px; margin: 5px 0; background: #2b2b2b; border-radius: 6px; }
        @media (max-width: 768px) {
            .nav { flex-direction: column; align-items: center; }
            .run-btn { width: 100%; max-width: 250px; }
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>حذف عبارت‌های Prompt</h1>
        </div>
        <div class="nav">
                        <button onclick="window.history.back();" class="update-btn" style="background:#1976d2; color:white; border:none; border-radius:6px; padding:10px 20px; cursor:pointer;">بازگشت به صفحه قبل</button>

        </div>
        <form method="post">
            <button type="submit" name="run" class="run-btn">حذف از همه فایل‌های unorganized</button>
        </form>
        <?php if (!empty($result)): ?>
            <div class="result">
                <h3>عبارت‌ها از فایل‌های زیر حذف شدند:</h3>
                <ul>
                    <?php foreach ($result as $fname): ?>
                        <li>📄 <?php echo htmlspecialchars($fname); ?></li>
                    <?php endforeach; ?>
                </ul>
            </div>
        <?php endif; ?>
    </div>
</body>
</html>