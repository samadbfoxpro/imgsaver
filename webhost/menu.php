<?php
header('Content-Type: text/html; charset=utf-8');
?>
<div id="glass-menu" style="direction: rtl; position: fixed; bottom: 20px; left: 20px; z-index: 10000; font-family: 'IRANSans', 'Vazir', Tahoma, sans-serif;">
  <button id="menu-toggle" class="glass-button">
    <span class="hamburger-icon">☰</span>
  </button>

  <div id="menu-content" class="glass-content">
    <nav>
      <ul id="menu-list">
        <li><a href="upload-local.php">آپلود محلی</a></li>
        <li><a href="gallery-view.php">گالری عکس‌های آپلود شده</a></li>
        <li><a href="random-gallery.php">گالری تصادفی</a></li>
        <li><a href="naming-manager.php">مدیریت نام‌گذاری فایل‌ها</a></li>
        <li><a href="daily-uploads.php">عکس‌های روز (بر اساس تاریخ امروز)</a></li>
        <li><a href="tel.php">ارسال به تلگرام</a></li>
        <li><a href="manage-files.php">مدیریت فایل‌های گالری</a></li>
        <li><a href="zip-manager.php">مدیریت فایل‌های ZIP</a></li>
        <li><a href="fix.php">مدیریت فایل‌های گالری (Fix)</a></li>
      </ul>
    </nav>
  </div>
</div>

<style>
  @font-face {
    font-family: 'IRANSans';
    src: url('fonts/IRANSans.ttf') format('truetype');
    font-weight: normal;
    font-style: normal;
  }

  #glass-menu {
    display: block !important;
    direction: rtl;
    position: fixed;
    bottom: 20px;
    left: 20px;
    z-index: 10000;
    font-family: 'IRANSans', 'Vazir', Tahoma, sans-serif;
  }

  .glass-button {
    all: unset;
    display: flex;
    align-items: center;
    justify-content: center;
    width: 50px;
    height: 50px;
    border-radius: 16px;
    background: rgba(255, 255, 255, 0.15);
    backdrop-filter: blur(10px);
    -webkit-backdrop-filter: blur(10px);
    border: 1px solid rgba(255, 255, 255, 0.2);
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
    cursor: pointer;
    transition: all 0.3s ease;
    color: white;
    font-size: 24px;
    position: relative;
    z-index: 10001;
  }

  .glass-button:hover {
    background: rgba(255, 255, 255, 0.25);
  }

  .glass-content {
    position: absolute;
    bottom: 65px;
    left: 0;
    width: 280px;
    max-width: calc(100vw - 40px);
    background: rgba(255, 255, 255, 0.1);
    backdrop-filter: blur(12px);
    -webkit-backdrop-filter: blur(12px);
    border: 1px solid rgba(255, 255, 255, 0.18);
    border-radius: 20px;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.15);
    overflow: hidden;
    transform: translateY(10px) scale(0.95);
    opacity: 0;
    visibility: hidden;
    transition: all 0.35s cubic-bezier(0.34, 1.56, 0.64, 1);
    height: auto;
  }

  .glass-content.open {
    transform: translateY(0) scale(1);
    opacity: 1;
    visibility: visible;
  }

  .glass-content ul {
    list-style: none;
    padding: 0;
    margin: 0;
    display: flex;
    flex-direction: column;
    transition: height 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
    height: 168px; /* 3 × 56px — مقدار اولیه */
    overflow: hidden;
  }

  .glass-content li {
    padding: 0;
    margin: 0;
    position: relative;
  }

  .glass-content li:not(:first-child)::before {
    content: '';
    position: absolute;
    top: 0;
    right: 20px;
    left: 20px;
    height: 1px;
    background: rgba(255, 255, 255, 0.15);
  }

  .glass-content a {
    display: block;
    padding: 16px 20px;
    color: white;
    text-decoration: none;
    font-size: 17px;
    transition: background 0.2s ease;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .glass-content a:hover {
    background: rgba(255, 255, 255, 0.1);
  }

  .hamburger-icon {
    display: block;
    transition: transform 0.3s ease;
  }

  .glass-button.open .hamburger-icon {
    transform: rotate(45deg);
  }

  .glass-button.open .hamburger-icon::before,
  .glass-button.open .hamburger-icon::after {
    transform: rotate(90deg);
    opacity: 0;
  }

  @media (max-width: 768px) {
    .glass-content {
      width: 260px;
      max-width: calc(100vw - 30px);
    }
    .glass-button {
      width: 52px;
      height: 52px;
    }
    .glass-content a {
      font-size: 18px;
      padding: 18px 20px;
    }
  }
</style>

<script>
(function() {
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initMenu);
  } else {
    initMenu();
  }

  function initMenu() {
    const menuContainer = document.getElementById('glass-menu');
    const toggleBtn = menuContainer.querySelector('#menu-toggle');
    const menuContent = menuContainer.querySelector('#menu-content');
    const menuList = menuContainer.querySelector('#menu-list');
    const items = Array.from(menuList.children);
    const itemHeight = 56; // شامل padding و فاصله
    const minItems = 3;
    const maxItems = items.length;

    let currentVisible = minItems;

    // تنظیم ارتفاع اولیه
    menuList.style.height = (minItems * itemHeight) + 'px';

    function openMenu() {
      menuContent.classList.add('open');
      toggleBtn.classList.add('open');
      currentVisible = minItems;
      menuList.style.height = (minItems * itemHeight) + 'px';
      document.body.style.overflow = 'hidden';
    }

    function closeMenu() {
      menuContent.classList.remove('open');
      toggleBtn.classList.remove('open');
      document.body.style.overflow = '';
    }

    toggleBtn.addEventListener('click', () => {
      if (menuContent.classList.contains('open')) {
        closeMenu();
      } else {
        openMenu();
      }
    });

    document.addEventListener('click', (e) => {
      if (!menuContainer.contains(e.target) && menuContent.classList.contains('open')) {
        closeMenu();
      }
    });

    // ========== Touch (Mobile) ==========
    let startY = 0;
    let startVisible = 0;

    menuList.addEventListener('touchstart', (e) => {
      if (!menuContent.classList.contains('open')) return;
      startY = e.touches[0].clientY;
      startVisible = currentVisible;
    }, { passive: true });

    menuList.addEventListener('touchmove', (e) => {
      if (!menuContent.classList.contains('open')) return;
      if (!startY) return;

      const deltaY = e.touches[0].clientY - startY;
      const deltaItems = Math.round(deltaY / itemHeight);
      let newVisible = startVisible - deltaItems;
      newVisible = Math.max(minItems, Math.min(maxItems, newVisible));

      if (newVisible !== currentVisible) {
        currentVisible = newVisible;
        menuList.style.height = (currentVisible * itemHeight) + 'px';
      }

      e.preventDefault();
    }, { passive: false });

    menuList.addEventListener('touchend', () => {
      startY = 0;
    });

    // ========== Mouse Wheel (Desktop) ==========
    let wheelTimeout = null;

    menuList.addEventListener('wheel', (e) => {
      if (!menuContent.classList.contains('open')) return;

      clearTimeout(wheelTimeout);
      const direction = e.deltaY > 0 ? -1 : 1;
      let newVisible = currentVisible + direction;
      newVisible = Math.max(minItems, Math.min(maxItems, newVisible));

      if (newVisible !== currentVisible) {
        currentVisible = newVisible;
        menuList.style.height = (currentVisible * itemHeight) + 'px';
      }

      // جلوگیری از اسکرول صفحه
      e.preventDefault();
    }, { passive: false });
  }
})();
</script>
