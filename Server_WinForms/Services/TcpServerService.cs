#nullable enable
using System;
using System.Drawing;
using System.IO;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RemoteDesktopServer.Models;

namespace RemoteDesktopServer.Services
{
    public class TcpServerService
    {
        private TcpListener? _listener;
        private TcpClient? _activeClient;
        private NetworkStream? _activeStream;
        private CancellationTokenSource? _cts;

        public bool IsRunning { get; private set; }
        public bool IsClientConnected => _activeClient != null && _activeClient.Connected;

        public event Action<string>? OnLog;
        public event Action<string, string>? OnClientConnected;
        public event Action? OnClientDisconnected;
        public event Action<Image, ushort, ushort, int>? OnFrameReceived;
        public event Action<double, double>? OnStatsUpdated;

        public string ExpectedPin { get; set; } = "1234";

        public void Start(int port)
        {
            if (IsRunning) Stop();

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            IsRunning = true;

            Log($"[Server] Đã khởi chạy TCP Listener trên Cổng (Port): {port}");
            Task.Run(() => AcceptClientsAsync(_cts.Token));
        }

        public void Stop()
        {
            IsRunning = false;
            _cts?.Cancel();

            DisconnectCurrentClient();

            try
            {
                _listener?.Stop();
            }
            catch { }

            _listener = null;
            Log("[Server] Đã dừng TCP Listener.");
        }

        private void DisconnectCurrentClient()
        {
            if (_activeClient != null)
            {
                try
                {
                    _activeStream?.Close();
                    _activeClient.Close();
                }
                catch { }
                _activeStream = null;
                _activeClient = null;
                OnClientDisconnected?.Invoke();
                Log("[Server] Đã ngắt kết nối với Client hiện tại.");
            }
        }

        private async Task AcceptClientsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    client.NoDelay = true; // Disable Nagle's algorithm for low latency

                    string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                    Log($"[Server] Nhận yêu cầu kết nối mới từ {clientEndPoint}...");

                    if (_activeClient != null)
                    {
                        Log($"[Server] Đang có kết nối khác, thay thế kết nối cũ.");
                        DisconnectCurrentClient();
                    }

                    _activeClient = client;
                    _activeStream = client.GetStream();

