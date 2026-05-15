using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input; // برای KeyEventArgs
using System.Runtime.InteropServices; // برای Marshal

namespace imgsaver
{
    // ✅ کلاس اصلی سرور
    public class RemoteServer : IDisposable
    {
        private HttpListener? _listener;
        private bool _isRunning;
        private CancellationTokenSource? _cts;
        public const int Port = 9899;

        public event Action<string>? StatusChanged;
        public bool IsRunning => _isRunning;

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _cts = new CancellationTokenSource();
                _listener = new HttpListener();

                // ✅ پیکربندی Prefixها برای دسترسی از همه آدرس‌ها
                _listener.Prefixes.Add($"http://+:{Port}/");
                _listener.Prefixes.Add($"http://*:{Port}/");
                _listener.Prefixes.Add($"http://localhost:{Port}/");

                string ip = GetLocalIPAddress();
                _listener.Prefixes.Add($"http://{ip}:{Port}/");

                _listener.Start();
                _isRunning = true;

                // ✅ اجرای Listener در بک‌گراند
                Task.Run(() => ListenAsync(_cts.Token));

                StatusChanged?.Invoke($"✅ Server running at http://{ip}:{Port}");
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5)
            {
                System.Windows.MessageBox.Show("❌ دسترسی رد شد!\nلطفاً برنامه را به صورت Run as Administrator اجرا کنید.",
                    "خطای سرور", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                Stop();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"❌ خطا در راه‌اندازی سرور:\n{ex.Message}",
                    "خطا", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Stop();
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cts?.Cancel();

            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
            _cts?.Dispose();
            _cts = null;

            StatusChanged?.Invoke("🛑 Server stopped");
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (_isRunning && !token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener!.GetContextAsync();
                    // ✅ پردازش هر درخواست در تسک جداگانه برای جلوگیری از بلاک شدن
                    _ = Task.Run(() => ProcessRequest(context), token);
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) { break; }
                catch (OperationCanceledException) { break; }
                catch { /* خطاهای دیگر را نادیده بگیر برای توقف نرم */ }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                string path = request.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? "/";

                if (request.HttpMethod == "GET")
                {
                    switch (path)
                    {
                        case "/clipboard":
                            ServeJson(response, new { text = GetClipboardText() });
                            break;
                        case "/status":
                            ServeJson(response, new { status = _isRunning ? "running" : "offline", port = Port });
                            break;
                        default:
                            ServeHtml(response, GetHtmlInterface());
                            break;
                    }
                }
                else if (request.HttpMethod == "POST")
                {
                    HandlePost(request, response);
                }
                else
                {
                    response.StatusCode = 405; // Method Not Allowed
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = 500;
                ServeText(response, $"Error: {ex.Message}");
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        // ✅ متدهای کمکی برای ارسال پاسخ
        private void ServeJson<T>(HttpListenerResponse response, T data)
        {
            string json = JsonSerializer.Serialize(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private void ServeHtml(HttpListenerResponse response, string html)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private void ServeText(HttpListenerResponse response, string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            response.ContentType = "text/plain; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        // ✅ دریافت متن کلیپ‌بورد با ایمن‌سازی Thread
        private string GetClipboardText()
        {
            string result = string.Empty;
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (System.Windows.Clipboard.ContainsText())
                            result = System.Windows.Clipboard.GetText();
                    }
                    catch { /* دسترسی به کلیپ‌بورد ممکن است رد شود */ }
                });
            }
            return result;
        }

        // ✅ پردازش درخواست‌های POST
        private void HandlePost(HttpListenerRequest request, HttpListenerResponse response)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            string rawData = reader.ReadToEnd();

            // ✅ پارس کردن دستی form-urlencoded
            var formData = new Dictionary<string, string>();
            foreach (var pair in rawData.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    string key = WebUtility.UrlDecode(parts[0]);
                    string value = WebUtility.UrlDecode(parts[1]);
                    formData[key] = value;
                }
            }

            if (formData.TryGetValue("action", out string? action) &&
                formData.TryGetValue("value", out string? actionValue))
            {
                // ✅ اجرا در ترد اصلی UI برای دسترسی به کلیپ‌بورد و Input
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ExecuteAction(action, actionValue);
                });
            }

            response.StatusCode = 200;
            ServeText(response, "OK");
        }

        // ✅ اجرای دستورها بر اساس action
        private void ExecuteAction(string action, string value)
        {
            try
            {
                switch (action)
                {
                    case "type":
                        if (!string.IsNullOrEmpty(value))
                            InputSimulator.SimulateTextEntry(value);
                        break;
                    case "pasteText":
                        if (!string.IsNullOrEmpty(value))
                        {
                            System.Windows.Clipboard.SetText(value);
                            System.Threading.Thread.Sleep(50); // ⏱ زمان برای ست شدن کلیپ‌بورد
                            InputSimulator.SimulatePaste();
                        }
                        break;
                    case "key":
                        ExecuteKeyCommand(value);
                        break;
                }
            }
            catch { /* خطاها را نادیده بگیر تا سرور کرش نکند */ }
        }

        // ✅ اجرای دستورات کیبورد
        private void ExecuteKeyCommand(string key)
        {
            switch (key?.ToLowerInvariant())
            {
                case "enter": InputSimulator.SimulateEnter(); break;
                case "backspace": InputSimulator.SimulateBackspace(1); break;
                case "tab": InputSimulator.SimulateTab(); break;
                case "undo": InputSimulator.SimulateUndo(); break;
                case "redo": InputSimulator.SimulateRedo(); break;
                case "copy": InputSimulator.SimulateCopy(); break;
                case "cut": InputSimulator.SimulateCut(); break;
                case "paste": InputSimulator.SimulatePaste(); break;
                case "selectall": InputSimulator.SimulateSelectAll(); break;
                case "win": InputSimulator.SimulateWinKey(); break;
                case "ctrl": InputSimulator.SimulateCtrlKey(); break;
            }
        }

        // ✅ دریافت IP محلی معتبر (بدون VPN)
        public string GetLocalIPAddress()
        {
            try
            {
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                // اولویت با شبکه‌های واقعی (Wi-Fi / Ethernet)
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType is not (System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 or
                                                       System.Net.NetworkInformation.NetworkInterfaceType.Ethernet)) continue;

                    if (IsVpnInterface(ni)) continue;

                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string addr = ip.Address.ToString();
                            if (IsPrivateIP(addr)) return addr;
                        }
                    }
                }

                // fallback به هر آدرس غیر لوکال‌هاست
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (IsVpnInterface(ni)) continue;

                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(ip.Address))
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private bool IsPrivateIP(string ip) =>
            ip.StartsWith("192.168.") ||
            ip.StartsWith("10.") ||
            (ip.StartsWith("172.") && int.TryParse(ip.Split('.')[1], out int b) && b >= 16 && b <= 31);

        private bool IsVpnInterface(System.Net.NetworkInformation.NetworkInterface ni)
        {
            string name = ni.Name.ToLower();
            string desc = ni.Description.ToLower();
            return name.Contains("vpn") || name.Contains("tun") || name.Contains("tap") || name.Contains("proton") ||
                   desc.Contains("vpn") || desc.Contains("tun") || desc.Contains("tap") || desc.Contains("proton");
        }

        // ✅ صفحه HTML کنترل ریموت (مینیمال و بهینه)
        private string GetHtmlInterface() => @"<!DOCTYPE html>
