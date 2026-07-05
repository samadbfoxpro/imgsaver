using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace imgsaver
{
    public class LocalProxyBridge
    {
        private TcpListener? _listener;
        private bool _isRunning;
        public int Port { get; private set; }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            Task.Run(ListenLoop);
        }

        public void Stop()
        {
            _isRunning = false;
            try { _listener?.Stop(); } catch { }
        }

        private async Task ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var client = await _listener!.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch
                {
                    break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var clientStream = client.GetStream();
                    
                    byte[] buffer = new byte[16384];
                    int bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead <= 0) return;

                    string requestString = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    string[] lines = requestString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length == 0) return;

                    string firstLine = lines[0];
                    string[] parts = firstLine.Split(' ');
                    if (parts.Length < 3) return;

                    string method = parts[0];
                    string target = parts[1];

                    string host = "";
                    int port = 80;

                    if (method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
                    {
                        var hostParts = target.Split(':');
                        host = hostParts[0];
                        port = hostParts.Length > 1 ? int.Parse(hostParts[1]) : 443;
                    }
                    else
                    {
                        try
                        {
                            var uri = new Uri(target);
                            host = uri.Host;
                            port = uri.Port;
                        }
                        catch
                        {
                            // Fallback parsing for partial URIs
                            string temp = target;
                            if (temp.Contains("://")) temp = temp.Substring(temp.IndexOf("://") + 3);
                            if (temp.Contains("/")) temp = temp.Substring(0, temp.IndexOf("/"));
                            var hostParts = temp.Split(':');
                            host = hostParts[0];
                            port = hostParts.Length > 1 ? int.Parse(hostParts[1]) : 80;
                        }
                    }

                    var settings = BrowserSettings.Load();
                    TcpClient targetClient = new TcpClient();

                    bool useUpstream = false;
                    string upstreamHost = "";
                    int upstreamPort = 8080;
                    string upstreamType = "http";

                    if (settings.ProxyMode == "custom" && !string.IsNullOrWhiteSpace(settings.ProxyAddress))
                    {
                        useUpstream = true;
                        upstreamHost = settings.ProxyAddress;
                        upstreamPort = int.TryParse(settings.ProxyPort, out int p) ? p : 8080;
                        upstreamType = settings.ProxyType?.ToLower() ?? "http";
                    }
                    else if (settings.ProxyMode == "system")
                    {
                        try
                        {
                            Uri targetUri = new Uri(method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase) ? $"https://{host}:{port}" : target);
                            IWebProxy systemProxy = System.Net.Http.HttpClient.DefaultProxy;
                            Uri proxyUri = systemProxy.GetProxy(targetUri);
                            if (proxyUri != null && proxyUri != targetUri)
                            {
                                useUpstream = true;
                                upstreamHost = proxyUri.Host;
                                upstreamPort = proxyUri.Port;
                                upstreamType = "http";
                            }
                        }
                        catch { }
                    }

                    if (useUpstream)
                    {
                        await targetClient.ConnectAsync(upstreamHost, upstreamPort);
                        var targetStream = targetClient.GetStream();

                        if (upstreamType == "socks5")
                        {
                            // SOCKS5 Handshake
                            byte[] greeting = new byte[] { 0x05, 0x01, 0x00 };
                            await targetStream.WriteAsync(greeting, 0, greeting.Length);
                            
                            byte[] greetingResp = new byte[2];
                            int read = await targetStream.ReadAsync(greetingResp, 0, 2);
                            if (read < 2 || greetingResp[0] != 0x05 || greetingResp[1] != 0x00) return;

                            byte[] hostBytes = Encoding.ASCII.GetBytes(host);
                            byte[] socksReq = new byte[7 + hostBytes.Length];
                            socksReq[0] = 0x05;
                            socksReq[1] = 0x01; // Connect
                            socksReq[2] = 0x00;
                            socksReq[3] = 0x03; // Domain name
                            socksReq[4] = (byte)hostBytes.Length;
                            Array.Copy(hostBytes, 0, socksReq, 5, hostBytes.Length);
                            socksReq[socksReq.Length - 2] = (byte)(port >> 8);
                            socksReq[socksReq.Length - 1] = (byte)(port & 0xFF);

                            await targetStream.WriteAsync(socksReq, 0, socksReq.Length);

                            byte[] connResp = new byte[4];
                            int readResp = await targetStream.ReadAsync(connResp, 0, 4);
                            if (readResp < 4 || connResp[1] != 0x00) return;

                            int addressType = connResp[3];
                            if (addressType == 0x01) {
                                await targetStream.ReadAsync(new byte[6], 0, 6);
                            } else if (addressType == 0x03) {
                                byte len = (byte)targetStream.ReadByte();
                                await targetStream.ReadAsync(new byte[len + 2], 0, len + 2);
                            } else if (addressType == 0x04) {
                                await targetStream.ReadAsync(new byte[18], 0, 18);
                            }

                            if (method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
                            {
                                byte[] okResp = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                                await clientStream.WriteAsync(okResp, 0, okResp.Length);
                                await clientStream.FlushAsync();
                            }
                            else
                            {
                                await targetStream.WriteAsync(buffer, 0, bytesRead);
                                await targetStream.FlushAsync();
                            }
                        }
                        else
                        {
                            // Upstream HTTP Proxy
                            await targetStream.WriteAsync(buffer, 0, bytesRead);
                            await targetStream.FlushAsync();
                        }

                        await TunnelAsync(clientStream, targetStream);
                    }
                    else
                    {
                        // Direct connection
                        await targetClient.ConnectAsync(host, port);
                        var targetStream = targetClient.GetStream();

                        if (method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
                        {
                            byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                            await clientStream.WriteAsync(response, 0, response.Length);
                            await clientStream.FlushAsync();
                        }
                        else
                        {
                            await targetStream.WriteAsync(buffer, 0, bytesRead);
                            await targetStream.FlushAsync();
                        }

                        await TunnelAsync(clientStream, targetStream);
                    }
                }
                catch
                {
                    // Ignore network disconnects
                }
            }
        }

        private async Task TunnelAsync(NetworkStream clientStream, NetworkStream targetStream)
        {
            var task1 = CopyStreamAsync(clientStream, targetStream);
            var task2 = CopyStreamAsync(targetStream, clientStream);
            await Task.WhenAny(task1, task2);
        }

        private async Task CopyStreamAsync(NetworkStream input, NetworkStream output)
        {
            byte[] buffer = new byte[16384];
            int read;
            try
            {
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await output.WriteAsync(buffer, 0, read);
                    await output.FlushAsync();
                }
            }
            catch
            {
                // Disconnected
            }
        }
    }
}
