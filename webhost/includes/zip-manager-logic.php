<?php
$dir = realpath(__DIR__ . '/../temp-zip');
$files = [];
if ($dir && is_dir($dir)) {
    foreach (glob($dir . '/*.zip') as $file) {
        $files[] = basename($file);
    }
}
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $action = $_POST['action'] ?? '';
    $filename = basename($_POST['filename'] ?? '');
    $filepath = $dir . DIRECTORY_SEPARATOR . $filename;
    if ($action === 'delete' && file_exists($filepath)) {
        unlink($filepath);
        header('Location: zip-manager.php');
        exit;
    }
    if ($action === 'rename' && file_exists($filepath)) {
        $newname = basename($_POST['newname'] ?? '');
        if ($newname && preg_match('/^[\w\-.]+\.zip$/', $newname)) {
            rename($filepath, $dir . DIRECTORY_SEPARATOR . $newname);
            header('Location: zip-manager.php');
            exit;
        }
    }
    if ($action === 'delete_all') {
        foreach (glob($dir . '/*.zip') as $file) {
            @unlink($file);
        }
        header('Location: zip-manager.php');
        exit;
    }
}
