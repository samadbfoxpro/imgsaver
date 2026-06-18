using System;

namespace imgsaver
{
    public partial class BrowserWindow
    {
        private string GetIconForUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return "*";
            string lower = url.ToLower();
            if (lower.Contains("google")) return "G";
            if (lower.Contains("github")) return "GH";
            if (lower.Contains("youtube")) return "YT";
            if (lower.Contains("facebook")) return "FB";
            if (lower.Contains("twitter") || lower.Contains("x.com")) return "X";
            if (lower.Contains("instagram")) return "IG";
            if (lower.Contains("reddit")) return "RD";
            if (lower.Contains("amazon")) return "AZ";
            if (lower.Contains("netflix")) return "NF";
            if (lower.Contains("spotify")) return "SP";
            if (lower.Contains("seaart")) return "SA";
            if (lower.Contains("civitai")) return "CV";
            if (lower.Contains("pinterest")) return "PT";
            if (lower.Contains("discord")) return "DC";
            return "*";
        }

        private string GetNewTabPageHtml() => """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>New Tab</title>
<style>
:root{color-scheme:dark;--bg:#0e1116;--panel:#161b22;--panel2:#10151d;--border:#283241;--text:#edf2f7;--muted:#8b98a8;--accent:#3ea6ff;--green:#31c46b;--orange:#f7b955}
*{box-sizing:border-box}html,body{height:100%;margin:0}body{font-family:Segoe UI,Inter,Arial,sans-serif;background:radial-gradient(circle at 28% 18%,#1a3148 0,#111820 34%,var(--bg) 76%);color:var(--text);display:flex;align-items:center;justify-content:center;padding:32px}
.shell{width:min(980px,100%);display:grid;gap:22px}.top{display:flex;align-items:end;justify-content:space-between;gap:18px}.brand{display:flex;align-items:center;gap:14px}.mark{width:46px;height:46px;border-radius:12px;background:linear-gradient(135deg,var(--accent),var(--green));display:grid;place-items:center;font-weight:800;color:#061018;box-shadow:0 14px 40px #0008}.title{font-size:30px;font-weight:700;letter-spacing:0}.sub{color:var(--muted);font-size:13px;margin-top:3px}.clock{text-align:right}.time{font-size:28px;font-weight:650}.date{font-size:12px;color:var(--muted)}
.google-entry{background:color-mix(in srgb,var(--panel) 88%,transparent);border:1px solid var(--border);border-radius:10px;display:grid;grid-template-columns:1fr auto;align-items:center;gap:18px;padding:18px 20px;box-shadow:0 18px 48px #0007}.google-entry h1{font-size:18px;margin:0 0 5px}.google-entry p{margin:0;color:var(--muted);font-size:13px}.google-button{display:inline-flex;align-items:center;gap:10px;text-decoration:none;border:1px solid #2f78b7;background:linear-gradient(180deg,#16629b,#11476f);color:#f2fbff;border-radius:8px;padding:13px 20px;font-weight:750;box-shadow:0 12px 30px #0005}.google-button:hover{background:linear-gradient(180deg,#1b75b8,#14537f);border-color:#4da3e8}.gmark{width:24px;height:24px;border-radius:50%;background:#fff;color:#111;display:grid;place-items:center;font-weight:800}
.grid{display:grid;grid-template-columns:1.15fr .85fr;gap:18px}.panel{background:linear-gradient(180deg,color-mix(in srgb,var(--panel) 92%,transparent),color-mix(in srgb,var(--panel2) 94%,transparent));border:1px solid var(--border);border-radius:10px;padding:18px}.panel h2{font-size:13px;text-transform:uppercase;letter-spacing:.08em;color:var(--muted);margin:0 0 14px}.quick{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}.tile{min-height:74px;border:1px solid #263140;background:#111923;border-radius:8px;padding:12px;text-decoration:none;color:var(--text);display:flex;flex-direction:column;justify-content:space-between}.tile:hover{border-color:#3c7fae;background:#132130}.tile b{font-size:14px}.tile small{color:var(--muted);font-size:11px}.stats{display:grid;gap:10px}.stat{display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid #222b38;padding:0 0 10px}.stat:last-child{border-bottom:0;padding-bottom:0}.stat label{color:var(--muted);font-size:12px}.stat strong{font-size:15px}.hint{font-size:12px;color:var(--muted);line-height:1.65;margin-top:14px}.pill{display:inline-flex;align-items:center;gap:7px;border:1px solid #2b3a4c;background:#111923;border-radius:999px;padding:6px 10px;color:#b9c5d3;font-size:12px}
@media(max-width:760px){body{padding:18px}.top{align-items:flex-start;flex-direction:column}.clock{text-align:left}.google-entry{grid-template-columns:1fr}.google-button{justify-content:center}.grid{grid-template-columns:1fr}.quick{grid-template-columns:1fr 1fr}.title{font-size:25px}}
</style>
</head>
<body>
<main class="shell">
  <section class="top">
    <div class="brand"><div class="mark">IS</div><div><div class="title">imgsaver Browser</div><div class="sub">Clean start page for search, downloads, and focused browsing</div></div></div>
    <div class="clock"><div class="time" id="time">--:--</div><div class="date" id="date"></div></div>
  </section>
  <section class="google-entry">
    <div><h1>Start with Google</h1><p>Open Google first, then search normally from the Google page.</p></div>
    <a class="google-button" href="https://www.google.com"><span class="gmark">G</span> Open Google</a>
  </section>
  <section class="grid">
    <div class="panel"><h2>Quick Links</h2><div class="quick">
      <a class="tile" href="https://www.google.com"><b>Google</b><small>Open homepage</small></a>
      <a class="tile" href="https://chat.openai.com"><b>ChatGPT</b><small>Open assistant</small></a>
      <a class="tile" href="https://www.youtube.com"><b>YouTube</b><small>Watch videos</small></a>
      <a class="tile" href="https://github.com"><b>GitHub</b><small>Code workspace</small></a>
      <a class="tile" href="https://mail.google.com"><b>Gmail</b><small>Mail inbox</small></a>
      <a class="tile" href="https://drive.google.com"><b>Drive</b><small>Cloud files</small></a>
    </div></div>
    <aside class="panel"><h2>Session</h2><div class="stats">
      <div class="stat"><label>Status</label><strong>Ready</strong></div>
      <div class="stat"><label>New tab</label><strong>Internal</strong></div>
      <div class="stat"><label>Privacy</label><strong>No file URL</strong></div>
    </div><p class="hint">Type a phrase to search, or enter a domain like <span class="pill">example.com</span>. This page is generated by the app and does not require an external HTML file.</p></aside>
  </section>
</main>
<script>
function tick(){const now=new Date();time.textContent=now.toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'});date.textContent=now.toLocaleDateString([], {weekday:'long', month:'short', day:'numeric'});}tick();setInterval(tick,1000);
</script>
</body>
</html>
""";
    }
}
