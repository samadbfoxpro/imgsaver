<?php
try {
    $folder = __DIR__ . '/fan';       // مسیر پوشه
    $tarFile = __DIR__ . '/fan.tar';  // خروجی tar
    $gzFile  = __DIR__ . '/fan.tar.gz'; // خروجی tar.gz

    // ساخت آرشیو tar
    $phar = new PharData($tarFile);
    $phar->buildFromDirectory($folder);

    // تبدیل tar به tar.gz
    if (file_exists($gzFile)) {
        unlink($gzFile); // اگر قبلاً ساخته شده حذف بشه
    }
    $phar->compress(Phar::GZ);

    echo "✅ فایل‌ها ساخته شدند:<br>";
    echo "tar: $tarFile<br>";
    echo "tar.gz: $gzFile<br>";

} catch (Exception $e) {
    echo "❌ خطا: " . $e->getMessage();
}
