<?php
// telegram-gallery.php (ادغام گالری و ارسال)

require_once __DIR__ . '/../config.php';

// تابع خواندن اطلاعات از فایل .txt (همان تابعی که در gallery-logic.php تعریف کردیم)
if (!function_exists('read_image_metadata')) {
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
}

// بررسی وجود پوشه
if (!is_dir(GALLERY_PATH)) {
    die('<h2 style="color:red; direction:rtl; text-align:center;">❌ خطا: پوشهٔ گالری وجود ندارد!</h2>' .
        '<p style="direction:rtl; text-align:center;">مسیر: ' . htmlspecialchars(GALLERY_PATH) . '</p>');
}

// گرفتن لیست فایل‌ها و مرتب‌سازی بر اساس زمان ایجاد (جدیدترین اول)
$images = get_gallery_images();
usort($images, function($a, $b) {
    return filemtime(GALLERY_PATH . $b) - filemtime(GALLERY_PATH . $a);
});

// فایل JSON برای ثبت ارسال‌های موفق و خطاها
$upload_log_file = __DIR__ . '/1upload_log.json';
$upload_log = [];
if (file_exists($upload_log_file)) {
    $content = file_get_contents($upload_log_file);
    $upload_log = json_decode($content, true) ?: [];
}

// جدا کردن فایل‌ها به سه دسته
$not_sent = [];
$failed = [];
$sent = [];

foreach ($images as $img) {
    if (isset($upload_log[$img])) {
        // بررسی وضعیت با استفاده از اپراتور null coalescing
        $status = $upload_log[$img]['status'] ?? null;

        if ($status === 'sent') {
            $sent[] = $img;
        } elseif ($status === 'failed') {
            $failed[] = $img;
        } else {
            // اگر status وجود نداشت یا مقدار دیگری بود، به عنوان عدم ارسال در نظر گرفته می‌شود
            $not_sent[] = $img;
        }
    } else {
        $not_sent[] = $img;
    }
}

// ادغام فایل‌ها: اول عدم ارسال، سپس خطا، سپس ارسال‌شده
$ordered_images = array_merge($not_sent, $failed, $sent);

// صفحه‌بندی
$total = count($ordered_images);
$page = max(1, (int)($_GET['page'] ?? 1));
$per_page = 50;
$offset = ($page - 1) * $per_page;
$sliced_images = array_slice($ordered_images, $offset, $per_page);
$pages_count = ceil($total / $per_page);

// جدا کردن فقط فایل‌هایی که هیچ‌بار امتحان نشده‌اند (نه ارسال شده، نه خطا داشته) از صفحه فعلی برای عملیات "ارسال همه"
$current_page_not_sent = [];
foreach ($sliced_images as $img) {
    // فقط فایل‌هایی که در upload_log وجود ندارند، جز لیست می‌شوند
    if (!isset($upload_log[$img])) {
        $current_page_not_sent[] = $img;
    }
}

