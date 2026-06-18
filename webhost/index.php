<?php
require_once 'config.php';

// دیباگ: شمارش همه فایل‌های پوشه
$all_files = array_diff(scandir(GALLERY_PATH), ['.', '..']);
$total_all_files = count($all_files);

// دیباگ: شمارش فقط فایل‌های تصویری
$images = get_gallery_images();
$total_images = count($images);

$today = date('Y-m-d');
$today_images = 0;
foreach ($images as $img) {
    $file_date = date('Y-m-d', filemtime(GALLERY_PATH . $img));
    if ($file_date === $today) $today_images++;
}

$latest_file = '';
if (!empty($images)) {
    usort($images, fn($a, $b) => filemtime(GALLERY_PATH . $b) - filemtime(GALLERY_PATH . $a));
    $latest_file = htmlspecialchars($images[0], ENT_QUOTES, 'UTF-8');
}
?>
<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>گالری آپلود عکس</title>
<style>
:root{
  --bg:#0f1724;
  --card:#0b1220;
  --accent1:#f97316;
  --accent2:#7c3aed;
  --muted: rgba(255,255,255,0.08);
  --glass: rgba(255,255,255,0.03);
}
*{box-sizing:border-box;margin:0;padding:0}
body{
  font-family:'Segoe UI',Tahoma,Arial,sans-serif;
  min-height:100vh;
  background:
    radial-gradient(1000px 600px at 10% 10%, rgba(124,58,237,0.08), transparent 10%),
    radial-gradient(900px 500px at 90% 90%, rgba(249,115,22,0.06), transparent 10%),
    var(--bg);
  color:#fff;
  display:flex;
  flex-direction:column;
  align-items:center;
  justify-content:flex-start;
  padding:40px 16px;
}
h1{
  margin-bottom:32px;
  font-size:2rem;
  color: #e2e8f0; /* بدون گرادیانت */
  text-align:center;
}
.btn{
  display:flex;
  justify-content: space-between;
  align-items: center;
  margin:10px auto;
  padding:14px 22px;
  width:92%;
  max-width:420px;
  text-decoration:none;
  color:#fff;
  font-size:16px;
  font-weight:600;
  border-radius:14px;
  border:1px solid rgba(255,255,255,0.08);
  background:linear-gradient(90deg,rgba(255,255,255,0.05),rgba(255,255,255,0.02));
  box-shadow:0 8px 20px rgba(2,6,23,0.7);
  backdrop-filter:blur(12px);
  transition:all .25s ease;
  position: relative;
}
.btn:hover{
  background:rgba(255,255,255,0.1);
  box-shadow:0 0 12px rgba(255,255,255,0.1);
  transform:translateY(-1px);
}
/* ============ حذف چشمک ابی ============ */
.btn:active {
  background:linear-gradient(90deg,rgba(255,255,255,0.05),rgba(255,255,255,0.02)) !important;
  box-shadow:0 8px 20px rgba(2,6,23,0.7) !important;
  transform:translateY(0) !important;
  outline: none !important;
  border:1px solid rgba(255,255,255,0.08) !important;
}
/* ==================================== */
.section-title {
  width: 92%;
  max-width: 420px;
  text-align: center;
  margin: 30px 0 10px 0;
  color: #aaa;
  font-size: 14px;
  border-bottom: 1px solid rgba(255,255,255,0.1);
  padding-bottom: 5px;
}
.stats{
  margin-top:40px;
  padding:20px;
  width:92%;
  max-width:420px;
  background:rgba(255,255,255,0.03);
  backdrop-filter:blur(12px);
  border-radius:14px;
  border:1px solid rgba(255,255,255,0.08);
  box-shadow:0 8px 20px rgba(2,6,23,0.7);
  font-size:15px;
  text-align:center;
}
.stats h2{
  font-size:17px;
  margin-bottom:12px;
  color: #e2e8f0; /* بدون گرادیانت */
}
.highlight{
  font-weight:bold;
  color:#ffb300;
}
@media(max-width:480px){
  h1{font-size:1.6rem}
  .btn{font-size:15px}
}
</style>
</head>
<body>
  <h1>🖼️ گالری آپلود عکس</h1>

  <!-- دکمه‌های پرکاربرد -->
  <div class="section-title">پرکاربرد</div>
  <a href="upload-local.php" class="btn">
    <span>📂 آپلود محلی</span>
  </a>
  <a href="gallery-view.php" class="btn">
    <span>🖼️ گالری عکس‌های آپلود شده</span>
  </a>
  <a href="random-gallery.php" class="btn">
    <span>🎲 گالری تصادفی</span>
  </a>
  <a href="naming-manager.php" class="btn">
    <span>🏷️ مدیریت نام‌گذاری فایل‌ها</span>
  </a>

  <!-- دکمه‌های متوسط -->
  <div class="section-title">کاربرد متوسط</div>
  <a href="daily-uploads.php" class="btn">
    <span>📅 عکس‌های روز (بر اساس تاریخ امروز)</span>
  </a>
  <a href="tel.php" class="btn">
    <span>📞 ارسال به تلگرام</span>
  </a>

  <!-- دکمه‌های کم‌کاربرد -->
  <div class="section-title">کم‌کاربرد</div>
  <a href="manage-files.php" class="btn">
    <span>🗂️ مدیریت فایل‌های گالری</span>
  </a>
  <a href="zip-manager.php" class="btn">
    <span>📦 مدیریت فایل‌های ZIP</span>
  </a>
  <a href="fix.php" class="btn">
    <span>🔧 مدیریت فایل‌های گالری (Fix)</span>
  </a>

<!-- فقط این بخش رو جایگزین کن -->  
 <?php include 'menu.php'; ?>
  <div class="stats">
    <h2>📊 وضعیت فایل‌ها</h2>
    <p>کل فایل‌های پوشه: <span class="highlight"><?= $total_all_files ?></span></p>
    <p>فایل‌های تصویری معتبر: <span class="highlight"><?= $total_images ?></span></p>
    <p>فایل‌های امروز: <span class="highlight"><?= $today_images ?></span></p>
    <?php if ($latest_file): ?>
      <p>آخرین فایل: <span class="highlight"><?= $latest_file ?></span></p>
    <?php else: ?>
      <p>هیچ فایلی وجود ندارد.</p>
    <?php endif; ?>
  </div>
</body>
</html>