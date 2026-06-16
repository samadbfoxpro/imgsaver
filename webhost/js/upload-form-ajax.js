// نمایش بخش آپلود از دستگاه با دکمه کوچک
document.addEventListener('DOMContentLoaded', function() {
    var showBtn = document.getElementById('show-upload-btn');
    var uploadBox = document.getElementById('upload-device-box');
    if (showBtn && uploadBox) {
        showBtn.addEventListener('click', function() {
            uploadBox.style.display = (uploadBox.style.display === 'none') ? 'block' : 'none';
        });
    }
});

document.querySelector('form').addEventListener('submit', function(e) {
    e.preventDefault();

    const form = this;
    const formData = new FormData(form);

    const progressContainer = document.getElementById('progress-container');
    const progressBar = document.getElementById('progress-bar');
    const progressPercent = document.getElementById('progress-percent');

    progressContainer.style.display = 'block';
    progressBar.style.width = '0%';
    progressPercent.textContent = '0%';

    const xhr = new XMLHttpRequest();

    xhr.upload.onprogress = function(event) {
        if (event.lengthComputable) {
            const percent = Math.round((event.loaded / event.total) * 100);
            progressBar.style.width = percent + '%';
            progressPercent.textContent = percent + '%';
        }
    };

    xhr.onload = function() {
        if (xhr.status === 200) {
            try {
                const data = JSON.parse(xhr.responseText);

                const toast = document.getElementById('toast');

                if (data.success) {
                    toast.textContent = `✅ فایل "${data.filename}" با موفقیت آپلود شد!`;
                    toast.style.display = 'block'; // نمایش مستقیم
                    toast.classList.add('show'); // اعمال انیمیشن اروم
                } else {
                    toast.textContent = `❌ ${data.message}`;
                    toast.style.display = 'block';
                    toast.classList.add('show');
                }

                // حذف کلاس show بعد از 4 ثانیه
                setTimeout(() => {
                    toast.classList.remove('show');
                    toast.style.display = 'none';
                }, 4000);

            } catch (e) {
                showError('خطا در پردازش پاسخ سرور.');
            }
        } else {
            showError('خطای سرور: ' + xhr.status);
        }

        setTimeout(() => {
            progressContainer.style.display = 'none';
        }, 1000);
    };

    xhr.onerror = function() {
        showError('❌ ارتباط با سرور برقرار نشد!');
        progressContainer.style.display = 'none';
    };

    xhr.open('POST', '');
    xhr.setRequestHeader('Accept', 'application/json');
    xhr.send(formData);

    function showError(message) {
        const toast = document.getElementById('toast');
        toast.textContent = message;
        toast.style.display = 'block';
        toast.classList.add('show');

        setTimeout(() => {
            toast.classList.remove('show');
            toast.style.display = 'none';
        }, 4000);
    }
});