// اگر درخواست POST بود، عملیات ارسال اجرا می‌شه
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $action = $_POST['action'] ?? '';

    if ($action === 'send_all') {
        // پاسخ JSON برای "ارسال همه"
        header('Content-Type: application/json; charset=utf-8');
        echo json_encode(['success' => true, 'files' => $current_page_not_sent]);
        exit;
    }

    // این بخش برای ارسال یک فایل است
    $filename = trim($_POST['filename'] ?? '');
    if (empty($filename)) {
        echo json_encode(['success' => false, 'message' => 'نام فایل الزامی است.']);
        exit;
    }

    $image_path = GALLERY_PATH . $filename;

    if (!file_exists($image_path)) {
        echo json_encode(['success' => false, 'message' => 'فایل عکس وجود ندارد.']);
        exit;
    }

    $name = pathinfo($filename, PATHINFO_FILENAME);
    $txt_path = GALLERY_PATH . $name . '.txt';

    // اگر فایل متنی نبود، فقط عکس ارسال می‌شود
    $metadata = [];
    if (file_exists($txt_path)) {
        $metadata = read_image_metadata($image_path);
    }

    // اطلاعات ثابت
    $bot_token = '8316601241:AAF-CyP-W0ZWp7N514RABD_WQVGm41PrGkk';
    $chat_id = '-1003188537557';

    // ارسال عکس
    $url = "https://api.telegram.org/bot{$bot_token}/sendPhoto";
    $ch = curl_init();
    curl_setopt_array($ch, [
        CURLOPT_URL => $url,
        CURLOPT_POST => true,
        CURLOPT_POSTFIELDS => [
            'chat_id' => $chat_id,
            'photo' => new CURLFile($image_path),
            'caption' => '' // بدون کپشن
        ],
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_TIMEOUT => 60
    ]);

    $response = curl_exec($ch);
    $http_code = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    $error = curl_error($ch);
    curl_close($ch);

    if ($error) {
        $upload_log[$filename] = [
            'timestamp' => date('Y-m-d H:i:s'),
            'error' => "خطای cURL: $error",
            'status' => 'failed'
        ];
        file_put_contents($upload_log_file, json_encode($upload_log, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
        echo json_encode(['success' => false, 'message' => "خطای cURL: $error"]);
        exit;
    }

    if ($http_code !== 200) {
        // چک کردن پیام مسدود شده توسط NTA
        if (strpos($response, 'Nepal Telecommunication Authority') !== false ||
            strpos($response, 'Civil Criminal Code') !== false ||
            strpos($response, 'Government of Nepal') !== false) {
            $upload_log[$filename] = [
                'timestamp' => date('Y-m-d H:i:s'),
                'error' => '❌ دسترسی به api.telegram.org مسدود است (NTA).',
                'status' => 'failed'
            ];
            file_put_contents($upload_log_file, json_encode($upload_log, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
            echo json_encode(['success' => false, 'message' => '❌ دسترسی به api.telegram.org مسدود است (NTA).']);
            exit;
        }
        $upload_log[$filename] = [
            'timestamp' => date('Y-m-d H:i:s'),
            'error' => "کد HTTP: $http_code",
            'status' => 'failed'
        ];
        file_put_contents($upload_log_file, json_encode($upload_log, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
        echo json_encode(['success' => false, 'message' => "کد HTTP: $http_code"]);
        exit;
    }

    $result = json_decode($response, true);
    if (!($result['ok'] ?? false)) {
        $upload_log[$filename] = [
            'timestamp' => date('Y-m-d H:i:s'),
            'error' => 'ارسال عکس ناموفق: ' . ($result['description'] ?? 'پاسخ نامشخص'),
            'status' => 'failed'
        ];
        file_put_contents($upload_log_file, json_encode($upload_log, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
        echo json_encode(['success' => false, 'message' => 'ارسال عکس ناموفق: ' . ($result['description'] ?? 'پاسخ نامشخص')]);
        exit;
    }

    // دریافت ID پیام ارسال شده (عکس)
    $message_id = $result['result']['message_id'] ?? null;


// 📨 ارسال متن به عنوان پیام جدید (بعد از ارسال عکس)
$text = "<b>📸 Image Info</b>\n"
      . "🖼️ Name: <code>{$name}</code>\n"
      . "💾 Type: ." . pathinfo($filename, PATHINFO_EXTENSION) . "\n"
      . "⚖️ Size: " . round(filesize($image_path) / 1024, 2) . "KB\n"
      . "🖱️ Click name to copy";

// ذخیره پاسخ ارسال عکس برای بررسی
file_put_contents(__DIR__ . '/telegram_debug_log.txt', $response . PHP_EOL, FILE_APPEND);

$url = "https://api.telegram.org/bot{$bot_token}/sendMessage";
$ch = curl_init();
curl_setopt_array($ch, [
    CURLOPT_URL => $url,
    CURLOPT_POST => true,
    CURLOPT_POSTFIELDS => [
        'chat_id' => $chat_id,
        'text' => $text,
        'parse_mode' => 'HTML', // ✅ به جای Markdown
        // ❌ در کانال‌ها ریپلای مجاز نیست → حذفش کن
        // 'reply_to_message_id' => $message_id 
    ],
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_TIMEOUT => 60
]);

$response = curl_exec($ch);
$http_code = curl_getinfo($ch, CURLINFO_HTTP_CODE);
$error = curl_error($ch);
curl_close($ch);

// ذخیره پاسخ کامل تلگرام برای دیباگ (اختیاری ولی مفید)
file_put_contents(__DIR__ . '/telegram_debug_log.txt', "\n\n---- RESPONSE SENDMESSAGE ----\n" . $response . PHP_EOL, FILE_APPEND);

if ($error) {
    $upload_log[$filename] = [
        'timestamp' => date('Y-m-d H:i:s'),
        'error' => "خطای cURL در ارسال متن: $error",
        'status' => 'failed'
    ];
    file_put_contents($upload_log_file, json_encode($upload_log, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
    echo json_encode(['success' => false, 'message' => "خطای cURL در ارسال متن: $error"]);
    exit;
}

if ($http_code !== 200) {
    if (strpos($response, 'Nepal Telecommunication Authority') !== false ||
        strpos($response, 'Civil Criminal Code') !== false ||
        strpos($response, 'Government of Nepal') !== false) {
        $upload_log[$filename] = [
            'timestamp' => date('Y-m-d H:i:s'),
            'error' => '❌ دسترسی به api.telegram.org مسدود است (NTA).',
            'status' => 'failed'
        ];
        file_put_contents($upload_log_file, json_encode($upload_log, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
        echo json_encode(['success' => false, 'message' => '❌ دسترسی به api.telegram.org مسدود است (NTA).']);
        exit;
    }
    $upload_log[$filename] = [
        'timestamp' => date('Y-m-d H:i:s'),
        'error' => "کد HTTP در ارسال متن: $http_code",
        'status' => 'failed'
    ];
    file_put_contents($upload_log_file, json_encode($upload_log, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
    echo json_encode(['success' => false, 'message' => "کد HTTP در ارسال متن: $http_code"]);
    exit;
}

$result = json_decode($response, true);
if (!($result['ok'] ?? false)) {
    $upload_log[$filename] = [
        'timestamp' => date('Y-m-d H:i:s'),
        'error' => 'ارسال متن ناموفق: ' . ($result['description'] ?? 'پاسخ نامشخص'),
        'status' => 'failed'
    ];
    file_put_contents($upload_log_file, json_encode($upload_log, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
    echo json_encode(['success' => false, 'message' => 'ارسال متن ناموفق: ' . ($result['description'] ?? 'پاسخ نامشخص')]);
    exit;
}



    // ثبت در فایل JSON به عنوان ارسال موفق
    $upload_log[$filename] = [
        'timestamp' => date('Y-m-d H:i:s'),
        'status' => 'sent'
    ];
    file_put_contents($upload_log_file, json_encode($upload_log, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));

    echo json_encode(['success' => true, 'message' => '✅ ارسال موفق!']);
    exit;
}
?>

<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>گالری تلگرام</title>
  <style>
    body {
      font-family: Tahoma, sans-serif;
      margin: 0;
      padding: 10px;
      background-color: #121212;
      color: #e0e0e0;
    }
    .container {
      max-width: 100%;
      margin: 0 auto;
    }
    h2 {
      text-align: center;
      color: #bb86fc;
      font-size: 1.4em;
    }
    .gallery {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); /* اندازه مناسب موبایل */
      gap: 12px;
      padding: 8px;
    }
    .item {
      background: #1e1e1e;
      border-radius: 8px;
      padding: 10px;
      box-shadow: 0 2px 6px rgba(0,0,0,0.3);
      text-align: center;
    }
    .filename {
      word-break: break-word;
      font-size: 0.85em;
      color: #e0e0e0;
      margin-bottom: 8px;
    }
    .controls {
      padding: 8px;
      text-align: center;
    }
    .status {
      font-size: 0.8em;
      padding: 4px;
      margin-top: 4px;
      border-radius: 4px;
    }
    .sent {
      background: #2e7d32;
      color: #c8e6c9;
    }
    .failed {
      background: #f57f17;
      color: #fff9c4;
    }
    .btn-send {
      padding: 6px 10px;
      background: #6200ea;
      color: white;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      width: 100%;
      font-size: 0.85em;
    }
    .btn-send:hover {
      background: #7c4dff;
    }
    .btn-failed {
      background: #f57f17 !important;
      color: #fff9c4;
    }
    .btn-sent {
      background: #43a047 !important;
      cursor: default;
    }
    .progress {
      display: none;
      color: #7c4dff;
      margin-top: 4px;
      font-size: 0.75em;
    }
    .pagination {
      display: flex;
      justify-content: center;
      align-items: center;
      margin-top: 20px;
      gap: 6px;
      flex-wrap: wrap;
    }
    .pagination a, .pagination span {
      padding: 8px 12px;
      text-decoration: none;
      background-color: #1e1e1e;
      color: #bb86fc;
      border-radius: 4px;
      font-size: 0.9em;
    }
    .pagination a:hover {
      background-color: #333;
    }
    .pagination .current {
      background-color: #6200ea;
      color: white;
    }
    .send-all-container {
        text-align: center;
        margin-bottom: 20px;
    }
.btn-send-all {
    padding: 10px 20px;
    background: #00bcd4; /* رنگ آبی فیروزه‌ای */
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 1em;
    font-weight: bold;
}
.btn-send-all:hover {
    background: #0097a7; /* یک نیمکت تیره‌تر از رنگ اصلی */
}
    .btn-send-all:disabled {
        background: #757575;
        cursor: not-allowed;
    }
    .overall-progress {
        margin-top: 10px;
        font-size: 0.9em;
        color: #bb86fc;
        text-align: center;
    }
    @media (max-width: 600px) {
      .gallery {
        grid-template-columns: repeat(2, 1fr); /* فقط 2 تا در هر ردیف در موبایل */
      }
      h2 {
        font-size: 1.3em;
      }
      .pagination a, .pagination span {
        padding: 6px 10px;
        font-size: 0.85em;
      }
      .btn-send-all {
        padding: 8px 16px;
        font-size: 0.95em;
      }
    }
  </style>
</head>
<body>
        <?php include 'sidebar.php'; ?>
<div class="container">
  <h2>گالری عکس‌ها — ارسال به تلگرام</h2>

  <!-- دکمه ارسال همه -->
  <div class="send-all-container">
      <button id="btnSendAll" class="btn-send-all" onclick="sendAll()">📤 ارسال همه (ارسال نشده‌های صفحه)</button>
      <div id="overallProgress" class="overall-progress" style="display: none;"></div>
  </div>

  <div class="gallery">
    <?php foreach ($sliced_images as $img): ?>
      <?php
      $name = pathinfo($img, PATHINFO_FILENAME);
      $ext = pathinfo($img, PATHINFO_EXTENSION);

      // اصلاح خطاهای Undefined array key "status"
      $status = $upload_log[$img]['status'] ?? null;
      $isSent = $status === 'sent';
      $isFailed = $status === 'failed';

      if ($isSent) {
          $statusText = '🟢 ارسال شده';
          $statusClass = 'status sent';
          $btnClass = 'btn-sent';
      } elseif ($isFailed) {
          $statusText = '<button class="btn-send btn-failed" data-file="' . htmlspecialchars($img) . '">🔄 تلاش مجدد</button>';
          $statusClass = 'status failed';
          $btnClass = '';
      } else {
          $statusText = '<button class="btn-send" data-file="' . htmlspecialchars($img) . '">📤 ارسال</button>';
          $statusClass = 'status';
          $btnClass = '';
      }
      ?>
      <div class="item">
        <div class="filename"><?= htmlspecialchars($name . '.' . $ext) ?></div>
        <div class="controls">
          <div class="<?= $statusClass ?>"><?= $statusText ?></div>
          <div class="progress" id="progress-<?= htmlspecialchars($name) ?>"></div>
        </div>
      </div>
    <?php endforeach; ?>
  </div>

  <?php if ($pages_count > 1): ?>
    <div class="pagination">
      <?php if ($page > 1): ?>
        <a href="?page=<?= $page - 1 ?>">قبلی</a>
      <?php endif; ?>

      <?php
      $start = max(1, $page - 2);
      $end = min($pages_count, $page + 2);
      if ($start > 1): ?>
        <a href="?page=1">1</a>
        <?php if ($start > 2): ?>
          <span>...</span>
        <?php endif; ?>
      <?php endif; ?>

      <?php for ($i = $start; $i <= $end; $i++): ?>
        <?php if ($i == $page): ?>
          <span class="current"><?= $i ?></span>
        <?php else: ?>
          <a href="?page=<?= $i ?>"><?= $i ?></a>
        <?php endif; ?>
      <?php endfor; ?>

      <?php if ($end < $pages_count): ?>
        <?php if ($end < $pages_count - 1): ?>
          <span>...</span>
        <?php endif; ?>
        <a href="?page=<?= $pages_count ?>"><?= $pages_count ?></a>
      <?php endif; ?>

      <?php if ($page < $pages_count): ?>
        <a href="?page=<?= $page + 1 ?>">بعدی</a>
      <?php endif; ?>
    </div>
  <?php endif; ?>

</div>

<script>
let isSendingAll = false;
let sendAllQueue = [];
let sendAllIndex = 0;

async function sendAll() {
    if (isSendingAll) return; // جلوگیری از فشار دادن چند باره

    const btn = document.getElementById('btnSendAll');
    const progressDiv = document.getElementById('overallProgress');
    btn.disabled = true;
    btn.textContent = 'در حال پردازش...';
    progressDiv.style.display = 'block';

    try {
        const response = await fetch('', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({ action: 'send_all' })
        });

        if (!response.ok) {
            throw new Error('خطای دریافت لیست فایل‌ها');
        }

        const data = await response.json();
        if (!data.success) {
            throw new Error(data.message || 'خطا در دریافت لیست فایل‌ها');
        }

        sendAllQueue = data.files;
sendAllQueue.reverse(); // 🔄 ارسال از آخر به اول
        sendAllIndex = 0;

        if (sendAllQueue.length === 0) {
            progressDiv.textContent = '❌ هیچ فایل ارسال نشده‌ای در این صفحه وجود ندارد.';
            btn.disabled = false;
            btn.textContent = '📤 ارسال همه (ارسال نشده‌های صفحه)';
            return;
        }

        progressDiv.textContent = `0 از ${sendAllQueue.length} فایل ارسال شده...`;
        isSendingAll = true;
        processNextInQueue();
    } catch (err) {
        console.error(err);
        progressDiv.textContent = `❌ خطا: ${err.message}`;
        btn.disabled = false;
        btn.textContent = '📤 ارسال همه (ارسال نشده‌های صفحه)';
    }
}

async function processNextInQueue() {
    if (sendAllIndex >= sendAllQueue.length) {
        // تمام شد
        document.getElementById('overallProgress').textContent = '✅ ارسال همه فایل‌های صفحه به پایان رسید.';
        document.getElementById('btnSendAll').disabled = false;
        document.getElementById('btnSendAll').textContent = '📤 ارسال همه (ارسال نشده‌های صفحه)';
        isSendingAll = false;
        return;
    }

    const filename = sendAllQueue[sendAllIndex];
    const progressDivOverall = document.getElementById('overallProgress');
    const progressDivItem = document.getElementById('progress-' + filename.split('.')[0]);

    progressDivItem.style.display = 'block';
    progressDivItem.innerHTML = '📤 در حال ارسال...';
    progressDivOverall.textContent = `${sendAllIndex} از ${sendAllQueue.length} فایل ارسال شده...`;

    try {
        const response = await fetch('', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({ filename: filename })
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => ({ message: 'خطای سرور' }));
            throw new Error(errorData.message || 'خطای سرور');
        }

        const data = await response.json();

        if (data.success) {
            progressDivItem.innerHTML = '✅ موفق!';
            progressDivItem.style.color = '#4caf50';
            // بروزرسانی دکمه مربوط به فایل
            const itemDiv = progressDivItem.closest('.item');
            const controlsDiv = itemDiv.querySelector('.controls');
            controlsDiv.innerHTML = '<div class="status sent">🟢 ارسال شده</div><div class="progress" id="progress-' + filename.split('.')[0] + '"></div>';
        } else {
            progressDivItem.innerHTML = '❌ خطا: ' + data.message;
            progressDivItem.style.color = '#f44336';
            // بروزرسانی دکمه مربوط به فایل به حالت تلاش مجدد
            const itemDiv = progressDivItem.closest('.item');
            const controlsDiv = itemDiv.querySelector('.controls');
            controlsDiv.innerHTML = '<div class="status failed"><button class="btn-send btn-failed" data-file="' + filename + '">🔄 تلاش مجدد</button></div><div class="progress" id="progress-' + filename.split('.')[0] + '"></div>';
            // اضافه کردن event listener جدید
            controlsDiv.querySelector('.btn-send').addEventListener('click', function() {
                // اینجا می‌توانید عملیات ارسال یک فایل را دوباره فراخوانی کنید
                const file = this.getAttribute('data-file');
                sendSingleFile(file);
            });
        }
    } catch (err) {
        console.error(err);
        progressDivItem.innerHTML = '⚠️ خطا: ' + err.message;
        progressDivItem.style.color = '#f57c00';
    }

    sendAllIndex++;
    // تاخیر کوتاه بین ارسال‌ها
    setTimeout(processNextInQueue, 3000);
}

async function sendSingleFile(filename) {
    const progressDiv = document.getElementById('progress-' + filename.split('.')[0]);
    const statusDiv = progressDiv.parentNode;

    progressDiv.style.display = 'block';
    progressDiv.innerHTML = '📤 در حال ارسال...';

    try {
        const response = await fetch('', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({ filename: filename })
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => ({ message: 'خطای سرور' }));
            throw new Error(errorData.message || 'خطای سرور');
        }

        const data = await response.json();

        if (data.success) {
            progressDiv.innerHTML = '✅ موفق!';
            progressDiv.style.color = '#4caf50';
            statusDiv.innerHTML = '🟢 ارسال شده';
            statusDiv.className = 'status sent';
        } else {
            progressDiv.innerHTML = '❌ خطا: ' + data.message;
            progressDiv.style.color = '#f44336';
            statusDiv.innerHTML = '<button class="btn-send btn-failed" data-file="' + filename + '">🔄 تلاش مجدد</button>';
            statusDiv.className = 'status failed';
            statusDiv.querySelector('.btn-send').addEventListener('click', function() {
                const file = this.getAttribute('data-file');
                sendSingleFile(file);
            });
        }
    } catch (err) {
        console.error(err);
        progressDiv.innerHTML = '⚠️ مشکلی در ارتباط رخ داد.';
        progressDiv.style.color = '#f57c00';
    }
}

// اضافه کردن event listener به دکمه‌های ارسال یکی یکی
document.querySelectorAll('.btn-send:not(.btn-send-all)').forEach(button => {
    button.addEventListener('click', function() {
        const filename = this.getAttribute('data-file');
        sendSingleFile(filename);
    });
});
</script>

</body>
</html>