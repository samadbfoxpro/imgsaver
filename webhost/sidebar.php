<style>
    .top-menu {
        position: sticky;
        top: 0;
        background: #1e1e1e;
        color: #d4d4d4;
        z-index: 1000;
        padding: 10px 16px;
        box-shadow: 0 2px 10px rgba(0,0,0,0.4);
        display: flex;
        flex-wrap: wrap;
        justify-content: flex-start;
        align-items: center;
        gap: 6px;
        font-size: 0.9rem;
        transition: all 0.3s ease;
    }
    .top-menu a {
        color: #d4d4d4;
        text-decoration: none;
        padding: 8px 14px;
        border-radius: 6px;
        transition: all 0.3s ease;
        font-size: 0.95rem;
        background: #252526;
        border: 1px solid #2d2d2d;
    }
    .top-menu a:hover {
        background: #0078d7;
        color: #ffffff;
        transform: translateY(-2px);
        box-shadow: 0 4px 6px rgba(0,0,0,0.2);
    }

    /* حالت جمع شده: فقط سه تا اولی نشون داده میشه */
    .top-menu.compact a:nth-child(n+4) {
        display: none;
    }

    /* موبایل */
    @media (max-width: 600px) {
        .top-menu {
            justify-content: center;
            font-size: 0.8rem;
            padding: 8px 10px;
        }
        .top-menu a {
            padding: 6px 10px;
            font-size: 0.9rem;
        }
    }
</style>

<nav class="top-menu" id="navbar">
    <a href="index.php">🏠 صفحه اصلی</a>
    <a href="upload-local.php">📤 آپلـود</a>
    <a href="gallery.php">🖼️ گالری</a>
    <a href="manage-all.php">🗂️ مــــدیـــریــت</a>
</nav>

<script>
let prevScrollPos = window.pageYOffset;
let ticking = false;

// متغیرهایی برای ردیابی مسافت اسکرول
let scrollDownThreshold = 0;
let scrollUpThreshold = 0;
const threshold = 100; // حداقل ۱۰۰px برای تغییر منو

function updateNavbar() {
    const currentScrollPos = window.pageYOffset;
    const diff = currentScrollPos - prevScrollPos;

    const navbar = document.getElementById("navbar");

    if (diff > 0) {
        // اسکرول به پایین
        scrollDownThreshold += diff;
        if (scrollDownThreshold > threshold && !navbar.classList.contains("compact")) {
            navbar.classList.add("compact");
            scrollDownThreshold = 0;
            scrollUpThreshold = 0; // ریست مسافت بالا
        }
        scrollUpThreshold = 0; // اگر پایین بره، مسافت بالا ریست میشه
    } else if (diff < 0) {
        // اسکرول به بالا
        scrollUpThreshold -= diff; // diff منفی هست، پس منفیش می‌کنیم
        if (scrollUpThreshold > threshold && navbar.classList.contains("compact")) {
            navbar.classList.remove("compact");
            scrollUpThreshold = 0;
            scrollDownThreshold = 0; // ریست مسافت پایین
        }
        scrollDownThreshold = 0; // اگر بالا بره، مسافت پایین ریست میشه
    }

    prevScrollPos = currentScrollPos;
    ticking = false;
}

window.addEventListener("scroll", function() {
    if (!ticking) {
        requestAnimationFrame(updateNavbar);
        ticking = true;
    }
});
</script>