// js/multi-upload.js
// مدیریت فرم آپلود چندتایی و ساخت داینامیک فیلدها برای هر فایل

document.addEventListener('DOMContentLoaded', function() {
  const fileInput = document.getElementById('multi-image-upload');
  const filesContainer = document.getElementById('files-container');
  const addFileBtn = document.getElementById('add-file-btn');
  const uploadAllBtn = document.getElementById('upload-all-btn');
  let fileList = [];

  // هندل انتخاب چند فایل
  fileInput.addEventListener('change', function(e) {
    for (const file of e.target.files) {
      addFileToList(file);
    }
    fileInput.value = '';
  });

  // دکمه افزودن فایل جدید
  addFileBtn.addEventListener('click', function(e) {
    e.preventDefault();
    fileInput.click();
  });

  // افزودن فایل به لیست و ساخت فیلدهای مربوطه
  function addFileToList(file) {
    // جلوگیری از تکرار
    if (fileList.some(f => f.name === file.name && f.size === file.size)) return;
    fileList.push(file);
    const idx = fileList.length - 1;
      const fileBox = document.createElement('div');
      fileBox.className = 'file-upload-box';
      fileBox.style = 'background:#23232b;border-radius:12px;padding:16px 14px;box-shadow:0 2px 8px #0002;position:relative;';
      fileBox.innerHTML = `
        <button type="button" class="remove-file-btn" style="position:absolute;left:10px;top:10px;background:#c62828;color:#fff;border:none;border-radius:6px;padding:2px 10px;cursor:pointer;font-size:0.95rem;">حذف</button>
        <div class="file-info" style="color:#bb86fc;font-size:1.05rem;margin-bottom:7px;">${file.name} <span style="color:#888;font-size:0.95em;">(${Math.round(file.size/1024)} KB)</span></div>
        <div class="filename-box" style="margin-bottom:8px;">
          <label style="font-size:0.97em;">نام فایل (بدون پسوند):</label>
          <input type="text" name="filename[]" required placeholder="مثلاً: myphoto" value="${file.name.replace(/\.[^.]+$/, '')}" style="margin-right:7px;padding:3px 7px;border-radius:5px;border:1px solid #444;background:#18181f;color:#fff;">
        </div>
        <div style="margin-bottom:7px;">
          <label style="font-size:0.97em;">متن Prompt مثبت:</label>
          <textarea name="positive_prompt[]" rows="2" placeholder="مثلا: یک تصویر زیبا از طبیعت..." required style="width:100%;margin-top:3px;border-radius:5px;border:1px solid #444;background:#18181f;color:#fff;"></textarea>
        </div>
        <div>
          <label style="font-size:0.97em;">متن Prompt منفی:</label>
          <textarea name="negative_prompt[]" rows="2" placeholder="مثلا: بدون نویز، بدون متن..." required style="width:100%;margin-top:3px;border-radius:5px;border:1px solid #444;background:#18181f;color:#fff;"></textarea>
        </div>
      `;
    // حذف فایل
    fileBox.querySelector('.remove-file-btn').onclick = function() {
      fileList.splice(idx, 1);
      fileBox.remove();
    };
    filesContainer.appendChild(fileBox);
  }

  // دکمه آپلود همه
  uploadAllBtn.addEventListener('click', function(e) {
    e.preventDefault();
    if (fileList.length === 0) {
      alert('هیچ فایلی انتخاب نشده است!');
      return;
    }
    // جمع‌آوری داده‌ها
    const formData = new FormData();
    const filenames = filesContainer.querySelectorAll('input[name="filename[]"]');
    const positives = filesContainer.querySelectorAll('textarea[name="positive_prompt[]"]');
    const negatives = filesContainer.querySelectorAll('textarea[name="negative_prompt[]"]');
    fileList.forEach((file, i) => {
      formData.append('images[]', file);
      formData.append('filename[]', filenames[i].value);
      formData.append('positive_prompt[]', positives[i].value);
      formData.append('negative_prompt[]', negatives[i].value);
    });
    // نمایش نوار پیشرفت
    document.getElementById('progress-container').style.display = 'block';
    // ارسال AJAX
    fetch('includes/image-upload-handler.php', {
      method: 'POST',
      body: formData
    }).then(res => res.json())
      .then(data => {
        document.getElementById('progress-container').style.display = 'none';
        if (data.success) {
          showToast('همه فایل‌ها با موفقیت آپلود شدند!');
          filesContainer.innerHTML = '';
          fileList = [];
        } else {
          showToast('خطا در آپلود: ' + (data.error || ''));
        }
      })
      .catch(() => {
        document.getElementById('progress-container').style.display = 'none';
        showToast('خطا در ارتباط با سرور!');
      });
  });

  // Toast
  function showToast(msg) {
    const toast = document.getElementById('toast');
    toast.textContent = msg;
    toast.classList.add('show');
    setTimeout(() => toast.classList.remove('show'), 3000);
  }
});
