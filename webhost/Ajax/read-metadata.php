<?php
// ajax/read-metadata.php
header('Content-Type: application/json; charset=utf-8');
require_once '../config.php';

$filename = basename($_GET['filename'] ?? '');
if (!$filename) {
    echo json_encode(['error' => 'نام فایل نامعتبر']);
    exit;
}

$txt_path = GALLERY_PATH . pathinfo($filename, PATHINFO_FILENAME) . '.txt';

$positive = '---';
$negative = '---';
$description = '';

if (file_exists($txt_path)) {
    $content = file_get_contents($txt_path);
    
    if (preg_match('/Positive Prompt\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $p)) {
        $positive = trim($p[1]);
    } else {
        $lines = explode("\n", $content);
        $positive = trim($lines[0] ?? '---');
    }

    if (preg_match('/Negative Prompt\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $n)) {
        $negative = trim($n[1]);
    } else {
        $lines = explode("\n", $content);
        $negative = trim($lines[1] ?? '---');
    }

    if (preg_match('/Description\s*:\s*(.*?)(?:\n\n|\z)/s', $content, $d)) {
        $description = trim($d[1]);
    }
}

echo json_encode([
    'positive' => $positive,
    'negative' => $negative,
    'description' => $description
]);