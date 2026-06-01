@echo off
setlocal

echo.
echo Backup folder files only
echo ------------------------
echo Enter a folder path or folder name. Subfolders will be ignored.
echo Example: imgsaver
echo.

set /p "TARGET_FOLDER=Folder: "

if "%TARGET_FOLDER%"=="" (
    echo No folder entered.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$folder = (Resolve-Path -LiteralPath '%TARGET_FOLDER%' -ErrorAction Stop).Path; " ^
    "$dir = Get-Item -LiteralPath $folder; " ^
    "$stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'; " ^
    "$zipPath = Join-Path $dir.Parent.FullName ($dir.Name + '_' + $stamp + '.zip'); " ^
    "$files = Get-ChildItem -LiteralPath $folder -File; " ^
    "if (-not $files) { Write-Host 'No files found in this folder.'; exit 2 }; " ^
    "Compress-Archive -LiteralPath $files.FullName -DestinationPath $zipPath -Force; " ^
    "Write-Host ('Created: ' + $zipPath)"

if errorlevel 1 (
    echo.
    echo Backup failed. Check that the folder exists and try again.
    pause
    exit /b 1
)

echo.
pause
