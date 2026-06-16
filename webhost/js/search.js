(() => {
  const input = document.getElementById('searchInput');
  const gallery = document.getElementById('gallery-container');
  const pagination = document.querySelector('.pagination');
  const dayHeader = document.querySelector('.day-header');
  // کپی HTML اولیه تا بتوانیم بعداً بازگردانیم
  const originalGalleryHTML = gallery ? gallery.innerHTML : '';
  const originalPaginationHTML = pagination ? pagination.innerHTML : '';
  const originalDayHeaderHTML = dayHeader ? dayHeader.innerHTML : '';

  // debounce helper
  function debounce(fn, delay) {
    let t;
    return (...args) => {
      clearTimeout(t);
      t = setTimeout(() => fn(...args), delay);
    };
  }

  async function doSearch(q) {
    if (!gallery) return;
    q = q.trim();
    if (q.length < 2) {
      // اگه کمتر از ۲ حرف، محتوای اصلی رو برگردون
      gallery.innerHTML = originalGalleryHTML;
      if (pagination) pagination.innerHTML = originalPaginationHTML;
      if (dayHeader) dayHeader.innerHTML = originalDayHeaderHTML;
      return;
    }

    gallery.innerHTML = '<p style="text-align:center;color:#aaa;padding:30px">⏳ در حال جستجو...</p>';
    if (pagination) pagination.innerHTML = ''; // پنهان کردن پیجینیشن هنگام نمایش نتایج سرچ
    if (dayHeader) dayHeader.innerHTML = '';

    try {
      // اگر view.php و gallery_logica.php در یک فولدر نباشند، این مسیر را اصلاح کن.
      const res = await fetch('gallery_logica.php?search=' + encodeURIComponent(q), { cache: 'no-cache' });
      if (!res.ok) throw new Error('HTTP ' + res.status);
      const data = await res.json();

      if (!data || !Array.isArray(data.results) || data.results.length === 0) {
        gallery.innerHTML = '<p style="text-align:center;color:#f66;padding:30px">❌ هیچ نتیجه‌ای یافت نشد.</p>';
        return;
      }

      // ساخت HTML نتایج
      const html = data.results.map(item => {
        // امن‌سازی مسیرها/متن‌ها با encode و replace ساده (پروسه‌ی کامل‌تر در سرور بهتره)
        const pPositive = item.positive ? escapeHtml(item.positive) : '---';
        const pNegative = item.negative ? escapeHtml(item.negative) : '---';
        const fname = escapeHtml(item.filename);
        const date = escapeHtml(item.date);
        const img = escapeHtml(item.path);

        return `
          <div class="gallery-item" style="display:inline-block; vertical-align:top; margin:10px; width:220px; background:#111; padding:8px; border-radius:8px;">
            <div style="height:140px; display:flex; align-items:center; justify-content:center; overflow:hidden;">
              <img src="${img}" alt="${fname}" style="max-width:100%; max-height:140px; object-fit:cover; border-radius:6px;">
            </div>
            <div style="padding:8px 6px; text-align:center; color:#ddd;">
              <div class="caption" style="font-weight:600; margin-bottom:6px;">📄 ${fname}</div>
              <div style="font-size:0.85rem; color:#bbb; height:36px; overflow:hidden; margin-bottom:6px;">🔹 ${pPositive}</div>
              <div style="font-size:0.8rem; color:#aaa; height:36px; overflow:hidden; margin-bottom:6px;">🔸 ${pNegative}</div>
              <div style="font-size:0.8rem; color:#999; margin-bottom:6px;">📅 ${date}</div>
              <div style="display:flex; gap:6px; justify-content:center;">
                <a href="${img}" download><button class="download-btn" style="padding:6px 8px;">💾 دانلود</button></a>
              </div>
            </div>
          </div>
        `;
      }).join('');

      gallery.innerHTML = html;

    } catch (err) {
      console.error(err);
      gallery.innerHTML = '<p style="text-align:center;color:#f55;padding:30px">❌ خطا در ارتباط با سرور یا پاسخ نامعتبر. کنسول را چک کن.</p>';
    }
  }

  // توابع کمکی
  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  const debounced = debounce((e) => doSearch(e.target.value), 300);

  if (input) {
    input.removeAttribute('oninput'); // اگر inline event هست حذفش کن
    input.addEventListener('input', debounced);
  }
})();