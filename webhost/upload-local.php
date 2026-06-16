<?php
require_once __DIR__ . '/config.php';
include __DIR__ . '/includes/image-upload-handler.php';
?>

<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
<title>آپلود مدرن گلس</title>
<style>
:root{
  --accent1:#6366f1;
  --accent2:#06b6d4;
  --bg-dark:#0f172a;
  --text:#e2e8f0;
  --muted:#94a3b8;
  --radius:16px;
  font-family:"Vazirmatn",system-ui,sans-serif;
}
body{
  margin:0;
  min-height:100vh;
  background:linear-gradient(145deg,#0f172a 0%,#1e293b 40%,#111827 100%);
  display:flex;
  flex-direction:column;
  align-items:center;
  justify-content:flex-start;
  padding:24px;
  color:var(--text);
}
.container{
  width:100%;
  max-width:480px;
  backdrop-filter:blur(14px) saturate(140%);
  background:rgba(255,255,255,0.05);
  border:1px solid rgba(255,255,255,0.08);
  border-radius:var(--radius);
  box-shadow:0 10px 40px rgba(0,0,0,0.3);
  padding:20px;
  box-sizing:border-box;
  display:flex;
  flex-direction:column;
  gap:20px;
}
h2{
  text-align:center;
  font-size:20px;
  font-weight:700;
  margin:0;
  background:linear-gradient(90deg,var(--accent1),var(--accent2));
  -webkit-background-clip:text;
  color:transparent;
}
.filename-box{
  margin-bottom:14px;
}
.filename-box label{
  color:var(--accent2);
  font-weight:600;
  font-size:14px;
  display:block;
  margin-bottom:6px;
}
input[type="text"],
textarea{
  width:100%;
  background:rgba(255,255,255,0.05);
  border:1px solid rgba(255,255,255,0.1);
  border-radius:12px;
  padding:10px;
  color:var(--text);
  font-size:14px;
  outline:none;
  box-sizing:border-box;
}
textarea{resize:vertical;min-height:70px;}
input::placeholder,textarea::placeholder{color:var(--muted);}
.paste-container{
  display:flex;
  gap:8px;
}
.paste-area{
  flex:1;
  border:2px dashed var(--accent2);
  border-radius:12px;
  padding:12px;
  text-align:center;
  color:var(--muted);
  font-size:14px;
  min-height:90px;
  display:flex;
  align-items:center;
  justify-content:center;
  flex-direction:column;
  background:rgba(255,255,255,0.03);
  transition:border .3s,background .3s;
}
.paste-area img{
  max-width:100%;
  max-height:80px;
  border-radius:10px;
  margin-top:6px;
}
.toggle-upload-btn{
  width:50px;
  height:90px;
  background:none;
  border:1px solid rgba(6, 182, 212, 0.3);
  color:rgba(6, 182, 212, 0.5);
  padding:6px 12px;
  border-radius:12px;
  cursor:pointer;
  font-size:13px;
  align-self:flex-start;
}
.upload-main-btn{
  width:100%;
  border:none;
  border-radius:50px;
  padding:14px;
  font-size:16px;
  font-weight:600;
  color:#fff;
  background:linear-gradient(90deg,var(--accent1),var(--accent2));
  box-shadow:0 4px 20px rgba(99,102,241,0.4);
  cursor:pointer;
  transition:opacity .25s,transform .2s;
  margin-top:8px;
}
.upload-main-btn:active{transform:scale(0.98);}
.upload-main-btn:hover{opacity:0.9;}
#image-upload{display:none;}
a{
  color:var(--accent2);
  text-decoration:none;
  font-size:13px;
  text-align:center;
  margin-top:10px;
}
a:hover{text-decoration:underline;}

/* Progress */
#progress-container{
  background:rgba(255,255,255,0.05);
  border-radius:12px;
  padding:10px;
  display:none;
}
#progress-bar{
  height:8px;
  background:linear-gradient(90deg,var(--accent1),var(--accent2));
  border-radius:4px;
  width:0%;
  transition:width 0.2s ease;
}

