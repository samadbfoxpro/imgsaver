using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace imgsaver
{
    public class FileShareServer
    {
        private HttpListener _listener;
        private bool _isRunning;
        private const int Port = 9896;
        private static readonly string SharePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "share");

        public event Action<string> StatusChanged;
        public event Action<string> FileReceived;

        public bool IsRunning => _isRunning;

        static FileShareServer()
        {
            if (!Directory.Exists(SharePath))
                Directory.CreateDirectory(SharePath);
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                string ip = GetLocalIPAddress();
                
                _listener.Prefixes.Add($"http://+:{Port}/");
                _listener.Prefixes.Add($"http://*:{Port}/");
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Prefixes.Add($"http://{ip}:{Port}/");
                
                _listener.Start();
                _isRunning = true;
                
                Task.Run(() => Listen());
                StatusChanged?.Invoke($"File Server active at {ip}:{Port}");
            }
            catch (Exception ex)
            {
                _isRunning = false;
                throw new Exception($"Failed to start File Server (Administrator privileges might be required):\n{ex.Message}");
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            StatusChanged?.Invoke("File Server stopped");
        }

        private async Task Listen()
        {
            while (_isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequest(context)); // Handle concurrently
                }
                catch { }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                string path = WebUtility.UrlDecode(request.Url.AbsolutePath).TrimStart('/');
                
                if (request.HttpMethod == "GET")
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        ServeInterface(response);
                    }
                    else if (path == "api/files")
                    {
                        ServeFileList(response);
                    }
                    else if (path.StartsWith("download/"))
                    {
                        string fileName = path.Substring("download/".Length);
                        ServeFile(fileName, response);
                    }
                    else
                    {
                        response.StatusCode = 404;
                    }
                }
                else if (request.HttpMethod == "POST" && path == "api/upload")
                {
                    await HandleUpload(request, response);
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

        private void ServeFileList(HttpListenerResponse response)
        {
            var files = Directory.GetFiles(SharePath)
                        .Select(f => new { 
                            name = Path.GetFileName(f), 
                            size = new FileInfo(f).Length,
                            date = File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm")
                        });

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(files);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private void ServeFile(string fileName, HttpListenerResponse response)
        {
            string fullPath = Path.Combine(SharePath, fileName);
            if (File.Exists(fullPath))
            {
                byte[] buffer = File.ReadAllBytes(fullPath);
                response.ContentType = "application/octet-stream";
                response.AddHeader("Content-Disposition", $"attachment; filename=\"{WebUtility.UrlEncode(fileName)}\"");
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                response.StatusCode = 404;
            }
        }

        private async Task HandleUpload(HttpListenerRequest request, HttpListenerResponse response)
        {
            try 
            {
                string contentType = request.ContentType;
                int boundaryIndex = contentType.IndexOf("boundary=");
                if (boundaryIndex == -1) throw new Exception("Invalid content type");
                
                string boundaryStr = contentType.Substring(boundaryIndex + 9);
                byte[] boundary = Encoding.UTF8.GetBytes("--" + boundaryStr);
                
                // Read entire request body
                using (var ms = new MemoryStream())
                {
                    byte[] buffer = new byte[81920];
                    int bytesRead;
                    
                    while ((bytesRead = await request.InputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await ms.WriteAsync(buffer, 0, bytesRead);
                    }
                    
                    await ms.FlushAsync();
                    byte[] data = ms.ToArray();
                    
                    if (data.Length == 0) throw new Exception("No data received");
                    
                    // Find filename in headers (search as string only in header part)
                    string headerPart = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 1024));
                    int fileNameIndex = headerPart.IndexOf("filename=\"");
                    if (fileNameIndex == -1) throw new Exception("No file found");
                    
                    int endNameIndex = headerPart.IndexOf("\"", fileNameIndex + 10);
                    string fileName = headerPart.Substring(fileNameIndex + 10, endNameIndex - (fileNameIndex + 10));
                    fileName = Path.GetFileName(fileName);
                    
                    // Find header end (CRLF CRLF) in binary
                    byte[] headerEnd = Encoding.UTF8.GetBytes("\r\n\r\n");
                    int headerEndIndex = FindBytes(data, headerEnd, 0);
                    if (headerEndIndex == -1) throw new Exception("Invalid multipart format");
                    
                    headerEndIndex += 4; // Skip past the \r\n\r\n
                    
                    // Find footer boundary in binary
                    byte[] footer = Encoding.UTF8.GetBytes("\r\n--" + boundaryStr);
                    int footerIndex = FindBytesReverse(data, footer, data.Length - 1);
                    
                    if (footerIndex > headerEndIndex)
                    {
                        int fileDataLength = footerIndex - headerEndIndex;
                        byte[] fileData = new byte[fileDataLength];
                        Array.Copy(data, headerEndIndex, fileData, 0, fileDataLength);
                        
                        string fullPath = Path.Combine(SharePath, fileName);
                        
                        // Write file
                        using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            await fileStream.WriteAsync(fileData, 0, fileData.Length);
                            await fileStream.FlushAsync();
                        }
                        
                        // Verify
                        var fileInfo = new FileInfo(fullPath);
                        if (fileInfo.Length != fileData.Length)
                        {
                            File.Delete(fullPath);
                            throw new Exception("File write verification failed");
                        }
                        
                        FileReceived?.Invoke(fileName);
                    }
                    else
                    {
                        throw new Exception("Invalid multipart data");
                    }
                }
                
                response.StatusCode = 200;
                byte[] ok = Encoding.UTF8.GetBytes("{\"status\":\"success\"}");
                response.ContentType = "application/json";
                response.ContentLength64 = ok.Length;
                await response.OutputStream.WriteAsync(ok, 0, ok.Length);
                await response.OutputStream.FlushAsync();
            }
            catch (Exception ex)
            {
                response.StatusCode = 400;
                byte[] err = Encoding.UTF8.GetBytes($"{{\"status\":\"error\", \"message\":\"{ex.Message}\"}}");
                response.ContentType = "application/json";
                response.ContentLength64 = err.Length;
                await response.OutputStream.WriteAsync(err, 0, err.Length);
                await response.OutputStream.FlushAsync();
            }
        }

        private int FindBytes(byte[] source, byte[] pattern, int startIndex)
        {
            for (int i = startIndex; i <= source.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return i;
            }
            return -1;
        }

        private int FindBytesReverse(byte[] source, byte[] pattern, int startIndex)
        {
            for (int i = startIndex - pattern.Length + 1; i >= 0; i--)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return i;
            }
            return -1;
        }

        public string GetLocalIPAddress()
        {
            try
            {
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 && 
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Ethernet) continue;

                    string name = ni.Name.ToLower();
                    if (name.Contains("vpn") || name.Contains("tun")) continue;

                    var props = ni.GetIPProperties();
                    foreach (var ip in props.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string addr = ip.Address.ToString();
                            if (addr.StartsWith("192.168.") || addr.StartsWith("10.") || addr.StartsWith("172."))
                                return addr;
                        }
                    }
                }
            } catch { }
            return "127.0.0.1";
        }

        private string GetHtmlInterface()
        {
            return @"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Cloud Link - File Share</title>
    <style>
        :root {
            --bg: #0f172a;
            --surface: #1e293b;
            --primary: #3b82f6;
            --primary-glow: rgba(59, 130, 246, 0.5);
            --accent: #8b5cf6;
            --text: #f8fafc;
            --text-muted: #94a3b8;
            --success: #10b981;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { 
            font-family: 'Inter', -apple-system, sans-serif; 
            background: var(--bg); color: var(--text); 
            line-height: 1.5; padding: 20px;
        }
        .container { max-width: 800px; margin: 0 auto; }
        header { 
            display: flex; justify-content: space-between; align-items: center; 
            margin-bottom: 30px; padding-bottom: 20px;
            border-bottom: 1px solid rgba(255,255,255,0.1);
        }
        h1 { 
            font-size: 1.5rem; font-weight: 800;
            background: linear-gradient(135deg, #3b82f6, #8b5cf6);
            -webkit-background-clip: text; -webkit-text-fill-color: transparent;
        }
        .upload-card {
            background: var(--surface); border-radius: 20px; padding: 30px;
            border: 2px dashed rgba(255,255,255,0.1);
            text-align: center; margin-bottom: 30px;
            transition: all 0.3s ease;
        }
        .upload-card.drag-over {
            border-color: var(--primary);
            background: rgba(59, 130, 246, 0.05);
            box-shadow: 0 0 20px var(--primary-glow);
        }
        .upload-btn {
            background: var(--primary); color: white; border: none;
            padding: 12px 24px; border-radius: 12px; font-weight: 600;
            cursor: pointer; margin-top: 15px; transition: transform 0.2s;
        }
        .upload-btn:active { transform: scale(0.95); }
        .file-list { display: grid; gap: 12px; }
        .file-item {
            background: var(--surface); padding: 16px; border-radius: 16px;
            display: flex; justify-content: space-between; align-items: center;
            border: 1px solid rgba(255,255,255,0.05);
            animation: fadeIn 0.4s ease-out;
        }
        .file-info { display: flex; align-items: center; gap: 15px; }
        .file-icon { font-size: 1.5rem; }
        .file-name { font-weight: 600; font-size: 0.95rem; }
        .file-meta { font-size: 0.75rem; color: var(--text-muted); }
        .download-btn {
            background: rgba(255,255,255,0.05); color: var(--text); border: none;
            width: 40px; height: 40px; border-radius: 10px; cursor: pointer;
            display: flex; align-items: center; justify-content: center;
            transition: all 0.2s;
        }
        .download-btn:hover { background: var(--primary); }
        #progress-bar {
            width: 100%; height: 4px; background: rgba(255,255,255,0.1);
            border-radius: 2px; margin-top: 15px; display: none; overflow: hidden;
        }
        #progress-inner {
            height: 100%; background: var(--primary); width: 0%;
            transition: width 0.3s;
        }
        @keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } }
        @media (max-width: 600px) {
            body { padding: 15px; }
            h1 { font-size: 1.2rem; }
        }
    </style>
