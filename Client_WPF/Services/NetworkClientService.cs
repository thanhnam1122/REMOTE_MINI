#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteDesktopClient.Services
{
    public class NetworkClientService
    {
        private static readonly byte[] MAGIC_HEADER = new byte[] { (byte)'R', (byte)'D' };
        private const byte PKT_TYPE_HANDSHAKE = 0x00;
        private const byte PKT_TYPE_FRAME = 0x01;
        private const byte PKT_TYPE_CONTROL = 0x02;
        private const byte PKT_TYPE_CONFIG = 0x03;
        private const byte PKT_TYPE_TILE_FRAME = 0x04;

        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;

        public bool IsRunning { get; private set; }
        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;

        public ScreenCapturer Capturer { get; }
        public RemoteExecutor Executor { get; }

        public event Action<string>? OnLog;
        public event Action<double, double, int, int, long>? OnStatsUpdated; // fps, kbps, width, height, totalBytes
        public event Action<bool, string>? OnStatusChanged; // isConnected, statusMessage

        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8888;
        public string Pin { get; set; } = "1234";

        private long _totalBytesSent;

        public NetworkClientService()
        {
            Capturer = new ScreenCapturer(quality: 100, scale: 1.0, targetFps: 120);
            Executor = new RemoteExecutor();
        }

        public void Start(string host, int port, string pin)
        {
            if (IsRunning) Stop();

            Host = host;
            Port = port;
            Pin = pin;
            IsRunning = true;
            _cts = new CancellationTokenSource();

            Log($"[Network] Đang bắt đầu kết nối tới {Host}:{Port}...");
            Task.Run(() => ConnectionLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            IsRunning = false;
            _cts?.Cancel();

            DisconnectSocket();
            OnStatusChanged?.Invoke(false, "Đã ngắt kết nối.");
            Log("[Network] Đã dừng Client TCP.");
        }

        private void DisconnectSocket()
        {
            try
            {
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch { }

            _stream = null;
            _tcpClient = null;
        }

        private async Task ConnectionLoopAsync(CancellationToken token)
        {
            while (IsRunning && !token.IsCancellationRequested)
            {
                try
                {
                    OnStatusChanged?.Invoke(false, "Đang kết nối...");
                    Log($"[Network] Kết nối TCP tới {Host}:{Port}...");

                    _tcpClient = new TcpClient();
                    _tcpClient.NoDelay = true;
                    _tcpClient.SendBufferSize = 4 * 1024 * 1024;
                    _tcpClient.ReceiveBufferSize = 4 * 1024 * 1024;

                    await _tcpClient.ConnectAsync(Host, Port, token);
                    _stream = _tcpClient.GetStream();

                    Log(">> [Network] Kết nối TCP thành công! Đang gửi Handshake...");
                    await SendHandshakeAsync(token);

                    OnStatusChanged?.Invoke(true, "Đã kết nối!");

                    // Start receiver task for input control & config packets
                    Task receiverTask = Task.Run(() => ReceiverLoopAsync(token), token);

                    // Start frame streaming loop
                    await StreamFramesAsync(token);

                    await receiverTask;
                }
                catch (Exception ex)
                {
                    if (IsRunning)
                    {
                        Log($"[Network Error] Lỗi kết nối: {ex.Message}");
                        OnStatusChanged?.Invoke(false, "Lỗi kết nối. Thử lại sau 3s...");
                    }
                }
                finally
                {
                    DisconnectSocket();
                }

                if (IsRunning && !token.IsCancellationRequested)
                {
                    await Task.Delay(3000, token).ContinueWith(_ => { });
                }
            }

            OnStatusChanged?.Invoke(false, "Đã dừng.");
        }

        private async Task SendHandshakeAsync(CancellationToken token)
        {
            if (_stream == null) return;

            string json = $"{{\"pin\":\"{Pin}\",\"client_name\":\"WPFClient\"}}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(json);

            // Header: Magic (2B) + Type (1B) + PayloadLen (4B Big-Endian) = 7 Bytes
            byte[] header = new byte[7];
            header[0] = MAGIC_HEADER[0];
            header[1] = MAGIC_HEADER[1];
            header[2] = PKT_TYPE_HANDSHAKE;

            byte[] lenBytes = BitConverter.GetBytes((uint)payloadBytes.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
            Array.Copy(lenBytes, 0, header, 3, 4);

            await _stream.WriteAsync(header, 0, header.Length, token);
            await _stream.WriteAsync(payloadBytes, 0, payloadBytes.Length, token);
            await _stream.FlushAsync(token);
        }

        private async Task StreamFramesAsync(CancellationToken token)
        {
            Log($"[Capture] Đang dùng {Capturer.BackendName}.");
            Capturer.RequestKeyframe();
            Stopwatch sw = Stopwatch.StartNew();
            long lastStatTime = sw.ElapsedMilliseconds;
            int frameCount = 0;
            long bytesCount = 0;
            string? lastCaptureError = null;

            // Packet Header: Magic (2B) + Type (1B: 0x04) + OrigW (2B) + OrigH (2B) + TileCount (2B) + PayloadLen (4B) = 13 Bytes
            byte[] header = new byte[13];
            header[0] = MAGIC_HEADER[0];
            header[1] = MAGIC_HEADER[1];
            header[2] = PKT_TYPE_TILE_FRAME;

            while (IsRunning && IsConnected && _stream != null && !token.IsCancellationRequested)
            {
                long frameStartMs = sw.ElapsedMilliseconds;

                var (payloadBytes, origW, origH, tileCount) = Capturer.CaptureDeltaTiles();

                if (payloadBytes == null || tileCount == 0)
                {
                    if (!string.IsNullOrWhiteSpace(Capturer.LastError)
                        && !string.Equals(lastCaptureError, Capturer.LastError, StringComparison.Ordinal))
                    {
                        lastCaptureError = Capturer.LastError;
                        Log($"[Capture Error] {lastCaptureError}");
                    }

                    // No screen changes: report stats & brief sleep
                    long idleNowMs = sw.ElapsedMilliseconds;
                    if (idleNowMs - lastStatTime >= 1000)
                    {
                        double elapsedSec = (idleNowMs - lastStatTime) / 1000.0;
                        double currentFps = frameCount / elapsedSec;
                        double kbps = (bytesCount / 1024.0) / elapsedSec;

                        OnStatsUpdated?.Invoke(currentFps, kbps, origW, origH, Interlocked.Read(ref _totalBytesSent));

                        frameCount = 0;
                        bytesCount = 0;
                        lastStatTime = idleNowMs;
                    }

                    int fpsLimitIdle = Capturer.TargetFps;
                    int targetIntervalMsIdle = 1000 / Math.Max(5, fpsLimitIdle);
                    await Task.Delay(targetIntervalMsIdle, token);
                    continue;
                }

                int payloadLen = payloadBytes.Length;

                byte[] wBytes = BitConverter.GetBytes(origW);
                byte[] hBytes = BitConverter.GetBytes(origH);
                byte[] countBytes = BitConverter.GetBytes(tileCount);
                byte[] lenBytes = BitConverter.GetBytes((uint)payloadLen);

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(wBytes);
                    Array.Reverse(hBytes);
                    Array.Reverse(countBytes);
                    Array.Reverse(lenBytes);
                }

                Array.Copy(wBytes, 0, header, 3, 2);
                Array.Copy(hBytes, 0, header, 5, 2);
                Array.Copy(countBytes, 0, header, 7, 2);
                Array.Copy(lenBytes, 0, header, 9, 4);

                try
                {
                    await _stream.WriteAsync(header, 0, header.Length, token);
                    await _stream.WriteAsync(payloadBytes, 0, payloadBytes.Length, token);
                    await _stream.FlushAsync(token);

                    frameCount++;
                    int totalPacketSize = header.Length + payloadLen;
                    bytesCount += totalPacketSize;
                    Interlocked.Add(ref _totalBytesSent, totalPacketSize);
                }
                catch (Exception ex)
                {
                    Log($"[Network Error] Lỗi gửi delta frame: {ex.Message}");
                    break;
                }

                // Stats reporting every 1 second
                long nowMs = sw.ElapsedMilliseconds;
                if (nowMs - lastStatTime >= 1000)
                {
                    double elapsedSec = (nowMs - lastStatTime) / 1000.0;
                    double currentFps = frameCount / elapsedSec;
                    double kbps = (bytesCount / 1024.0) / elapsedSec;

                    OnStatsUpdated?.Invoke(currentFps, kbps, origW, origH, Interlocked.Read(ref _totalBytesSent));

                    frameCount = 0;
                    bytesCount = 0;
                    lastStatTime = nowMs;
                }

                // Precise frame pacing delay calculation
                long frameElapsedMs = sw.ElapsedMilliseconds - frameStartMs;
                int fpsLimit = Capturer.TargetFps;
                int targetIntervalMs = 1000 / Math.Max(5, fpsLimit);
                int remainingDelayMs = targetIntervalMs - (int)frameElapsedMs;

                if (remainingDelayMs > 0)
                {
                    await Task.Delay(remainingDelayMs, token);
                }
                else
                {
                    await Task.Yield();
                }
            }
        }

        private async Task ReceiverLoopAsync(CancellationToken token)
        {
            byte[] headerBuffer = new byte[7];

            while (IsRunning && IsConnected && _stream != null && !token.IsCancellationRequested)
            {
                try
                {
                    int readHeader = await ReadExactAsync(_stream, headerBuffer, 0, 7, token);
                    if (readHeader < 7) break;

                    if (headerBuffer[0] != MAGIC_HEADER[0] || headerBuffer[1] != MAGIC_HEADER[1])
                    {
                        Log("[Protocol Warning] Invalid Header Magic!");
                        continue;
                    }

                    byte pktType = headerBuffer[2];
                    byte[] lenBytes = new byte[4];
                    Array.Copy(headerBuffer, 3, lenBytes, 0, 4);
                    if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                    uint payloadLen = BitConverter.ToUInt32(lenBytes, 0);

                    byte[] payloadBuffer = new byte[payloadLen];
                    int readPayload = await ReadExactAsync(_stream, payloadBuffer, 0, (int)payloadLen, token);
                    if (readPayload < payloadLen) break;

                    string jsonStr = Encoding.UTF8.GetString(payloadBuffer);

                    if (pktType == PKT_TYPE_CONTROL)
                    {
                        Executor.ExecuteCommand(jsonStr);
                    }
                    else if (pktType == PKT_TYPE_CONFIG)
                    {
                        Log($"[Config] Nhận cấu hình từ Server: {jsonStr}");
                        ParseAndApplyConfig(jsonStr);
                    }
                }
                catch (Exception ex)
                {
                    if (IsRunning && IsConnected)
                    {
                        Log($"[Network Error] Lỗi luồng nhận: {ex.Message}");
                    }
                    break;
                }
            }
        }

        private void ParseAndApplyConfig(string jsonStr)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonStr);
                JsonElement root = doc.RootElement;

                int? quality = root.TryGetProperty("quality", out var qElem) ? qElem.GetInt32() : null;
                double? scale = root.TryGetProperty("scale", out var sElem) ? sElem.GetDouble() : null;
                int? fpsLimit = root.TryGetProperty("fps_limit", out var fElem) ? fElem.GetInt32() : null;

                Capturer.UpdateSettings(quality, scale, fpsLimit);
            }
            catch (Exception ex)
            {
                Log($"[Config Error] Lỗi parse cấu hình: {ex.Message}");
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