                    _ = Task.Run(() => ProcessClientSessionAsync(_activeClient, _activeStream, token));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (IsRunning)
                        Log($"[Server] Lỗi khi nhận client: {ex.Message}");
                }
            }
        }

        private async Task ProcessClientSessionAsync(TcpClient client, NetworkStream stream, CancellationToken token)
        {
            string ep = client.Client.RemoteEndPoint?.ToString() ?? "Client";
            long bytesReceivedSec = 0;
            int framesSec = 0;
            DateTime lastStatTime = DateTime.Now;

            try
            {
                // Read Handshake Packet (Header 7B: Magic 2B + Type 1B (0x00) + PayloadLen 4B)
                byte[] hsHeader = await ReadExactAsync(stream, 7, token);
                if (hsHeader == null || hsHeader[0] != 'R' || hsHeader[1] != 'D')
                {
                    Log($"[Server] Handshake không hợp lệ từ {ep}. Đóng kết nối.");
                    DisconnectCurrentClient();
                    return;
                }

                uint hsLen = ReadUInt32BigEndian(hsHeader, 3);
                byte[] hsPayload = await ReadExactAsync(stream, (int)hsLen, token);
                if (hsPayload == null) return;
                
                string hsJson = Encoding.UTF8.GetString(hsPayload);
                HandshakeInfo hsObj = PacketProtocol.ParseHandshake(hsJson);

                if (hsObj != null && hsObj.Pin != ExpectedPin)
                {
                    Log($"[Lỗi Auth] Client gửi mã PIN không khớp: '{hsObj.Pin}' (Cần '{ExpectedPin}'). Từ chối kết nối!");
                    DisconnectCurrentClient();
                    return;
                }

                string clientName = string.IsNullOrEmpty(hsObj?.ClientName) ? "PythonClient" : hsObj.ClientName;
                Log($"[Thành công] Handshake hợp lệ! Client: {clientName} ({ep})");
                OnClientConnected?.Invoke(ep, clientName);

                // Continuous Frame Receiver Loop
                // Frame Header: Magic (2B) + Type (1B: 0x01) + OrigW (2B) + OrigH (2B) + PayloadLen (4B) = 11 Bytes
                while (!token.IsCancellationRequested && client.Connected)
                {
                    byte[] header = await ReadExactAsync(stream, 11, token);
                    if (header == null) break;

                    if (header[0] != 'R' || header[1] != 'D')
                    {
                        Log("[Protocol] Lỗi Header magic byte không phải 'RD'!");
                        continue;
                    }

                    byte type = header[2];
                    if (type == PacketProtocol.PKT_TYPE_FRAME)
                    {
                        ushort origW = ReadUInt16BigEndian(header, 3);
                        ushort origH = ReadUInt16BigEndian(header, 5);
                        uint payloadLen = ReadUInt32BigEndian(header, 7);

                        byte[] jpegBytes = await ReadExactAsync(stream, (int)payloadLen, token);
                        if (jpegBytes == null) break;

                        bytesReceivedSec += 11 + payloadLen;
                        framesSec++;

                        // Convert byte array to Image bitmap
                        using (MemoryStream ms = new MemoryStream(jpegBytes))
                        {
                            Image img = Image.FromStream(ms);
                            Bitmap bmpCopy = new Bitmap(img);
                            OnFrameReceived?.Invoke(bmpCopy, origW, origH, jpegBytes.Length);
                        }

                        // Calculate stats every 1 second
                        TimeSpan elapsed = DateTime.Now - lastStatTime;
                        if (elapsed.TotalSeconds >= 1.0)
                        {
                            double fps = framesSec / elapsed.TotalSeconds;
                            double kbps = (bytesReceivedSec / 1024.0) / elapsed.TotalSeconds;
                            OnStatsUpdated?.Invoke(fps, kbps);

                            framesSec = 0;
                            bytesReceivedSec = 0;
                            lastStatTime = DateTime.Now;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[Server] Kết nối với {ep} đã kết thúc: {ex.Message}");
            }
            finally
            {
                DisconnectCurrentClient();
            }
        }

        public async Task SendMouseCommandAsync(string action, float normX, float normY, string button = "left", int delta = 0)
        {
            if (_activeStream == null || !IsClientConnected) return;

            try
            {
                byte[] packet = PacketProtocol.CreateMousePacket(action, normX, normY, button, delta);
                await _activeStream.WriteAsync(packet, 0, packet.Length);
                await _activeStream.FlushAsync();
            }
            catch (Exception ex)
            {
                Log($"[Lỗi gửi chuột] {ex.Message}");
            }
        }

        public async Task SendKeyboardCommandAsync(string action, string key)
        {
            if (_activeStream == null || !IsClientConnected) return;

            try
            {
                byte[] packet = PacketProtocol.CreateKeyboardPacket(action, key);
                await _activeStream.WriteAsync(packet, 0, packet.Length);
                await _activeStream.FlushAsync();
            }
            catch (Exception ex)
            {
                Log($"[Lỗi gửi phím] {ex.Message}");
            }
        }

        public async Task SendConfigUpdateAsync(int quality, double scale, int fpsLimit)
        {
            if (_activeStream == null || !IsClientConnected) return;

            try
            {
                byte[] packet = PacketProtocol.CreateConfigPacket(quality, scale, fpsLimit);
                await _activeStream.WriteAsync(packet, 0, packet.Length);
                await _activeStream.FlushAsync();
                Log($"[Config] Đã gửi cấu hình mới tới Client (Quality={quality}%, Scale={scale}, FPS={fpsLimit})");
            }
            catch (Exception ex)
            {
                Log($"[Lỗi gửi Config] {ex.Message}");
            }
        }

        private async Task<byte[]?> ReadExactAsync(NetworkStream stream, int count, CancellationToken token)
        {
            byte[] buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int r = await stream.ReadAsync(buffer, read, count - read, token);
                if (r == 0) return null; // Connection closed
                read += r;
            }
            return buffer;
        }

        private ushort ReadUInt16BigEndian(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | (uint)data[offset + 3];
        }

        private void Log(string msg)
        {
            OnLog?.Invoke(msg);
        }
    }
}
