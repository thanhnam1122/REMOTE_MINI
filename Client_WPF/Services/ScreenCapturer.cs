#nullable enable
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace RemoteDesktopClient.Services
{
    public class ScreenCapturer : IDisposable
    {
        #region Win32 API Imports

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool StretchBlt(IntPtr hdcDest, int nXOriginDest, int nYOriginDest, int nWidthDest, int nHeightDest,
                                               IntPtr hdcSrc, int nXOriginSrc, int nYOriginSrc, int nWidthSrc, int nHeightSrc,
                                               uint dwRop);

        [DllImport("gdi32.dll")]
        private static extern int SetStretchBltMode(IntPtr hdc, int nStretchMode);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint timeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint timeEndPeriod(uint uMilliseconds);

        private const uint SRCCOPY = 0x00CC0020;
        private const int COLORONCOLOR = 3;

        #endregion

        private readonly object _lock = new object();
        private int _quality = 55;
        private double _scale = 0.5;
        private int _targetFps = 60;

        private ImageCodecInfo? _jpegCodec;
        private EncoderParameters? _encoderParams;
        private EncoderParameter? _qualityParam;
        private MemoryStream _reusableMs = new MemoryStream(1024 * 512);

        public int Quality
        {
            get { lock (_lock) return _quality; }
            set { lock (_lock) { _quality = Math.Clamp(value, 10, 100); UpdateEncoderParams(); } }
        }

        public double Scale
        {
            get { lock (_lock) return _scale; }
            set { lock (_lock) { _scale = Math.Clamp(value, 0.1, 1.0); } }
        }

        public int TargetFps
        {
            get { lock (_lock) return _targetFps; }
            set { lock (_lock) { _targetFps = Math.Clamp(value, 5, 120); } }
        }

        public ScreenCapturer(int quality = 55, double scale = 0.5, int targetFps = 60)
        {
            _quality = quality;
            _scale = scale;
            _targetFps = targetFps;

            // Set 1ms timer resolution on Windows for 60+ FPS
            timeBeginPeriod(1);
            InitJpegCodec();
        }

        private void InitJpegCodec()
        {
            foreach (var codec in ImageCodecInfo.GetImageEncoders())
            {
                if (codec.FormatID == ImageFormat.Jpeg.Guid)
                {
                    _jpegCodec = codec;
                    break;
                }
            }
            UpdateEncoderParams();
        }

        private void UpdateEncoderParams()
        {
            _encoderParams?.Dispose();
            _encoderParams = new EncoderParameters(1);
            _qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)_quality);
            _encoderParams.Param[0] = _qualityParam;
        }

        public void UpdateSettings(int? quality, double? scale, int? targetFps)
        {
            lock (_lock)
            {
                if (quality.HasValue) _quality = Math.Clamp(quality.Value, 10, 100);
                if (scale.HasValue) _scale = Math.Clamp(scale.Value, 0.1, 1.0);
                if (targetFps.HasValue) _targetFps = Math.Clamp(targetFps.Value, 5, 120);
                UpdateEncoderParams();
            }
        }

        public (byte[]? JpegBytes, ushort OrigW, ushort OrigH) CaptureFrame()
        {
            try
            {
                int origW = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 1920;
                int origH = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height ?? 1080;

                int targetW, targetH;
                double scale;

                lock (_lock)
                {
                    scale = _scale;
                }

                targetW = (int)Math.Max(1, Math.Round(origW * scale));
                targetH = (int)Math.Max(1, Math.Round(origH * scale));

                IntPtr hScreenDC = GetDC(IntPtr.Zero);
                IntPtr hMemoryDC = CreateCompatibleDC(hScreenDC);
                IntPtr hBitmap = CreateCompatibleBitmap(hScreenDC, targetW, targetH);
                IntPtr hOldBitmap = SelectObject(hMemoryDC, hBitmap);

                try
                {
                    if (targetW == origW && targetH == origH)
                    {
                        BitBlt(hMemoryDC, 0, 0, targetW, targetH, hScreenDC, 0, 0, SRCCOPY);
                    }
                    else
                    {
                        SetStretchBltMode(hMemoryDC, COLORONCOLOR);
                        StretchBlt(hMemoryDC, 0, 0, targetW, targetH, hScreenDC, 0, 0, origW, origH, SRCCOPY);
                    }

                    using (Bitmap bmp = Image.FromHbitmap(hBitmap))
                    {
                        lock (_reusableMs)
                        {
                            _reusableMs.SetLength(0);
                            _reusableMs.Position = 0;

                            if (_jpegCodec != null && _encoderParams != null)
                            {
                                bmp.Save(_reusableMs, _jpegCodec, _encoderParams);
                            }
                            else
                            {
                                bmp.Save(_reusableMs, ImageFormat.Jpeg);
                            }

                            return (_reusableMs.ToArray(), (ushort)origW, (ushort)origH);
                        }
                    }
                }
                finally
                {
                    SelectObject(hMemoryDC, hOldBitmap);
                    DeleteObject(hBitmap);
                    DeleteDC(hMemoryDC);
                    ReleaseDC(IntPtr.Zero, hScreenDC);
                }
            }
            catch
            {
                return (null, 0, 0);
            }
        }

        public void Dispose()
        {
            timeEndPeriod(1);
            _encoderParams?.Dispose();
            _qualityParam?.Dispose();
            lock (_reusableMs)
            {
                _reusableMs.Dispose();
            }
        }
    }
}
