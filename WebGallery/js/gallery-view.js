let days = [];
let currentPage = 1;
let currentImages = [];
let visibleCount = 10;

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
  notif.style.background = isError ? "#cf6679" : "linear-gradient(90deg,#43a047 60%,#388e3c 100%)";
  notif.classList.add("show");
  setTimeout(() => notif.classList.remove("show"), 1700);
}

async function reloadAll() {
  const res = await fetch("/api/gallery/days");
  const data = await res.json();
  days = data.days || [];
  renderCalendar();

  if (!days.length) {
    document.getElementById("content").innerHTML = "<p class=\"no-data\">No gallery images found. Set the desktop gallery path first.</p>";
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
  document.getElementById("content").innerHTML = "<div class=\"loading\">Loading...</div>";
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

  document.getElementById("content").innerHTML = "<div class=\"loading\">Searching...</div>";
  const res = await fetch("/api/gallery/images?q=" + encodeURIComponent(q) + "&limit=500");
  const data = await res.json();
  currentImages = data.images || [];
  visibleCount = currentImages.length;
  document.getElementById("content").innerHTML =
    `<div class="day-header">Search results for "${escapeHtml(q)}" - ${currentImages.length} result</div><div class="gallery-grid">${currentImages.map(renderCard).join("")}</div>`;
}

function renderDay(day) {
  const shown = currentImages.slice(0, visibleCount);
  document.getElementById("content").innerHTML = `
    <div class="day-header">${escapeHtml(day.date)} - ${currentImages.length} file</div>
    <div class="download-section">
      <strong>Download items for this day</strong>
      <span style="color:#ffcc00">Text files are available on each card when present.</span>
    </div>
    <div class="bulk-actions">
      <label><input type="checkbox" class="select-all" onchange="toggleSelectAll(this)"> Select all visible</label>
      <button class="delete-btn" onclick="deleteSelected()">Delete selected</button>
    </div>
    <div class="gallery-grid" id="galleryGrid">${shown.map(renderCard).join("")}</div>
    ${visibleCount < currentImages.length ? "<button class=\"load-more-btn\" onclick=\"loadMore()\">Load more</button>" : ""}
    ${renderPagination()}
    <div style="text-align:center;margin-top:20px;color:#aaa;font-size:.9rem">Page ${currentPage} of ${days.length} - ${escapeHtml(day.date)}</div>
  `;
}

function renderPagination() {
  const prev = currentPage > 1 ? `<button onclick="loadPage(${currentPage - 1})">Previous day</button>` : "<span class=\"disabled\">Previous day</span>";
  const next = currentPage < days.length ? `<button onclick="loadPage(${currentPage + 1})">Next day</button>` : "<span class=\"disabled\">Next day</span>";
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
      <button class="delete-single-btn" onclick="deleteImages(['${img.id}'])">x</button>
      <input type="checkbox" class="select-checkbox" name="selected_files[]" value="${img.id}">
      <div class="image-wrapper">
        <img src="${img.url}" alt="${escapeHtml(img.fileName)}" onclick="openModal('${img.url}')" style="cursor:zoom-in" loading="lazy" class="lazy" onload="this.classList.add('lazy-loaded')">
      </div>
      <div class="filename-display">${escapeHtml(img.fileName)}</div>
      ${renderPrompt("Positive Prompt", img.positive)}
      ${renderPrompt("Negative Prompt", img.negative)}
      ${img.description ? `<div class="prompt-box"><h4>Description:</h4><pre>${escapeHtml(img.description)}</pre></div>` : ""}
      <div class="card-actions">
        <a class="download-btn" href="${img.url}" download>Download image</a>
        ${img.textUrl ? `<a class="download-btn" href="${img.textUrl}" download>Download text file</a>` : ""}
      </div>
    </div>`;
}

function renderPrompt(label, value) {
  return `<div class="prompt-box"><h4>${label}:</h4><div class="copy-box" data-copy="${escapeHtml(value)}"><pre>${escapeHtml(value)}</pre><button class="copy-btn" onclick="copyToClipboard(this)">Copy</button></div></div>`;
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
    alert("Nothing selected.");
    return;
  }
  deleteImages(ids);
}

async function deleteImages(ids) {
  if (!confirm(`Delete ${ids.length} item(s)?`)) return;
  const res = await fetch("/api/gallery/delete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ids })
  });
  const data = await res.json();
  if (!data.success) {
    showNotification(data.error || "Delete failed", true);
    return;
  }
  showNotification("Deleted");
  await reloadAll();
}

function copyToClipboard(btn) {
  const text = btn.closest(".copy-box").dataset.copy || "";
  navigator.clipboard.writeText(text).then(() => {
    const old = btn.innerText;
    btn.innerText = "Copied";
    btn.style.background = "#28a745";
    showNotification("Copied");
    setTimeout(() => {
      btn.innerText = old;
      btn.style.background = "";
    }, 1500);
  });
}

function openModal(src) {
  document.getElementById("modalImg").src = src;
  document.getElementById("imageModal").style.display = "flex";
  document.body.style.overflow = "hidden";
}

function closeModal() {
  document.getElementById("imageModal").style.display = "none";
  document.body.style.overflow = "";
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
  if (event.key === "Escape") closeModal();
});

reloadAll();