<html lang='en'><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no'>
<title>Remote Control</title><style>
:root{--bg:#0f172a;--card:#1e293b;--primary:#3b82f6;--accent:#8b5cf6;--text:#f8fafc;--muted:#94a3b8;--border:#334155}
*{box-sizing:border-box;margin:0;padding:0;-webkit-tap-highlight-color:transparent}
body{font-family:system-ui,sans-serif;background:var(--bg);color:var(--text);min-height:100vh;display:flex;align-items:center;justify-content:center;padding:20px}
.container{width:100%;max-width:600px;background:var(--card);border-radius:20px;padding:24px;border:1px solid var(--border)}
h1{font-size:1.5rem;margin-bottom:8px;background:linear-gradient(90deg,var(--primary),var(--accent));-webkit-background-clip:text;color:transparent}
.subtitle{color:var(--muted);font-size:0.9rem;margin-bottom:20px;line-height:1.5}
textarea{width:100%;min-height:150px;padding:12px;border-radius:12px;border:1px solid var(--border);background:#020617;color:var(--text);font-size:1rem;resize:vertical;margin-bottom:12px}
.mode-row{display:flex;gap:12px;margin-bottom:16px}
.mode-item{flex:1;padding:10px 14px;border:1px solid var(--border);border-radius:10px;text-align:center;cursor:pointer;background:rgba(255,255,255,0.03);font-size:0.85rem}
.mode-item.active{border-color:var(--primary);background:rgba(59,130,246,0.15)}
.mode-item input{margin-right:6px}
.btn-row{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-bottom:20px}
.btn{padding:12px;border:none;border-radius:12px;font-weight:600;cursor:pointer;font-size:0.9rem;color:white}
.btn-primary{background:var(--primary)}.btn-accent{background:var(--accent)}.btn-danger{background:#ef4444}
.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:10px}
.grid button{padding:14px 8px;border:none;border-radius:14px;background:#0f172a;color:var(--text);cursor:pointer;display:flex;flex-direction:column;align-items:center;gap:6px;font-size:0.8rem}
.grid button:hover{background:rgba(59,130,246,0.2)}
.grid svg{width:20px;height:20px}
.toast{position:fixed;bottom:20px;right:20px;background:var(--card);padding:12px 18px;border-radius:12px;border:1px solid var(--border);opacity:0;transform:translateY(10px);transition:all 0.2s;pointer-events:none}
.toast.show{opacity:1;transform:translateY(0)}
</style></head><body>
<div class='container'>
<h1>🎮 Remote Control</h1>
<p class='subtitle'>Type or paste text to send to your Windows PC</p>
<textarea id='txt' placeholder='Type here...'></textarea>
<div class='mode-row'>
<label class='mode-item active'><input type='radio' name='mode' value='live' checked>⚡ Live</label>
<label class='mode-item'><input type='radio' name='mode' value='paste'>📋 Paste Mode</label>
</div>
<div class='btn-row'>
<button class='btn btn-accent' onclick='getClip()'>📥 Get Clipboard</button>
<button class='btn btn-primary' onclick='send()'>📤 Send</button>
<button class='btn btn-danger' onclick='clearTxt()'>🗑 Clear</button>
</div>
<div class='grid'>
<button onclick='key(""undo"")'><svg viewBox='0 0 24 24'><path fill='currentColor' d='M12.5 8c-2.65 0-5.05.99-6.9 2.6L2 7v9h9l-3.62-3.62c1.39-1.16 3.16-1.88 5.12-1.88 3.54 0 6.55 2.31 7.6 5.5l2.37-.78C21.08 11.03 17.15 8 12.5 8z'/></svg>Undo</button>
<button onclick='key(""redo"")'><svg viewBox='0 0 24 24'><path fill='currentColor' d='M18.4 10.6C16.55 8.99 14.15 8 11.5 8c-4.65 0-8.58 3.03-9.96 7.22L3.9 16c1.05-3.19 4.05-5.5 7.6-5.5 1.95 0 3.73.72 5.12 1.88L13 16h9V7l-3.6 3.6z'/></svg>Redo</button>
<button onclick='key(""selectall"")'><svg viewBox='0 0 24 24'><path fill='currentColor' d='M3 5v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2zm16 14H5V5h14v14z'/></svg>Select</button>
<button class='btn-danger' onclick='key(""backspace"")'><svg viewBox='0 0 24 24'><path fill='currentColor' d='M22 3H7c-.69 0-1.23.35-1.59.88L0 12l5.41 8.11c.36.53.9.89 1.59.89h15c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-3 12.59L17.59 17 14 13.41 10.41 17 9 15.59 12.59 12 9 8.41 10.41 7 14 10.59 17.59 7 19 8.41 15.41 12 19 15.59z'/></svg>Del</button>
<button onclick='key(""copy"")'><svg viewBox='0 0 24 24'><path fill='currentColor' d='M16 1H4v14h2V3h12V1zm3 4H8v14h11V7zm0 16H8V7h11v14z'/></svg>Copy</button>
<button onclick='key(""cut"")'><svg viewBox='0 0 24 24'><path fill='currentColor' d='M9.64 7.64c.23-.5.36-1.05.36-1.64 0-2.21-1.79-4-4-4S2 3.79 2 6s1.79 4 4 4c.59 0 1.14-.13 1.64-.36L10 12l-2.36 2.36C7.14 14.13 6.59 14 6 14c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4c0-.59-.13-1.14-.36-1.64L12 14l7 7h3v-1L9.64 7.64z'/></svg>Cut</button>
<button class='btn-accent' onclick='key(""paste"")'><svg viewBox='0 0 24 24'><path fill='currentColor' d='M19 2h-4.18C14.4.84 13.3 0 12 0c-1.3 0-2.4.84-2.82 2H5c-1.1 0-2 .9-2 2v16h14V4c0-1.1-.9-2-2-2zm-7 0c.55 0 1 .45 1 1s-.45 1-1 1z'/></svg>Paste</button>
<button class='btn-primary' style='grid-column:span 4' onclick='key(""enter"")'><svg viewBox='0 0 24 24'><path fill='currentColor' d='M19 7v4H5.83l3.58-3.59L8 6l-6 6 6 6 1.41-1.41L5.83 13H21V7z'/></svg>Enter</button>
</div>
</div>
<div id='toast' class='toast'></div>
<script>
const txt=document.getElementById('txt'),toast=document.getElementById('toast');
let mode='live',debounce;

document.querySelectorAll('input[name=mode]').forEach(r=>{
r.onchange=()=>{mode=r.value;toastMsg(mode==='live'?'⚡ Live mode':'📋 Paste mode');r.closest('.mode-item').classList.toggle('active',r.checked);};
});

txt.addEventListener('input',e=>{if(mode!=='live')return;clearTimeout(debounce);debounce=setTimeout(()=>sendAction('type',e.data||''),100);});
txt.addEventListener('keydown',e=>{if(mode!=='live')return;if(e.key==='Enter'){e.preventDefault();sendAction('key','enter');}});

function send(){if(mode==='paste'){if(!txt.value)return toastMsg('⚠ Text is empty');sendAction('pasteText',txt.value);toastMsg('📤 Sent via clipboard');}else{toastMsg('⚡ Live mode: just type!');}}
function getClip(){fetch('/clipboard').then(r=>r.json()).then(d=>{txt.value=d.text||'';toastMsg(d.text?'📥 Clipboard received':'⚠ Empty clipboard');}).catch(()=>toastMsg('❌ Connection failed'));}
function clearTxt(){txt.value='';toastMsg('🗑 Cleared');}
function key(k){sendAction('key',k);}
function sendAction(action,value){fetch('/',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:`action=${action}&value=${encodeURIComponent(value)}`}).catch(()=>toastMsg('❌ Send failed'));}
function toastMsg(msg){toast.textContent=msg;toast.classList.add('show');clearTimeout(window.tid);window.tid=setTimeout(()=>toast.classList.remove('show'),2500);}
</script></body></html>";

        // ✅ پیاده‌سازی IDisposable برای پاکسازی منابع
        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        ~RemoteServer() => Dispose();
    }
}