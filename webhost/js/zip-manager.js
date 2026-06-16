// اسکریپت مدیریت تغییر نام فایل زیپ (modal)
let renameTargetForm = null;
function openRenameDialog(btn, filename) {
    renameTargetForm = btn.closest('form');
    var base = filename.replace(/\.zip$/i, '');
    document.getElementById('modalNewName').value = base;
    document.getElementById('modalOldName').value = filename;
    document.getElementById('modalError').textContent = '';
    document.getElementById('renameModal').style.display = 'flex';
    setTimeout(()=>{
      document.getElementById('modalNewName').focus();
    }, 100);
}
function closeRenameModal() {
    document.getElementById('renameModal').style.display = 'none';
    renameTargetForm = null;
}
document.addEventListener('DOMContentLoaded', function() {
    document.getElementById('modalRenameForm').onsubmit = function(e) {
        e.preventDefault();
        var newBase = document.getElementById('modalNewName').value.trim();
        if (!/^[\w\-.]{1,60}$/.test(newBase)) {
            document.getElementById('modalError').textContent = 'نام معتبر وارد کنید (فقط حروف، عدد، خط تیره و نقطه)';
            return false;
        }
        if (renameTargetForm) {
            renameTargetForm.newname.value = newBase + '.zip';
            renameTargetForm.submit();
        }
        closeRenameModal();
        return false;
    };
    window.addEventListener('keydown', function(e) {
        if (e.key === 'Escape') closeRenameModal();
    });
});
