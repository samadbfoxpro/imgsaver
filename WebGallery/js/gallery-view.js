let days = [];
let currentPage = 1;
let currentImages = [];
let visibleCount = 10;
let modalImageIndex = -1;

function escapeHtml(value) {
  return String(value || "").replace(/[&<>"']/g, ch => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#39;"
  }[ch]));
}

function showNotification(msg, isError = false) {
  const notif = document.getElementById("notif");
  notif.textContent = msg;
  notif.style.background = isError ? "linear-gradient(135deg,#d1685f,#b94d44)" : "linear-gradient(135deg,#6fbf8b,#4f9d6d)";
  notif.classList.add("show");
  setTimeout(() => notif.classList.remove("show"), 1700);
}

async function reloadAll() {
  const res = await fetch("/api/gallery/days");
  const data = await res.json();
  days = data.days || [];
  renderCalendar();

  if (!days.length) {
    document.getElementById("content").innerHTML = "<p class=\"no-data\">هیچ تصویری در گالری یافت نشد. ابتدا مسیر گالری دسکتاپ را تنظیم کنید.</p>";
    return;
  }

  await loadPage(1);
}

function renderCalendar() {
  const strip = document.getElementById("calendarStrip");
  strip.innerHTML = days.slice(0, 90)
    .map(d => `<button class="date-nav-btn ${d.page === currentPage ? "active" : ""}" onclick="loadPage(${d.page})">${d.date} (${d.count})</button>`)
    .join("");
}

async function loadPage(page) {
  currentPage = page;
  visibleCount = 10;
  const day = days[page - 1];
  if (!day) return;

  renderCalendar();
  document.getElementById("content").innerHTML = "<div class=\"loading\">در حال بارگذاری...</div>";
  const res = await fetch("/api/gallery/images?date=" + encodeURIComponent(day.date) + "&limit=500");
  const data = await res.json();
  currentImages = data.images || [];
  renderDay(day);
}

async function searchGallery() {
  const q = document.getElementById("searchInput").value.trim();
  if (!q) {
    await loadPage(currentPage || 1);
    return;
  }

  document.getElementById("content").innerHTML = "<div class=\"loading\">در حال جستجو...</div>";
  const res = await fetch("/api/gallery/images?q=" + encodeURIComponent(q) + "&limit=500");
  const data = await res.json();
  currentImages = data.images || [];
  visibleCount = currentImages.length;
  document.getElementById("content").innerHTML =
    `<div class="day-header">نتایج جستجو برای «${escapeHtml(q)}» - ${currentImages.length} نتیجه</div><div class="gallery-grid">${currentImages.map(renderCard).join("")}</div>`;
}

function renderDay(day) {
  const shown = currentImages.slice(0, visibleCount);
  document.getElementById("content").innerHTML = `
    <div class="day-header">${escapeHtml(day.date)} - ${currentImages.length} فایل</div>
    <div class="download-section">
      <strong>دانلود آیتم‌های این روز</strong>
      <span>در صورت وجود فایل متنی، روی هر کارت در دسترس است.</span>
    </div>
    <div class="bulk-actions">
      <label><input type="checkbox" class="select-all" onchange="toggleSelectAll(this)"> انتخاب همه موارد نمایان</label>
      <button class="delete-btn" onclick="deleteSelected()">حذف انتخاب‌شده‌ها</button>
    </div>
    <div class="gallery-grid" id="galleryGrid">${shown.map(renderCard).join("")}</div>
    ${visibleCount < currentImages.length ? "<button class=\"load-more-btn\" onclick=\"loadMore()\">نمایش بیشتر</button>" : ""}
    ${renderPagination()}
    <div style="text-align:center;margin-top:20px;color:var(--mist);font-size:.9rem">صفحه ${currentPage} از ${days.length} - ${escapeHtml(day.date)}</div>
  `;
}

function renderPagination() {
  const prev = currentPage > 1 ? `<button onclick="loadPage(${currentPage - 1})">روز قبل</button>` : "<span class=\"disabled\">روز قبل</span>";
  const next = currentPage < days.length ? `<button onclick="loadPage(${currentPage + 1})">روز بعد</button>` : "<span class=\"disabled\">روز بعد</span>";
  const start = Math.max(1, currentPage - 3);
  const end = Math.min(days.length, start + 6);
  let nums = "";
  for (let i = start; i <= end; i++) {
    nums += i === currentPage ? `<span class="current">${i}</span>` : `<button onclick="loadPage(${i})">${i}</button>`;
  }
  return `<div class="pagination">${prev}${nums}${next}</div>`;
}

