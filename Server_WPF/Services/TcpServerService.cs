#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
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
        public event Action<string, string>? OnClientConnected; // clientEndPoint, clientName
        public event Action? OnClientDisconnected;
        public event Action<BitmapSource, ushort, ushort, int>? OnFrameReceived; // image, origW, origH, payloadLen
        public event Action<double, double>? OnStatsUpdated; // fps, kbps

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
                    TcpClient client = await _listener.AcceptTcpClientAsync(token);
                    client.NoDelay = true;
                    client.ReceiveBufferSize = 4 * 1024 * 1024;
                    client.SendBufferSize = 4 * 1024 * 1024;

                    string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                    Log($"[Server] Nhận yêu cầu kết nối từ {clientEndPoint}...");

                    if (_activeClient != null)
                    {
                        Log("[Server] Đang có kết nối khác, ngắt kết nối cũ.");
                        DisconnectCurrentClient();
                    }

                    _activeClient = client;
                    _activeStream = client.GetStream();

                    _ = Task.Run(() => HandleClientStreamAsync(client, _activeStream, clientEndPoint, token), token);
                }
                catch (Exception ex)
                {
                    if (IsRunning && !token.IsCancellationRequested)
                    {
                        Log($"[Server Error] Lỗi khi nhận kết nối: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleClientStreamAsync(TcpClient client, NetworkStream stream, string clientEndPoint, CancellationToken token)
        {
            try
            {
                // Step 1: Read Handshake Packet
                byte[] headerBuffer = new byte[7];
                int readHeader = await ReadExactAsync(stream, headerBuffer, 0, 7, token);
                if (readHeader < 7)
                {
                    Log($"[Server Error] Không nhận đủ Header Handshake từ {clientEndPoint}. Ngắt kết nối.");
                    DisconnectCurrentClient();
                    return;
                }

                if (headerBuffer[0] != PacketProtocol.MAGIC[0] || headerBuffer[1] != PacketProtocol.MAGIC[1] || headerBuffer[2] != PacketProtocol.PKT_TYPE_HANDSHAKE)
                {
                    Log($"[Server Error] Header Handshake không hợp lệ từ {clientEndPoint}.");
                    DisconnectCurrentClient();
                    return;
                }

                byte[] lenBytes = new byte[4];
                Array.Copy(headerBuffer, 3, lenBytes, 0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                uint payloadLen = BitConverter.ToUInt32(lenBytes, 0);

                byte[] payloadBuffer = new byte[payloadLen];
                int readPayload = await ReadExactAsync(stream, payloadBuffer, 0, (int)payloadLen, token);
                if (readPayload < payloadLen)
                {
                    Log("[Server Error] Payload Handshake không đầy đủ.");
                    DisconnectCurrentClient();
                    return;
                }

                string jsonHs = Encoding.UTF8.GetString(payloadBuffer);
                HandshakeInfo info = PacketProtocol.ParseHandshake(jsonHs);

                if (info.Pin != ExpectedPin)
                {
                    Log($"[Server Auth Failure] Sai mã PIN từ {clientEndPoint}! (PIN gửi: '{info.Pin}', PIN yêu cầu: '{ExpectedPin}')");
                    DisconnectCurrentClient();
                    return;
                }

                Log($"[Server Auth Success] Client '{info.ClientName}' ({clientEndPoint}) đã xác thực mã PIN thành công!");
                OnClientConnected?.Invoke(clientEndPoint, info.ClientName);

                // Step 2: Main Stream Loop
                Stopwatch sw = Stopwatch.StartNew();
                long lastStatTime = sw.ElapsedMilliseconds;
                int frameCount = 0;
                long bytesCount = 0;

                byte[] frameHeader = new byte[11];

                while (IsRunning && IsClientConnected && !token.IsCancellationRequested)
                {
                    int headerRead = await ReadExactAsync(stream, frameHeader, 0, 11, token);
                    if (headerRead < 11) break;

                    if (frameHeader[0] != PacketProtocol.MAGIC[0] || frameHeader[1] != PacketProtocol.MAGIC[1])
                    {
                        Log("[Server Protocol Warning] Invalid Frame Header Magic!");
                        continue;
                    }

                    byte pktType = frameHeader[2];
                    if (pktType != PacketProtocol.PKT_TYPE_FRAME)
                    {
                        continue;
                    }

                    byte[] wBytes = new byte[2];
                    byte[] hBytes = new byte[2];
                    byte[] pLenBytes = new byte[4];

                    Array.Copy(frameHeader, 3, wBytes, 0, 2);
                    Array.Copy(frameHeader, 5, hBytes, 0, 2);
                    Array.Copy(frameHeader, 7, pLenBytes, 0, 4);

                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(wBytes);
                        Array.Reverse(hBytes);
                        Array.Reverse(pLenBytes);
                    }

                    ushort origW = BitConverter.ToUInt16(wBytes, 0);
                    ushort origH = BitConverter.ToUInt16(hBytes, 0);
                    uint framePayloadLen = BitConverter.ToUInt32(pLenBytes, 0);

                    byte[] jpegBytes = new byte[framePayloadLen];
                    int payloadRead = await ReadExactAsync(stream, jpegBytes, 0, (int)framePayloadLen, token);
                    if (payloadRead < framePayloadLen) break;

                    BitmapSource? bitmap = DecodeJpegToBitmapSource(jpegBytes);
                    if (bitmap != null)
                    {
                        OnFrameReceived?.Invoke(bitmap, origW, origH, (int)framePayloadLen);
                    }

                    frameCount++;
                    bytesCount += 11 + framePayloadLen;

                    long nowMs = sw.ElapsedMilliseconds;
                    if (nowMs - lastStatTime >= 1000)
                    {
                        double elapsedSec = (nowMs - lastStatTime) / 1000.0;
                        double fps = frameCount / elapsedSec;
                        double kbps = (bytesCount / 1024.0) / elapsedSec;

                        OnStatsUpdated?.Invoke(fps, kbps);

                        frameCount = 0;
                        bytesCount = 0;
                        lastStatTime = nowMs;
                    }
                }
            }
            catch (Exception ex)
            {
                if (IsRunning)
                {
                    Log($"[Server Error] Lỗi luồng dữ liệu Client: {ex.Message}");
                }
            }
            finally
            {
                DisconnectCurrentClient();
            }
        }

        private BitmapSource? DecodeJpegToBitmapSource(byte[] jpegBytes)
        {
            try
            {
                using MemoryStream ms = new MemoryStream(jpegBytes);
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze(); // Make cross-thread accessible
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public void SendMouseCommand(string action, float normX, float normY, string button = "left", int delta = 0)
        {
            byte[] packet = PacketProtocol.CreateMousePacket(action, normX, normY, button, delta);
            SendRawPacket(packet);
        }

        public void SendKeyboardCommand(string action, string key)
        {
            byte[] packet = PacketProtocol.CreateKeyboardPacket(action, key);
            SendRawPacket(packet);
        }

        public void SendConfigCommand(int quality, double scale, int fpsLimit)
        {
            byte[] packet = PacketProtocol.CreateConfigPacket(quality, scale, fpsLimit);
            SendRawPacket(packet);
        }

        private void SendRawPacket(byte[] packet)
        {
            if (!IsClientConnected || _activeStream == null) return;
            try
            {
                _activeStream.Write(packet, 0, packet.Length);
                _activeStream.Flush();
            }
            catch (Exception ex)
            {
                Log($"[Server Send Error] Lỗi gửi packet: {ex.Message}");
            }
        }

        private async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken token)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, token);
                if (read == 0) return totalRead;
                totalRead += read;
            }
            return totalRead;
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }
}
