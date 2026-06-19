let currentImages = [];
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
  setTimeout(() => notif.classList.remove("show"), 2500);
}

function escapeAttr(value) {
  return escapeHtml(value).replace(/`/g, "&#96;");
}

async function loadRandomImages() {
  const grid = document.getElementById("randomGrid");
  grid.innerHTML = "<div class=\"loading\">در حال بارگذاری...</div>";
  const count = getRandomCount();
  const res = await fetch("/api/gallery/random?count=" + encodeURIComponent(count));
  const data = await res.json();
  if (!data.success) {
    showNotification(data.error || "بارگذاری تصادفی ناموفق بود", true);
    return;
  }
  currentImages = data.images || [];
  renderImages(currentImages);
}

function getRandomCount() {
  const input = document.getElementById("randomCount");
  const value = parseInt(input?.value || "5", 10);
  if (!Number.isFinite(value)) return 5;
  return Math.min(100, Math.max(1, value));
}

function renderImages(images) {
  const grid = document.getElementById("randomGrid");
  if (!images.length) {
    grid.innerHTML = "<div class=\"loading\">تصویری یافت نشد.</div>";
    return;
  }
  grid.innerHTML = images.map(img => `
    <div class="random-card" data-id="${img.id}" data-filename="${escapeHtml(img.fileName)}" data-extension="${escapeHtml(img.extension)}">
      <img src="${img.url}" alt="${escapeHtml(img.fileName)}" loading="lazy" onclick="openModal('${escapeAttr(img.id)}')">
      <div class="filename-display">
        ${escapeHtml(img.baseName || img.fileName)}
        <div class="card-actions">
          <button class="btn-edit" onclick="toggleEdit(this)">ویرایش نام</button>
          <button class="btn-meta" onclick="toggleCardMeta(this)">نمایش اطلاعات</button>
        </div>
      </div>
      <div class="card-meta"></div>
      <div class="edit-section">
        <input type="text" class="edit-input" value="${escapeHtml(img.baseName)}">
        <div class="edit-btns"><button class="btn btn-save" onclick="saveRename(this)">ذخیره</button><button class="btn btn-cancel" onclick="toggleEdit(this)">انصراف</button></div>
      </div>
    </div>`).join("");
}

async function toggleCardMeta(btn) {
  const card = btn.closest(".random-card");
  const metaBox = card.querySelector(".card-meta");
  const isOpen = metaBox.style.display === "block";
  if (isOpen) {
    metaBox.style.display = "none";
    btn.textContent = "نمایش اطلاعات";
    return;
  }

  if (!metaBox.dataset.loaded) {
    metaBox.innerHTML = "<div class=\"meta-loading\">در حال بارگذاری اطلاعات...</div>";
    metaBox.style.display = "block";
    const res = await fetch("/api/gallery/metadata/" + encodeURIComponent(card.dataset.id));
    const meta = await res.json();
    metaBox.innerHTML = renderMeta(meta);
    metaBox.dataset.loaded = "true";
  } else {
    metaBox.style.display = "block";
  }
  btn.textContent = "پنهان کردن اطلاعات";
}

function toggleEdit(btn) {
  const card = btn.closest(".random-card");
  const display = card.querySelector(".filename-display");
  const editSection = card.querySelector(".edit-section");
  const editing = display.style.display === "none";
  display.style.display = editing ? "block" : "none";
  editSection.style.display = editing ? "none" : "block";
  if (!editing) editSection.querySelector(".edit-input").focus();
}

async function saveRename(btn) {
  const card = btn.closest(".random-card");
  const input = card.querySelector(".edit-input");
  const id = card.dataset.id;
  const newBaseName = input.value.trim();
  if (!newBaseName) {
    showNotification("نام نمی‌تواند خالی باشد", true);
    return;
  }

  const res = await fetch("/api/gallery/rename", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ id, newBaseName })
  });
  const data = await res.json();
  if (!data.success) {
    showNotification(data.error || "تغییر نام ناموفق بود", true);
    return;
  }

  const updated = data.image;
  card.dataset.id = updated.id;
  card.dataset.filename = updated.fileName;
  card.querySelector("img").src = updated.url + "?t=" + Date.now();
  card.querySelector("img").onclick = () => openModal(updated.id);
  card.querySelector(".filename-display").innerHTML = `
    ${escapeHtml(updated.baseName || updated.fileName)}
    <div class="card-actions">
      <button class="btn-edit" onclick="toggleEdit(this)">ویرایش نام</button>
      <button class="btn-meta" onclick="toggleCardMeta(this)">نمایش اطلاعات</button>
    </div>`;
  const metaBox = card.querySelector(".card-meta");
  metaBox.innerHTML = "";
  metaBox.style.display = "none";
  delete metaBox.dataset.loaded;
  toggleEdit(btn);
  showNotification("تغییر نام انجام شد");
  currentImages = currentImages.map(img => img.id === id ? updated : img);
}

async function openModal(id) {
  const index = currentImages.findIndex(i => i.id === id);
  if (index < 0) return;

  modalImageIndex = index;
  await showModalImage(currentImages[modalImageIndex]);
  document.getElementById("imageModal").style.display = "flex";
  document.body.style.overflow = "hidden";
}

async function showModalImage(img) {
  document.getElementById("modalImg").src = img.url + "?t=" + Date.now();
  document.getElementById("modalImg").alt = img.fileName;
  document.getElementById("modalPrompts").innerHTML = "<div class=\"meta-loading\">در حال بارگذاری اطلاعات...</div>";
  updateModalControls();

  const res = await fetch("/api/gallery/metadata/" + encodeURIComponent(img.id));
  const meta = await res.json();
  document.getElementById("modalPrompts").innerHTML = renderMeta(meta);
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

async function moveModalImage(delta) {
  if (modalImageIndex < 0 || !currentImages.length) return;
  modalImageIndex = (modalImageIndex + delta + currentImages.length) % currentImages.length;
  await showModalImage(currentImages[modalImageIndex]);
}

function showPreviousImage() {
  moveModalImage(-1);
}

function showNextImage() {
  moveModalImage(1);
}

function renderMeta(meta) {
  let html = "";
  if (meta.positive) html += `<div class="prompt-box"><h4>پرامپت مثبت:</h4><div class="copy-box"><pre>${escapeHtml(meta.positive)}</pre><button class="copy-btn" onclick="copyText(this, \`${escapeJs(meta.positive)}\`)">کپی</button></div></div>`;
  if (meta.negative) html += `<div class="prompt-box is-negative"><h4>پرامپت منفی:</h4><div class="copy-box"><pre>${escapeHtml(meta.negative)}</pre><button class="copy-btn" onclick="copyText(this, \`${escapeJs(meta.negative)}\`)">کپی</button></div></div>`;
  if (meta.description) html += `<div class="prompt-box"><h4>توضیحات:</h4><pre>${escapeHtml(meta.description)}</pre></div>`;
  return html || "<p style=\"text-align:center;color:var(--mist)\">اطلاعاتی یافت نشد.</p>";
}

function escapeJs(value) {
  return String(value || "").replace(/\\/g, "\\\\").replace(/`/g, "\\`").replace(/\$/g, "\\$");
}

function closeModal() {
  document.getElementById("imageModal").style.display = "none";
  document.body.style.overflow = "";
  modalImageIndex = -1;
}

function copyText(btn, text) {
  navigator.clipboard.writeText(text).then(() => {
    const old = btn.textContent;
    btn.textContent = "کپی شد";
    setTimeout(() => btn.textContent = old, 1500);
  });
}

window.onclick = event => {
  if (event.target === document.getElementById("imageModal")) closeModal();
};

document.addEventListener("keydown", event => {
  if (document.getElementById("imageModal").style.display !== "flex") return;
  if (event.key === "Escape") closeModal();
  if (event.key === "ArrowLeft") showPreviousImage();
  if (event.key === "ArrowRight") showNextImage();
});

document.addEventListener("DOMContentLoaded", loadRandomImages);