function renderCard(img) {
  return `
    <div class="card" data-id="${img.id}" data-filename="${escapeHtml(img.fileName)}" data-positive="${escapeHtml(img.positive)}" data-negative="${escapeHtml(img.negative)}" data-description="${escapeHtml(img.description)}">
      <button class="delete-single-btn" onclick="deleteImages(['${img.id}'])">&times;</button>
      <input type="checkbox" class="select-checkbox" name="selected_files[]" value="${img.id}">
      <div class="image-wrapper">
        <img src="${img.url}" alt="${escapeHtml(img.fileName)}" onclick="openModal('${img.id}')" style="cursor:zoom-in" loading="lazy" class="lazy" onload="this.classList.add('lazy-loaded')">
      </div>
      <div class="filename-display">${escapeHtml(img.fileName)}</div>
      ${renderPrompt("پرامپت مثبت", img.positive, false)}
      ${renderPrompt("پرامپت منفی", img.negative, true)}
      ${img.description ? `<div class="prompt-box"><h4>توضیحات:</h4><pre>${escapeHtml(img.description)}</pre></div>` : ""}
      <div class="card-actions">
        <a class="download-btn" href="${img.url}" download>دانلود تصویر</a>
        ${img.textUrl ? `<a class="download-btn" href="${img.textUrl}" download>دانلود فایل متنی</a>` : ""}
      </div>
    </div>`;
}

function renderPrompt(label, value, isNegative) {
  return `<div class="prompt-box${isNegative ? " is-negative" : ""}"><h4>${label}:</h4><div class="copy-box" data-copy="${escapeHtml(value)}"><pre>${escapeHtml(value)}</pre><button class="copy-btn" onclick="copyToClipboard(this)">کپی</button></div></div>`;
}

function loadMore() {
  visibleCount += 10;
  renderDay(days[currentPage - 1]);
}

function toggleSelectAll(source) {
  document.querySelectorAll("input[name=\"selected_files[]\"]").forEach(cb => {
    cb.checked = source.checked;
  });
}

function deleteSelected() {
  const ids = Array.from(document.querySelectorAll("input[name=\"selected_files[]\"]:checked")).map(cb => cb.value);
  if (!ids.length) {
    alert("هیچ موردی انتخاب نشده است.");
    return;
  }
  deleteImages(ids);
}

async function deleteImages(ids) {
  if (!confirm(`حذف ${ids.length} مورد؟`)) return;
  const res = await fetch("/api/gallery/delete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ids })
  });
  const data = await res.json();
  if (!data.success) {
    showNotification(data.error || "حذف ناموفق بود", true);
    return;
  }
  showNotification("حذف شد");
  await reloadAll();
}

function copyToClipboard(btn) {
  const text = btn.closest(".copy-box").dataset.copy || "";
  navigator.clipboard.writeText(text).then(() => {
    const old = btn.innerText;
    btn.innerText = "کپی شد";
    showNotification("کپی شد");
    setTimeout(() => {
      btn.innerText = old;
    }, 1500);
  });
}

function openModal(idOrUrl) {
  const index = currentImages.findIndex(img => img.id === idOrUrl || img.url === idOrUrl);
  modalImageIndex = index >= 0 ? index : -1;
  showModalImage(index >= 0 ? currentImages[index] : { url: idOrUrl, fileName: "پیش‌نمایش" });
  document.getElementById("imageModal").style.display = "flex";
  document.body.style.overflow = "hidden";
}

function showModalImage(img) {
  const modalImg = document.getElementById("modalImg");
  modalImg.src = img.url;
  modalImg.alt = img.fileName || "پیش‌نمایش";
  updateModalControls();
}

function updateModalControls() {
  const hasImages = currentImages.length > 1 && modalImageIndex >= 0;
  document.querySelectorAll(".modal-nav").forEach(btn => {
    btn.style.display = hasImages ? "flex" : "none";
  });
  const counter = document.getElementById("modalCounter");
  if (modalImageIndex >= 0 && currentImages.length) {
    counter.textContent = `${modalImageIndex + 1} / ${currentImages.length}`;
    counter.style.display = "block";
  } else {
    counter.textContent = "";
    counter.style.display = "none";
  }
}

function moveModalImage(delta) {
  if (modalImageIndex < 0 || !currentImages.length) return;
  modalImageIndex = (modalImageIndex + delta + currentImages.length) % currentImages.length;
  showModalImage(currentImages[modalImageIndex]);
}

function showPreviousImage() {
  moveModalImage(-1);
}

function showNextImage() {
  moveModalImage(1);
}

function closeModal() {
  document.getElementById("imageModal").style.display = "none";
  document.body.style.overflow = "";
  modalImageIndex = -1;
}

let searchTimer = null;
document.getElementById("searchInput").addEventListener("input", () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(searchGallery, 350);
});

window.onclick = event => {
  if (event.target === document.getElementById("imageModal")) closeModal();
};

document.addEventListener("keydown", event => {
  if (document.getElementById("imageModal").style.display !== "flex") return;
  if (event.key === "Escape") closeModal();
  if (event.key === "ArrowLeft") showNextImage();
  if (event.key === "ArrowRight") showPreviousImage();
});

reloadAll();