/* Toast */
#toast {
  position: fixed;
  top: -120px;
  left: 50%;
  transform: translateX(-50%);
  background: linear-gradient(135deg, #164e20, #0f3d19);
  color: #a3f7a9;
  padding: 14px 18px;
  border-radius: 10px;
  box-shadow: 0 6px 20px rgba(0,0,0,0.4);
  font-size: 14px;
  z-index: 10000;
  opacity: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 340px;
  max-width: 90%;
  transition: top .4s ease, opacity .4s ease, transform .4s ease;
  border: 1px solid rgba(163, 247, 169, 0.2);
}
#toast.show {top:25px; opacity:1; transform: translateX(-50%) scale(1);}
#toast:not(.show) {transform: translateX(-50%) scale(0.95);}
#toast.success{background:linear-gradient(135deg, #164e20, #0f3d19); border: 1px solid rgba(163, 247, 169, 0.2); color: #a3f7a9;}
#toast.error{background:linear-gradient(135deg, #450a0a, #7f1d1d); border: 1px solid rgba(252, 165, 165, 0.2); color: #fecaca;}
#toast .close-btn{background: rgba(163, 247, 169, 0.1); border: 1px solid rgba(163, 247, 169, 0.3); color: #a3f7a9; font-size: 16px; cursor: pointer; transition: all .2s ease; width: 24px; height: 24px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: bold;}
#toast .close-btn:hover{background: rgba(163, 247, 169, 0.2); transform: scale(1.1);}

/* Live Stats شیک */
#live-stats{
  margin-top:20px;
  width:100%;
  max-width:480px;
  backdrop-filter:blur(10px);
  background:rgba(0,0,0,0.25);
  border-radius:16px;
  padding:12px 16px;
  font-size:14px;
  color:#f8fafc;
  box-shadow:0 8px 20px rgba(0,0,0,0.3);
}
#live-stats h3{
  margin:0 0 8px 0;
  font-size:16px;
  border-bottom:1px solid rgba(255,255,255,0.1);
  padding-bottom:6px;
}
#stats-list{
  margin:0;
  padding:0;
  list-style:none;
  max-height:180px;
  overflow-y:auto;
}
#stats-list li{
  padding:6px 8px;
  border-bottom:1px solid rgba(255,255,255,0.1);
  display:flex;
  justify-content:space-between;
  font-size:13px;
  color:#e2e8f0;
}
#stats-list li:last-child{border-bottom:none;}
</style>
</head>
<body>

<div class="container">
  <h2>📁 آپلود مدرن (پیش‌فرض روی پیست)</h2>
  <form id="upload-form" method="post" enctype="multipart/form-data">
    
    <div id="progress-container">
      <div style="display:flex;justify-content:space-between;color:var(--muted);font-size:13px;margin-bottom:4px;">
        <span>📤 در حال آپلود...</span><span id="progress-percent">0%</span>
      </div>
      <div style="width:100%;height:8px;background:rgba(255,255,255,0.1);border-radius:4px;overflow:hidden;">
        <div id="progress-bar"></div>
      </div>
    </div>

    <div class="filename-box">
      <label for="filename-input">نام فایل (بدون پسوند):</label>
      <input type="text" name="filename" id="filename-input" required placeholder="مثلاً: myphoto">
    </div>

    <div class="paste-container">
      <button type="button" class="toggle-upload-btn" id="show-upload-btn">📁</button>
      <div id="paste-area" class="paste-area">
        📋 اینجا کلیک کن و Ctrl+V بزن!
      </div>
    </div>
    <input type="file" name="image" accept="image/*" id="image-upload">

    <label>پرامپت مثبت:</label>
    <textarea name="positive_prompt" rows="3" placeholder="مثلاً: منظره‌ای زیبا از غروب..." required></textarea>

    <label>پرامپت منفی:</label>
    <textarea id="negative_prompt" name="negative_prompt" rows="3" placeholder="مواردی که نمی‌خواهی در تصویر باشند..."><?= htmlspecialchars($last_negative_prompt ?? '---') ?></textarea>

    <label>توضیحات اختیاری:</label>
    <textarea name="optional_description" rows="2" placeholder="شرایط یا توضیحات اضافی..."></textarea>

    <button type="submit" class="upload-main-btn">🚀 آپلود کن</button>
  </form>
