<?php
$log_file = __DIR__ . '/server.log';

if (file_exists($log_file)) {
    // خواندن خطوط فایل و حذف خطوط خالی
    $lines = file($log_file, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);

    echo "<!DOCTYPE html>
    <html lang='fa'>
    <head>
        <meta charset='UTF-8'>
        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        <title>مشاهده لاگ سرور</title>
        <style>
            body { font-family: monospace; background:#1e1e2f; color:#d4d4d4; padding:10px; }
            pre { background:#252526; color:#9cdcfe; padding:10px; border-radius:5px; overflow-x:auto; }
            h1 { color:#569cd6; }
        </style>
    </head>
    <body>
        <h1>لاگ سرور PHP</h1>
        <pre>";
    
    foreach($lines as $line) {
        echo htmlspecialchars($line) . "\n";
    }

    echo "</pre>
    </body>
    </html>";
} else {
    echo "لاگ هنوز ایجاد نشده.";
}
?>