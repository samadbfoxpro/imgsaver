function copyToClipboard(btn) {
    const copyBox = btn.closest('.copy-box');
    const text = copyBox.getAttribute('data-copy');
    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(text).then(() => {
            const originalText = btn.innerText;
            btn.innerText = '✅ کپی شد!';
            btn.style.background = '#28a745';
            if (typeof showNotification === 'function') showNotification('متن با موفقیت کپی شد!');
            setTimeout(() => {
                btn.innerText = originalText;
                btn.style.background = '';
            }, 2000);
        }).catch(() => {
            fallbackCopy(text, btn);
        });
    } else {
        fallbackCopy(text, btn);
    }

}

function fallbackCopy(text, btn) {
    // ایجاد یک textarea مخفی برای انتخاب متن
    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.setAttribute('readonly', '');
    textarea.style.position = 'absolute';
    textarea.style.left = '-9999px';
    document.body.appendChild(textarea);
    textarea.select();
    try {
        const successful = document.execCommand('copy');
        const originalText = btn.innerText;
        btn.innerText = successful ? '✅ کپی شد!' : 'کپی نشد!';
        btn.style.background = successful ? '#28a745' : '#cf6679';
        if (typeof showNotification === 'function') {
            showNotification(successful ? 'متن با موفقیت کپی شد!' : 'کپی نشد!');
        }
        setTimeout(() => {
            btn.innerText = originalText;
            btn.style.background = '';
        }, 2000);
    } catch (err) {
        if (typeof showNotification === 'function') showNotification('کپی با خطا مواجه شد!');
    }
    document.body.removeChild(textarea);
}

document.querySelectorAll('.select-all').forEach(checkbox => {
    checkbox.addEventListener('change', function() {
        document.querySelectorAll(`input[name="selected_files[]"]`).forEach(cb => {
            cb.checked = this.checked;
        });
    });
});

function deleteSelected(section) {
    const selected = [];
    document.querySelectorAll('input[name="selected_files[]"]:checked').forEach(cb => {
        selected.push(cb.value);
    });

    if (selected.length === 0) {
        alert('هیچ موردی انتخاب نشده است!');
        return;
    }

    if (!confirm(`آیا مطمئنید می‌خواهید ${selected.length} مورد انتخاب‌شده را حذف کنید؟`)) {
        return;
    }

    const form = document.createElement('form');
    form.method = 'POST';
    form.style.display = 'none';

    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = 'delete_selected';
    input.value = '1';
    form.appendChild(input);

    selected.forEach(path => {
        const inp = document.createElement('input');
        inp.type = 'hidden';
        inp.name = 'selected_files[]';
        inp.value = path;
        form.appendChild(inp);
    });

    document.body.appendChild(form);
    form.submit();
}

function filterGallery() {
    const query = document.getElementById('searchInput').value.toLowerCase();
    const cards = document.querySelectorAll('.card');

    cards.forEach(card => {
        const filename = card.getAttribute('data-filename').toLowerCase();
        const positive = card.getAttribute('data-positive').toLowerCase();
        const negative = card.getAttribute('data-negative').toLowerCase();

        if (filename.includes(query) || positive.includes(query) || negative.includes(query)) {
            card.classList.remove('filtered');
        } else {
            card.classList.add('filtered');
        }
    });
}

function openModal(imgSrc) {
    const modal = document.getElementById('imageModal');
    const modalImg = document.getElementById('modalImg');
    modalImg.src = imgSrc;
    modal.style.display = 'flex';
    document.body.style.overflow = 'hidden';
}

function closeModal() {
    document.getElementById('imageModal').style.display = 'none';
    document.body.style.overflow = '';
}

window.onclick = function(event) {
    const modal = document.getElementById('imageModal');
    if (event.target === modal) {
        closeModal();
    }
};

document.addEventListener('keydown', function(event) {
    if (event.key === "Escape") {
        closeModal();
    }
});