</div>

<a href="index.php">← بازگشت به صفحه اصلی</a>

<div id="live-stats">
  <h3>📊 آمار آپلود زنده</h3>
  <ul id="stats-list"></ul>
</div>

<div id="toast">
  <span id="toast-message">فایل با موفقیت آپلود شد!</span>
  <button class="close-btn" onclick="hideToast()">×</button>
</div>

<script>
document.addEventListener('DOMContentLoaded',function(){
  const pasteArea=document.getElementById('paste-area');
  const pastedImageInput=document.createElement('input');
  pastedImageInput.type='hidden';
  pastedImageInput.name='pasted_image';
  document.getElementById('upload-form').appendChild(pastedImageInput);

  const form=document.getElementById('upload-form');
  const showUploadBtn=document.getElementById('show-upload-btn');
  const imageUpload=document.getElementById('image-upload');
  const statsList=document.getElementById('stats-list');

  showUploadBtn.addEventListener('click',()=>imageUpload.click());

  document.addEventListener('paste',function(e){
    const items=(e.clipboardData||e.originalEvent.clipboardData).items;
    for(let i=0;i<items.length;i++){
      if(items[i].type.indexOf('image')!==-1){
        const blob=items[i].getAsFile();
        const reader=new FileReader();
        reader.onload=function(ev){
          pasteArea.innerHTML='';
          const img=document.createElement('img');
          img.src=ev.target.result;
          pasteArea.appendChild(img);
          pastedImageInput.value=ev.target.result;
        };
        reader.readAsDataURL(blob);
      }
    }
  });

  form.addEventListener('submit',function(e){
    e.preventDefault();
    const formData=new FormData(form);
    const progressContainer=document.getElementById('progress-container');
    const progressBar=document.getElementById('progress-bar');
    const progressPercent=document.getElementById('progress-percent');
    progressContainer.style.display='block';
    progressBar.style.width='0%';
    progressPercent.textContent='0%';
    const xhr=new XMLHttpRequest();
    xhr.upload.onprogress=function(event){
      if(event.lengthComputable){
        const percent=Math.round((event.loaded/event.total)*100);
        progressBar.style.width=percent+'%';
        progressPercent.textContent=percent+'%';
      }
    };
    xhr.onload=function(){
      if(xhr.status===200){
        try{
          const data=JSON.parse(xhr.responseText);
          if(data.success){
            showToast(`✅ فایل "${data.filename}" با موفقیت آپلود شد!`,"success");

            // اضافه کردن به آمار زنده با اسم و زمان
            const time = new Date().toLocaleTimeString();
            const li = document.createElement('li');
            li.textContent = `${data.filename} — ${time}`;
            statsList.appendChild(li);

            form.reset();
            pasteArea.innerHTML='📋 اینجا کلیک کن و Ctrl+V بزن!';
            pastedImageInput.value='';
          }else showToast(`❌ ${data.message}`,"error");
        }catch(e){alert('❌ خطای JSON: '+e.message);}
      }else alert('❌ خطای سرور: '+xhr.status);
      setTimeout(()=>progressContainer.style.display='none',1000);
    };
    xhr.onerror=function(){alert('❌ ارتباط با سرور برقرار نشد!');progressContainer.style.display='none';};
    xhr.open('POST','');
    xhr.setRequestHeader('Accept','application/json');
    xhr.send(formData);
  });
});

function showToast(msg,type=""){
  const toast=document.getElementById('toast');
  const toastMsg=document.getElementById('toast-message');
  toastMsg.textContent=msg;
  toast.className='';
  toast.classList.add(type);
  toast.style.display='flex';
  requestAnimationFrame(()=>toast.classList.add('show'));
  toast.addEventListener('click',()=>hideToast());
  window.hideToast=hideToast;
  
  // خودکار پنهان شود بعد از 2 ثانیه
  setTimeout(hideToast, 2000);
}
function hideToast(){
  const toast=document.getElementById('toast');
  toast.classList.remove('show');
  setTimeout(()=>toast.style.display='none',500);
}
</script>

<script src="js/negative-prompt-clear.js"></script>
</body>
</html>