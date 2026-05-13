using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace imgsaver
{
    public class RemoteServer
    {
        private HttpListener _listener;
        private bool _isRunning;
        private const int Port = 9899;

        public event Action<string> StatusChanged;

        public bool IsRunning => _isRunning;

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                string ip = GetLocalIPAddress();
                // Add multiple redundant prefixes
                _listener.Prefixes.Add($"http://+:{Port}/");
                _listener.Prefixes.Add($"http://*:{Port}/");
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Prefixes.Add($"http://{ip}:{Port}/");
                
                _listener.Start();
                _isRunning = true;
                
                Task.Run(() => Listen());
                StatusChanged?.Invoke($"Server running at {ip}:{Port}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not start server (Try Run as Admin):\n{ex.Message}");
                _isRunning = false;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            StatusChanged?.Invoke("Server stopped");
        }

        private async Task Listen()
        {
            while (_isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch { /* Quiet stop */ }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                if (request.HttpMethod == "GET")
                {
                    ServeInterface(response);
                }
                else if (request.HttpMethod == "POST")
                {
                    HandlePost(request, response);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    response.StatusCode = 500;
                    byte[] buffer = Encoding.UTF8.GetBytes(ex.Message);
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                } catch { }
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        private void ServeInterface(HttpListenerResponse response)
        {
            string html = GetHtmlInterface();
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private void HandlePost(HttpListenerRequest request, HttpListenerResponse response)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string data = reader.ReadToEnd();
                string action = "";
                string value = "";

                // Manual parsing to avoid System.Web dependency
                var pairs = data.Split('&');
                foreach (var pair in pairs)
                {
                    var kvp = pair.Split('=');
                    if (kvp.Length == 2)
                    {
                        string key = kvp[0];
                        string val = WebUtility.UrlDecode(kvp[1]);
                        if (key == "action") action = val;
                        else if (key == "value") value = val;
                    }
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (action)
                    {
                        case "type":
                            InputSimulator.SimulateTextEntry(value);
                            break;
                        case "key":
                            HandleKeyAction(value);
                            break;
                    }
                });

                response.StatusCode = 200;
                byte[] buffer = Encoding.UTF8.GetBytes("OK");
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
        }

        private void HandleKeyAction(string key)
        {
            switch (key)
            {
                case "enter": InputSimulator.SimulateEnter(); break;
                case "backspace": InputSimulator.SimulateBackspace(1); break;
                case "undo": InputSimulator.SimulateUndo(); break;
                case "redo": InputSimulator.SimulateRedo(); break;
                case "copy": InputSimulator.SimulateCopy(); break;
                case "cut": InputSimulator.SimulateCut(); break;
                case "paste": InputSimulator.SimulatePaste(); break;
                case "selectall": InputSimulator.SimulateSelectAll(); break;
                case "tab": InputSimulator.SimulateTab(); break;
                case "win": InputSimulator.SimulateWinKey(); break;
                case "ctrl": InputSimulator.SimulateCtrlKey(); break;
            }
        }

        public string GetLocalIPAddress()
        {
            try
            {
                // Prioritize 192.168.x.x, 10.x.x.x, etc. and ignore VPN/Tunnel
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                
                // First pass: look specifically for Wi-Fi or Ethernet with a private IP
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 && 
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Ethernet) continue;

                    // Strictly avoid VPN keywords
                    string name = ni.Name.ToLower();
                    string desc = ni.Description.ToLower();
                    if (name.Contains("vpn") || name.Contains("tun") || name.Contains("tap") || name.Contains("proton") ||
                        desc.Contains("vpn") || desc.Contains("tun") || desc.Contains("tap") || desc.Contains("proton")) continue;

                    var props = ni.GetIPProperties();
                    foreach (var ip in props.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string addr = ip.Address.ToString();
                            // Strongly prefer standard private LAN ranges
                            if (addr.StartsWith("192.168.") || addr.StartsWith("10.") || addr.StartsWith("172."))
                                return addr;
                        }
                    }
                }

                // Second pass: fallback to any active inter-network interface that isn't loopback/VPN
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    string name = ni.Name.ToLower();
                    if (name.Contains("vpn") || name.Contains("tun") || name.Contains("tap") || name.Contains("proton")) continue;

                    var props = ni.GetIPProperties();
                    foreach (var ip in props.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address))
                            return ip.Address.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private string GetHtmlInterface()
        {
            return @"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
    <title>Remote Control</title>
    <style>
        :root {
            --bg: #0f172a;
            --card: #1e293b;
            --primary: #3b82f6;
            --primary-hover: #2563eb;
            --accent: #8b5cf6;
            --text: #f8fafc;
            --text-dim: #94a3b8;
            --danger: #ef4444;
        }
        * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; }
        body { 
            font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; 
            background: var(--bg); color: var(--text); margin: 0; padding: 20px;
            display: flex; flex-direction: column; align-items: center; min-height: 100vh;
        }
        .container {
            width: 100%; max-width: 500px; background: var(--card); border-radius: 24px; padding: 24px;
            box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5); border: 1px solid rgba(255,255,255,0.05);
        }
        h1 { 
            text-align: center; font-size: 1.5rem; margin-bottom: 24px;
            background: linear-gradient(to right, #3b82f6, #8b5cf6);
            -webkit-background-clip: text; -webkit-text-fill-color: transparent; font-weight: 800;
        }
        #textInput { 
            width: 100%; padding: 16px; border-radius: 16px; border: 2px solid #334155;
            background: #0f172a; color: white; font-size: 18px; margin-bottom: 24px;
            outline: none; transition: border-color 0.2s;
        }
        #textInput:focus { border-color: var(--primary); }
        .section-label { font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.1em; color: var(--text-dim); margin-bottom: 12px; font-weight: 700; }
        .button-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 24px; }
        button { 
            aspect-ratio: 1; border: none; border-radius: 16px; background: #334155; color: white; 
            font-size: 13px; font-weight: 600; display: flex; flex-direction: column; align-items: center;
            justify-content: center; gap: 6px; cursor: pointer; transition: all 0.2s;
        }
        button:active { transform: scale(0.92); background: var(--primary); }
        button svg { width: 22px; height: 22px; fill: currentColor; }
        .btn-large { grid-column: span 4; aspect-ratio: auto; min-height: 64px; flex-direction: row; font-size: 18px; text-transform: uppercase; gap: 12px; }
        .btn-primary { background: var(--primary); }
        .btn-accent { background: var(--accent); }
        .btn-danger { background: #334155; color: var(--danger); }
        .group { margin-bottom: 24px; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>REMOTE CONTROL</h1>
        <input type='text' id='textInput' placeholder='Type here...' autocomplete='off' autofocus>
        
        <div class='group'>
            <div class='section-label'>Basic Controls</div>
            <div class='button-grid'>
                <button onclick='sendKey(""undo"")' title='Undo'>
                    <svg viewBox='0 0 24 24'><path d='M12.5 8c-2.65 0-5.05.99-6.9 2.6L2 7v9h9l-3.62-3.62c1.39-1.16 3.16-1.88 5.12-1.88 3.54 0 6.55 2.31 7.6 5.5l2.37-.78C21.08 11.03 17.15 8 12.5 8z'/></svg>
                    <span>Undo</span>
                </button>
                <button onclick='sendKey(""redo"")' title='Redo'>
                    <svg viewBox='0 0 24 24'><path d='M18.4 10.6C16.55 8.99 14.15 8 11.5 8c-4.65 0-8.58 3.03-9.96 7.22L3.9 16c1.05-3.19 4.05-5.5 7.6-5.5 1.95 0 3.73.72 5.12 1.88L13 16h9V7l-3.6 3.6z'/></svg>
                    <span>Redo</span>
                </button>
                <button onclick='sendKey(""selectall"")' title='Select All'>
                    <svg viewBox='0 0 24 24'><path d='M3 5v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2zm16 14H5V5h14v14z'/></svg>
                    <span>All</span>
                </button>
                <button onclick='sendKey(""backspace"")' class='btn-danger' title='Backspace'>
                    <svg viewBox='0 0 24 24'><path d='M22 3H7c-.69 0-1.23.35-1.59.88L0 12l5.41 8.11c.36.53.9.89 1.59.89h15c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-3 12.59L17.59 17 14 13.41 10.41 17 9 15.59 12.59 12 9 8.41 10.41 7 14 10.59 17.59 7 19 8.41 15.41 12 19 15.59z'/></svg>
                    <span>Del</span>
                </button>
            </div>
        </div>

        <div class='group'>
            <div class='section-label'>Clipboard</div>
            <div class='button-grid'>
                <button onclick='sendKey(""copy"")' title='Copy'>
                    <svg viewBox='0 0 24 24'><path d='M16 1H4v14h2V3h12V1zm3 4H8v14h11V7zm0 16H8V7h11v14z'/></svg>
                    <span>Copy</span>
                </button>
                <button onclick='sendKey(""cut"")' title='Cut'>
                    <svg viewBox='0 0 24 24'><path d='M9.64 7.64c.23-.5.36-1.05.36-1.64 0-2.21-1.79-4-4-4S2 3.79 2 6s1.79 4 4 4c.59 0 1.14-.13 1.64-.36L10 12l-2.36 2.36C7.14 14.13 6.59 14 6 14c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4c0-.59-.13-1.14-.36-1.64L12 14l7 7h3v-1L9.64 7.64z'/></svg>
                    <span>Cut</span>
                </button>
                <button onclick='sendKey(""paste"")' class='btn-accent' style='grid-column: span 2; aspect-ratio: auto;'>
                    <svg viewBox='0 0 24 24'><path d='M19 2h-4.18C14.4.84 13.3 0 12 0c-1.3 0-2.4.84-2.82 2H5c-1.1 0-2 .9-2 2v16h14V4c0-1.1-.9-2-2-2zm-7 0c.55 0 1 .45 1 1s-.45 1-1 1z'/></svg>
                    <span>Paste</span>
                </button>
            </div>
        </div>

        <button onclick='sendKey(""enter"")' class='btn-large btn-primary' title='Enter'>
            <svg viewBox='0 0 24 24'><path d='M19 7v4H5.83l3.58-3.59L8 6l-6 6 6 6 1.41-1.41L5.83 13H21V7z'/></svg>
            <span>Confirm & Enter</span>
        </button>
    </div>

    <script>
        const input = document.getElementById('textInput');
        input.addEventListener('input', (e) => {
            if (e.data) {
                sendAction('type', e.data);
                setTimeout(() => { input.value = ''; }, 10);
            }
        });
        input.addEventListener('keydown', (e) => {
            if (e.key === 'Backspace') { sendKey('backspace'); }
            else if (e.key === 'Enter') { sendKey('enter'); }
        });
        function sendKey(key) { sendAction('key', key); }
        function sendAction(action, value) {
            fetch('/', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: `action=${action}&value=${encodeURIComponent(value)}`
            });
        }
    </script>
</body>
</html>";
        }
    }
}