</head>
<body>
    <div class='container'>
        <header>
            <h1>CLOUD LINK</h1>
            <span id='status' style='font-size: 0.8rem; color: var(--success); font-weight: bold;'>● ONLINE</span>
        </header>

        <div class='upload-card' id='drop-zone'>
            <div style='font-size: 3rem; margin-bottom: 10px;'>📁</div>
            <p style='font-weight: 600;'>Drag & Drop files to share</p>
            <p style='font-size: 0.8rem; color: var(--text-muted);'>or click to browse your device</p>
            <input type='file' id='file-input' hidden>
            <button class='upload-btn' onclick='document.getElementById(""file-input"").click()'>Choose File</button>
            <div id='progress-bar'><div id='progress-inner'></div></div>
        </div>

        <div style='margin-bottom: 15px; display: flex; justify-content: space-between; align-items: center;'>
            <h2 style='font-size: 1.1rem;'>Shared Files</h2>
            <button onclick='loadFiles()' style='background: transparent; border: none; color: var(--primary); cursor: pointer; font-size: 0.8rem;'>Refresh</button>
        </div>
        <div class='file-list' id='file-list'>
            <!-- Files populated by JS -->
        </div>
    </div>

    <script>
        const dropZone = document.getElementById('drop-zone');
        const fileInput = document.getElementById('file-input');

        // Drag & Drop handlers
        dropZone.ondragover = (e) => { e.preventDefault(); dropZone.classList.add('drag-over'); };
        dropZone.ondragleave = () => dropZone.classList.remove('drag-over');
        dropZone.ondrop = (e) => {
            e.preventDefault();
            dropZone.classList.remove('drag-over');
            handleFiles(e.dataTransfer.files);
        };

        fileInput.onchange = (e) => handleFiles(e.target.files);

        function handleFiles(files) {
            if (files.length === 0) return;
            const file = files[0];
            const formData = new FormData();
            formData.append('file', file);

            const progressBar = document.getElementById('progress-bar');
            const progressInner = document.getElementById('progress-inner');
            progressBar.style.display = 'block';

            const xhr = new XMLHttpRequest();
            xhr.open('POST', '/api/upload', true);
            
            xhr.upload.onprogress = (e) => {
                if (e.lengthComputable) {
                    const percent = (e.loaded / e.total) * 100;
                    progressInner.style.width = percent + '%';
                }
            };

            xhr.onload = () => {
                progressBar.style.display = 'none';
                progressInner.style.width = '0%';
                if (xhr.status === 200) {
                    loadFiles();
                } else {
                    alert('Upload failed');
                }
            };
            xhr.send(formData);
        }

        async function loadFiles() {
            try {
                const res = await fetch('/api/files');
                const files = await res.json();
                const list = document.getElementById('file-list');
                list.innerHTML = '';
                
                files.forEach(f => {
                    const div = document.createElement('div');
                    div.className = 'file-item';
                    div.innerHTML = `
                        <div class='file-info'>
                            <div class='file-icon'>📄</div>
                            <div>
                                <div class='file-name'>${f.name}</div>
                                <div class='file-meta'>${formatSize(f.size)} • ${f.date}</div>
                            </div>
                        </div>
                        <a href='/download/${encodeURIComponent(f.name)}' class='download-btn' download>
                           <svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'></path><polyline points='7 10 12 15 17 10'></polyline><line x1='12' y1='15' x2='12' y2='3'></line></svg>
                        </a>
                    `;
                    list.appendChild(div);
                });
            } catch (err) { console.error(err); }
        }

        function formatSize(bytes) {
            if (bytes === 0) return '0 B';
            const k = 1024;
            const sizes = ['B', 'KB', 'MB', 'GB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));
            return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
        }

        loadFiles();
    </script>
</body>
</html>";
        }
    }
}
