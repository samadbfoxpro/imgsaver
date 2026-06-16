let currentImages = [];

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
  notif.style.background = isError ? "#f44336" : "#4CAF50";
  notif.classList.add("show");
  setTimeout(() => notif.classList.remove("show"), 2500);
}

async function loadRandomImages() {
  const grid = document.getElementById("randomGrid");
  grid.innerHTML = "<div class=\"loading\">Loading...</div>";
  const res = await fetch("/api/gallery/random");
  const data = await res.json();
  if (!data.success) {
    showNotification(data.error || "Random failed", true);
    return;
  }
  currentImages = data.images || [];
  renderImages(currentImages);
}

function renderImages(images) {
  const grid = document.getElementById("randomGrid");
  if (!images.length) {
    grid.innerHTML = "<div class=\"loading\">No images.</div>";
    return;
  }
  grid.innerHTML = images.map(img => `
    <div class="random-card" data-id="${img.id}" data-filename="${escapeHtml(img.fileName)}" data-extension="${escapeHtml(img.extension)}">
      <img src="${img.url}" alt="${escapeHtml(img.fileName)}" loading="lazy" onclick="openModal('${img.id}')">
      <div class="filename-display">${escapeHtml(img.fileName)}<br><button class="btn-edit" onclick="toggleEdit(this)">Edit name</button></div>
      <div class="edit-section">
        <input type="text" class="edit-input" value="${escapeHtml(img.baseName)}">
        <div class="edit-btns"><button class="btn btn-save" onclick="saveRename(this)">Save</button><button class="btn btn-cancel" onclick="toggleEdit(this)">Cancel</button></div>
      </div>
    </div>`).join("");
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
    showNotification("Name cannot be empty", true);
    return;
  }

  const res = await fetch("/api/gallery/rename", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ id, newBaseName })
  });
  const data = await res.json();
  if (!data.success) {
    showNotification(data.error || "Rename failed", true);
    return;
  }

  const updated = data.image;
  card.dataset.id = updated.id;
  card.dataset.filename = updated.fileName;
  card.querySelector("img").src = updated.url + "?t=" + Date.now();
  card.querySelector("img").onclick = () => openModal(updated.id);
  card.querySelector(".filename-display").innerHTML = `${escapeHtml(updated.fileName)}<br><button class="btn-edit" onclick="toggleEdit(this)">Edit name</button>`;
  toggleEdit(btn);
  showNotification("Renamed");
  currentImages = currentImages.map(img => img.id === id ? updated : img);
}

async function openModal(id) {
  const img = currentImages.find(i => i.id === id);
  if (!img) return;

  document.getElementById("modalImg").src = img.url + "?t=" + Date.now();
  document.getElementById("modalImg").alt = img.fileName;
  const res = await fetch("/api/gallery/metadata/" + encodeURIComponent(id));
  const meta = await res.json();
  document.getElementById("modalPrompts").innerHTML = renderMeta(meta);
  document.getElementById("imageModal").style.display = "block";
}

function renderMeta(meta) {
  let html = "";
  if (meta.positive) html += `<div class="prompt-box"><h4>Positive Prompt:</h4><div class="copy-box"><pre>${escapeHtml(meta.positive)}</pre><button class="copy-btn" onclick="copyText(this, \`${escapeJs(meta.positive)}\`)">Copy</button></div></div>`;
  if (meta.negative) html += `<div class="prompt-box"><h4>Negative Prompt:</h4><div class="copy-box"><pre>${escapeHtml(meta.negative)}</pre><button class="copy-btn" onclick="copyText(this, \`${escapeJs(meta.negative)}\`)">Copy</button></div></div>`;
  if (meta.description) html += `<div class="prompt-box"><h4>Description:</h4><pre>${escapeHtml(meta.description)}</pre></div>`;
  return html || "<p style=\"text-align:center;color:#aaa\">No metadata found.</p>";
}

function escapeJs(value) {
  return String(value || "").replace(/\\/g, "\\\\").replace(/`/g, "\\`").replace(/\$/g, "\\$");
}

function closeModal() {
  document.getElementById("imageModal").style.display = "none";
}

function copyText(btn, text) {
  navigator.clipboard.writeText(text).then(() => {
    const old = btn.textContent;
    btn.textContent = "Copied";
    setTimeout(() => btn.textContent = old, 1500);
  });
}

window.onclick = event => {
  if (event.target === document.getElementById("imageModal")) closeModal();
};

document.addEventListener("DOMContentLoaded", loadRandomImages);
