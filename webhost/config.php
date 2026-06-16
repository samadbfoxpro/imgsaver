<?php
// در ابتدای config.php
defined('PROJECT_ROOT') or define('PROJECT_ROOT', __DIR__);
/**
 * config.php — تنظیمات مرکزی پروژه
 * مسیر عکس‌ها برای محیط موبایل (KSWeb)
 */

// ⚠️ مسیر فیزیکی پوشهٔ عکس‌ها در گوشی شما
// این مسیر باید دقیقاً مثل سیستم‌فایل گوشی باشه
// مسیر گالری برای محیط لوکال (xampp)
define('GALLERY_PATH', __DIR__ . '/uploads/local/');
// مسیر گالری لوکال برای فایل‌های خاص
define('GALLERY_LOCAL_PATH', __DIR__ . '/uploads/local/');

// لینک صفحه گالری عکس
define('GALLERY_PAGE_URL', '/telegram/index.php');
// لینک صفحه گالری متن (در صورت وجود)
define('TEXT_GALLERY_PAGE_URL', '/text-proxy.php');

// ✅ بررسی وجود پوشه (اختیاری — فقط برای دیباگ)
if (!defined('SKIP_CONFIG_CHECK') && !is_dir(GALLERY_PATH)) {
    die('<h2 style="color:red; direction:rtl; text-align:center;">❌ خطا: پوشهٔ گالری پیدا نشد!</h2>' .
        '<p style="direction:rtl; text-align:center;">مسیر: ' . htmlspecialchars(GALLERY_PATH) . '</p>' .
        '<p style="direction:rtl; text-align:center;">لطفاً مطمئن شوید KSWeb مجوز دسترسی به فایل‌ها را دارد.</p>');
}

// پسوندهای مجاز برای عکس
define('ALLOWED_IMAGE_EXTENSIONS', ['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp']);

// تابع کمکی: آیا فایل، عکس معتبر است؟
function is_valid_image($filename) {
    $ext = strtolower(pathinfo($filename, PATHINFO_EXTENSION));
    return in_array($ext, ALLOWED_IMAGE_EXTENSIONS);
}

// تابع کمکی: دریافت لیست عکس‌های معتبر از گالری
function get_gallery_images() {
    $images = [];
    if (is_dir(GALLERY_PATH)) {
        $files = scandir(GALLERY_PATH);
        foreach ($files as $file) {
            if ($file === '.' || $file === '..') continue;
            $full_path = GALLERY_PATH . $file;
            if (is_file($full_path) && is_valid_image($file)) {
                $images[] = $file;
            }
        }
    }
    return $images;
}
// در config.php — تابع برای خواندن فایل‌های متنی (prompt)
function read_prompt_file($text_path) {
    $positive = '---';
    $negative = '---';
    if (file_exists($text_path)) {
        $content = file_get_contents($text_path);
        if (preg_match('/Positive Prompt\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $p_matches)) {
            $positive = trim($p_matches[1]);
        } else {
            $lines = explode("\n", $content);
            $positive = trim($lines[0] ?? '---');
        }
        if (preg_match('/Negative Prompt\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $n_matches)) {
            $negative = trim($n_matches[1]);
        } else {
            $lines = explode("\n", $content);
            $negative = trim($lines[1] ?? '---');
        }
    }
    return [$positive, $negative];
}