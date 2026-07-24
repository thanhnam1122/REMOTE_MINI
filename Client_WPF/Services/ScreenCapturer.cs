#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace RemoteDesktopClient.Services
{
    public class ScreenCapturer : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(
            IntPtr hdc,
            ref BitmapInfo bitmapInfo,
            uint usage,
            out IntPtr bits,
            IntPtr section,
            uint offset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr gdiObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr gdiObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr destinationDc,
            int destinationX,
            int destinationY,
            int width,
            int height,
            IntPtr sourceDc,
            int sourceX,
            int sourceY,
            uint rasterOperation);

        [DllImport("gdi32.dll")]
        private static extern bool StretchBlt(
            IntPtr destinationDc,
            int destinationX,
            int destinationY,
            int destinationWidth,
            int destinationHeight,
            IntPtr sourceDc,
            int sourceX,
            int sourceY,
            int sourceWidth,
            int sourceHeight,
            uint rasterOperation);

        [DllImport("gdi32.dll")]
        private static extern int SetStretchBltMode(IntPtr hdc, int stretchMode);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint timeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint timeEndPeriod(uint milliseconds);

        private const uint SourceCopy = 0x00CC0020;
        private const uint DibRgbColors = 0;
        private const uint BiRgb = 0;
        private const int ColorOnColor = 3;
        private const int TileSize = 256;
        private const int FullFrameThresholdPercent = 20;

        private readonly object _settingsLock = new object();
        private readonly object _captureLock = new object();
        private int _quality;
        private double _scale;
        private int _targetFps;
        private int _captureVersion;
        private int _appliedCaptureVersion = -1;

        private readonly ImageCodecInfo? _jpegCodec;
        private ulong[]? _previousTileHashes;
        private int _previousTileColumns;
        private int _previousTileRows;
        private int _forceKeyframeCounter;

        private IntPtr _screenDc;
        private IntPtr _memoryDc;
        private IntPtr _dibHandle;
        private IntPtr _previousSelectedBitmap;
        private Bitmap? _captureBitmap;
        private int _captureWidth;
        private int _captureHeight;
        private WindowsGraphicsCapturer? _windowsGraphicsCapturer;

        public string BackendName => _windowsGraphicsCapturer != null
            ? "Windows Graphics Capture (GPU)"
            : "GDI Desktop Capture";
        public string? BackendInitializationError { get; private set; }
        public string? LastError { get; private set; }

        public int Quality
        {
            get { lock (_settingsLock) return _quality; }
            set { lock (_settingsLock) _quality = Math.Clamp(value, 10, 100); }
        }

        public double Scale
        {
            get { lock (_settingsLock) return _scale; }
            set
            {
                lock (_settingsLock)
                {
                    double normalizedValue = Math.Clamp(value, 0.1, 1.0);
                    if (Math.Abs(_scale - normalizedValue) > 0.0001)
                    {
                        _scale = normalizedValue;
                        _captureVersion++;
                    }
                }
            }
        }

        public int TargetFps
        {
            get { lock (_settingsLock) return _targetFps; }
            set { lock (_settingsLock) _targetFps = Math.Clamp(value, 5, 120); }
        }

        public ScreenCapturer(int quality = 100, double scale = 1.0, int targetFps = 120)
        {
            _quality = Math.Clamp(quality, 10, 100);
            _scale = Math.Clamp(scale, 0.1, 1.0);
            _targetFps = Math.Clamp(targetFps, 5, 120);
            _jpegCodec = FindJpegCodec();

            timeBeginPeriod(1);

            try
            {
                _windowsGraphicsCapturer = new WindowsGraphicsCapturer();
            }
            catch (Exception ex)
            {
                BackendInitializationError = ex.ToString();
                _windowsGraphicsCapturer = null;
            }
        }

        public void UpdateSettings(int? quality, double? scale, int? targetFps)
        {
            lock (_settingsLock)
            {
                if (quality.HasValue)
                    _quality = Math.Clamp(quality.Value, 10, 100);

                if (scale.HasValue)
                {
                    double normalizedScale = Math.Clamp(scale.Value, 0.1, 1.0);
                    if (Math.Abs(_scale - normalizedScale) > 0.0001)
                    {
                        _scale = normalizedScale;
                        _captureVersion++;
                    }
                }

                if (targetFps.HasValue)
                    _targetFps = Math.Clamp(targetFps.Value, 5, 120);
            }
        }

        public void RequestKeyframe()
        {
            lock (_settingsLock)
            {
                _previousTileHashes = null;
                _appliedCaptureVersion = -1;
                _captureVersion++;
            }
        }

        public void ResetState()
        {
            lock (_settingsLock)
            {
                _previousTileHashes = null;
                _previousTileColumns = 0;
                _previousTileRows = 0;
                _appliedCaptureVersion = -1;
                _forceKeyframeCounter = 0;
                _captureVersion++;
            }
        }

        public (byte[]? PayloadBytes, ushort OrigW, ushort OrigH, ushort TileCount) CaptureDeltaTiles()
        {
            lock (_captureLock)
            {
                try
                {
                    Rectangle screenBounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                        ?? new Rectangle(0, 0, 1920, 1080);

                    int quality;
                    double scale;
                    int captureVersion;
                    lock (_settingsLock)
                    {
                        quality = _quality;
                        scale = _scale;
                        captureVersion = _captureVersion;
                    }

                    int originalWidth = screenBounds.Width;
                    int originalHeight = screenBounds.Height;
                    int targetWidth = Math.Max(1, (int)Math.Round(originalWidth * scale));
                    int targetHeight = Math.Max(1, (int)Math.Round(originalHeight * scale));

                    Bitmap? capturedBitmap = null;
                    bool ownsCapturedBitmap = false;

                    if (_windowsGraphicsCapturer != null)
                    {
                        try
                        {
                            capturedBitmap = _windowsGraphicsCapturer.TryGetFrame(targetWidth, targetHeight);
                            ownsCapturedBitmap = capturedBitmap != null;
                        }
                        catch
                        {
                            _windowsGraphicsCapturer.Dispose();
                            _windowsGraphicsCapturer = null;
                        }
                    }

                    if (capturedBitmap == null)
                    {
                        bool needKeyframe = _previousTileHashes == null || _appliedCaptureVersion != captureVersion;
                        if (needKeyframe)
                        {
                            capturedBitmap = CaptureScreen(screenBounds, targetWidth, targetHeight);
                            ownsCapturedBitmap = false;
                        }
                        else
                        {
                            return (null, (ushort)originalWidth, (ushort)originalHeight, 0);
                        }
                    }

                    try
                    {
                        var result = EncodeChangedTiles(
                            capturedBitmap,
                            originalWidth,
                            originalHeight,
                            quality,
                            captureVersion);

                        LastError = null;
                        return result;
                    }
                    finally
                    {
                        if (ownsCapturedBitmap)
                            capturedBitmap.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    return (null, 0, 0, 0);
                }
            }
        }

        private Bitmap CaptureScreen(Rectangle sourceBounds, int targetWidth, int targetHeight)
        {
            EnsureCaptureResources(targetWidth, targetHeight);
            uint rasterOperation = SourceCopy;

            bool captureSucceeded;
            if (targetWidth == sourceBounds.Width && targetHeight == sourceBounds.Height)
            {
                captureSucceeded = BitBlt(
                    _memoryDc,
                    0,
                    0,
                    targetWidth,
                    targetHeight,
                    _screenDc,
                    sourceBounds.Left,
                    sourceBounds.Top,
                    rasterOperation);
            }
            else
            {
                captureSucceeded = StretchBlt(
                    _memoryDc,
                    0,
                    0,
                    targetWidth,
                    targetHeight,
                    _screenDc,
                    sourceBounds.Left,
                    sourceBounds.Top,
                    sourceBounds.Width,
                    sourceBounds.Height,
                    rasterOperation);
            }

            if (!captureSucceeded)
                throw new InvalidOperationException("GDI không chụp được màn hình hiện tại.");

            return _captureBitmap!;
        }

        private void EnsureCaptureResources(int targetWidth, int targetHeight)
        {
            if (_captureBitmap != null
                && _captureWidth == targetWidth
                && _captureHeight == targetHeight)
            {
                return;
            }

            DisposeCaptureResources();

            _screenDc = GetDC(IntPtr.Zero);
            if (_screenDc == IntPtr.Zero)
                throw new InvalidOperationException("Không lấy được device context của màn hình.");

            _memoryDc = CreateCompatibleDC(_screenDc);
            if (_memoryDc == IntPtr.Zero)
            {
                DisposeCaptureResources();
                throw new InvalidOperationException("Không tạo được device context cho frame.");
            }

            BitmapInfo bitmapInfo = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = targetWidth,
                    Height = -targetHeight,
                    Planes = 1,
                    BitCount = 32,
                    Compression = BiRgb,
                    SizeImage = (uint)(targetWidth * targetHeight * 4)
                }
            };

            _dibHandle = CreateDIBSection(
                _screenDc,
                ref bitmapInfo,
                DibRgbColors,
                out IntPtr pixelBuffer,
                IntPtr.Zero,
                0);

            if (_dibHandle == IntPtr.Zero)
            {
                DisposeCaptureResources();
                throw new InvalidOperationException("Không tạo được bitmap cho frame.");
            }

            _previousSelectedBitmap = SelectObject(_memoryDc, _dibHandle);
            SetStretchBltMode(_memoryDc, ColorOnColor);
            _captureBitmap = new Bitmap(
                targetWidth,
                targetHeight,
                targetWidth * 4,
                PixelFormat.Format32bppArgb,
                pixelBuffer);
            _captureWidth = targetWidth;
            _captureHeight = targetHeight;
        }

        private void DisposeCaptureResources()
        {
            _captureBitmap?.Dispose();
            _captureBitmap = null;

            if (_previousSelectedBitmap != IntPtr.Zero && _memoryDc != IntPtr.Zero)
                SelectObject(_memoryDc, _previousSelectedBitmap);
            _previousSelectedBitmap = IntPtr.Zero;

            if (_dibHandle != IntPtr.Zero)
                DeleteObject(_dibHandle);
            _dibHandle = IntPtr.Zero;

            if (_memoryDc != IntPtr.Zero)
                DeleteDC(_memoryDc);
            _memoryDc = IntPtr.Zero;

            if (_screenDc != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, _screenDc);
            _screenDc = IntPtr.Zero;

            _captureWidth = 0;
            _captureHeight = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPixelsPerMeter;
            public int YPixelsPerMeter;
            public uint ColorsUsed;
            public uint ColorsImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }


        private unsafe (byte[] PayloadBytes, ushort OrigW, ushort OrigH, ushort TileCount) EncodeChangedTiles(
            Bitmap bitmap,
            int originalWidth,
            int originalHeight,
            int quality,
            int captureVersion)
        {
            PixelFormat pixelFormat = bitmap.PixelFormat;
            if (Image.GetPixelFormatSize(pixelFormat) != 32)
                throw new InvalidOperationException($"Định dạng pixel không được hỗ trợ: {pixelFormat}.");

            int columns = (int)Math.Ceiling((double)bitmap.Width / TileSize);
            int rows = (int)Math.Ceiling((double)bitmap.Height / TileSize);
            int totalTiles = columns * rows;

            _forceKeyframeCounter++;
            bool forceKeyframe = _previousTileHashes == null
                || _previousTileColumns != columns
                || _previousTileRows != rows
                || _appliedCaptureVersion != captureVersion
                || _forceKeyframeCounter >= 180;

            if (forceKeyframe)
            {
                _previousTileHashes = new ulong[totalTiles];
                _previousTileColumns = columns;
                _previousTileRows = rows;
                _appliedCaptureVersion = captureVersion;
                _forceKeyframeCounter = 0;
            }

            List<Rectangle> changedRegions = new List<Rectangle>(totalTiles);
            BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                pixelFormat);

            try
            {
                byte* firstPixel = (byte*)bitmapData.Scan0;
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int tileX = column * TileSize;
                        int tileY = row * TileSize;
                        int tileWidth = Math.Min(TileSize, bitmap.Width - tileX);
                        int tileHeight = Math.Min(TileSize, bitmap.Height - tileY);
                        int tileIndex = row * columns + column;

                        ulong hash = ComputeTileHash(
                            firstPixel,
                            bitmapData.Stride,
                            tileX,
                            tileY,
                            tileWidth,
                            tileHeight);

                        if (!forceKeyframe && _previousTileHashes![tileIndex] == hash)
                            continue;

                        _previousTileHashes![tileIndex] = hash;
                        changedRegions.Add(new Rectangle(tileX, tileY, tileWidth, tileHeight));
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            if (changedRegions.Count == 0)
            {
                return (
                    Array.Empty<byte>(),
                    (ushort)originalWidth,
                    (ushort)originalHeight,
                    0);
            }

            bool sendFullFrame = forceKeyframe
                || changedRegions.Count * 100 >= totalTiles * FullFrameThresholdPercent;

            using MemoryStream payload = new MemoryStream();
            using EncoderParameters encoderParameters = CreateEncoderParameters(quality);

            if (sendFullFrame)
            {
                WriteJpegRegion(
                    payload,
                    bitmap,
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    encoderParameters,
                    useSourceBitmap: true);

                return (
                    payload.ToArray(),
                    (ushort)originalWidth,
                    (ushort)originalHeight,
                    1);
            }

            foreach (Rectangle region in changedRegions)
            {
                WriteJpegRegion(
                    payload,
                    bitmap,
                    region,
                    encoderParameters,
                    useSourceBitmap: false);
            }

            return (
                payload.ToArray(),
                (ushort)originalWidth,
                (ushort)originalHeight,
                (ushort)changedRegions.Count);
        }

        private void WriteJpegRegion(
            Stream payload,
            Bitmap sourceBitmap,
            Rectangle region,
            EncoderParameters encoderParameters,
            bool useSourceBitmap)
        {
            using MemoryStream jpegStream = new MemoryStream();

            if (useSourceBitmap)
            {
                if (_jpegCodec != null)
                    sourceBitmap.Save(jpegStream, _jpegCodec, encoderParameters);
                else
                    sourceBitmap.Save(jpegStream, ImageFormat.Jpeg);
            }
            else
            {
                using Bitmap tileBitmap = sourceBitmap.Clone(region, PixelFormat.Format32bppArgb);
                if (_jpegCodec != null)
                    tileBitmap.Save(jpegStream, _jpegCodec, encoderParameters);
                else
                    tileBitmap.Save(jpegStream, ImageFormat.Jpeg);
            }

            byte[] jpegBytes = jpegStream.ToArray();
            WriteUInt16BigEndian(payload, (ushort)region.X);
            WriteUInt16BigEndian(payload, (ushort)region.Y);
            WriteUInt16BigEndian(payload, (ushort)region.Width);
            WriteUInt16BigEndian(payload, (ushort)region.Height);
            WriteUInt32BigEndian(payload, (uint)jpegBytes.Length);
            payload.Write(jpegBytes, 0, jpegBytes.Length);
        }

        private static unsafe ulong ComputeTileHash(
            byte* firstPixel,
            int stride,
            int x,
            int y,
            int width,
            int height)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            int bytesPerRow = width * 4;

            for (int row = 0; row < height; row++)
            {
                byte* rowStart = firstPixel + ((y + row) * stride) + (x * 4);
                int blockCount = bytesPerRow / sizeof(ulong);
                ulong* blocks = (ulong*)rowStart;

                for (int block = 0; block < blockCount; block++)
                {
                    hash ^= blocks[block];
                    hash *= prime;
                }

                for (int index = blockCount * sizeof(ulong); index < bytesPerRow; index++)
                {
                    hash ^= rowStart[index];
                    hash *= prime;
                }
            }

            return hash;
        }

        private static ImageCodecInfo? FindJpegCodec()
        {
            foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
            {
                if (codec.FormatID == ImageFormat.Jpeg.Guid)
                    return codec;
            }

            return null;
        }

        private static EncoderParameters CreateEncoderParameters(int quality)
        {
            EncoderParameters parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(
                System.Drawing.Imaging.Encoder.Quality,
                (long)quality);
            return parameters;
        }

        private static void WriteUInt16BigEndian(Stream stream, ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteUInt32BigEndian(Stream stream, uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            stream.Write(bytes, 0, bytes.Length);
        }

        public void Dispose()
        {
            lock (_captureLock)
            {
                DisposeCaptureResources();
                _windowsGraphicsCapturer?.Dispose();
                _windowsGraphicsCapturer = null;
                timeEndPeriod(1);
            }
        }
    }
}
