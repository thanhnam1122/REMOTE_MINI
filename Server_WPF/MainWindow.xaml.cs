#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RemoteDesktopServer.Helpers;
using RemoteDesktopServer.Models;
using RemoteDesktopServer.Services;

using WpfMessageBox = System.Windows.MessageBox;
using MediaBrush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace RemoteDesktopServer
{
    public partial class MainWindow : Window
    {
        private readonly TcpServerService _serverService;
        private ushort _remoteWidth = 1920;
        private ushort _remoteHeight = 1080;
        private DateTime _lastMouseMoveTime = DateTime.MinValue;

        private WriteableBitmap? _screenBitmap;
        private int _isRenderPending = 0;
        private List<DecodedTile>? _latestTiles;
        private ushort _latestOrigW;
        private ushort _latestOrigH;
        private int _latestFrameSize;

        public MainWindow()
        {
            InitializeComponent();

            _serverService = new TcpServerService();
            _serverService.OnLog += ServerService_OnLog;
            _serverService.OnClientConnected += ServerService_OnClientConnected;
            _serverService.OnClientDisconnected += ServerService_OnClientDisconnected;
            _serverService.OnTileFrameReceived += ServerService_OnTileFrameReceived;
            _serverService.OnStatsUpdated += ServerService_OnStatsUpdated;

            // Auto-start listener on launch
            StartServer();
        }

        private void StartServer()
        {
            if (!int.TryParse(txtPort.Text.Trim(), out int port))
            {
                WpfMessageBox.Show("Cổng (Port) không hợp lệ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _serverService.ExpectedPin = txtPin.Text.Trim();
            _serverService.Start(port);

            btnToggleServer.Content = "Dừng máy chủ";
            UpdateStatusUI(false, $"Đang lắng nghe Cổng {port}...");
        }

        private void StopServer()
        {
            _serverService.Stop();
            btnToggleServer.Content = "Khởi động máy chủ";
            UpdateStatusUI(false, "Server đã dừng");
            overlayPlaceholder.Visibility = Visibility.Visible;
            imgViewport.Source = null;
            _screenBitmap = null;
        }

        private void BtnToggleServer_Click(object sender, RoutedEventArgs e)
        {
            if (_serverService.IsRunning)
            {
                StopServer();
            }
            else
            {
                StartServer();
            }
        }

        private void ServerService_OnLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string timeStr = DateTime.Now.ToString("HH:mm:ss");
                txtLogs.AppendText($"[{timeStr}] {message}\n");
                txtLogs.ScrollToEnd();
            });
        }

        private void ServerService_OnClientConnected(string endpoint, string clientName)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusUI(true, $"Đã kết nối: {clientName}");
                overlayPlaceholder.Visibility = Visibility.Collapsed;
                _screenBitmap = null;
            });
        }

        private void ServerService_OnClientDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusUI(false, "Chờ Client kết nối...");
                overlayPlaceholder.Visibility = Visibility.Collapsed;
                imgViewport.Source = null;
                _screenBitmap = null;
                txtFps.Text = "0.0";
                txtBandwidth.Text = "0.0";
                txtResolution.Text = "N/A";
                txtFrameSize.Text = "0 KB";
            });
        }

        private void ServerService_OnTileFrameReceived(List<TileEntry> tiles, ushort origW, ushort origH, int frameSize)
        {
            var decodedTiles = new List<DecodedTile>(tiles.Count);
            foreach (TileEntry tile in tiles)
            {
                BitmapSource? pixels = DecodeTileJpeg(tile.JpegBytes);
                if (pixels == null)
                    continue;

                decodedTiles.Add(new DecodedTile(tile.X, tile.Y, tile.Width, tile.Height, pixels));
            }

            if (decodedTiles.Count == 0)
                return;

            _latestTiles = decodedTiles;
            _latestOrigW = origW;
            _latestOrigH = origH;
            _latestFrameSize = frameSize;

            if (System.Threading.Interlocked.CompareExchange(ref _isRenderPending, 1, 0) == 0)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
                {
                    try
                    {
                        var tilesToRender = _latestTiles;
                        if (tilesToRender != null && tilesToRender.Count > 0)
                        {
                            RenderTileDeltaFrame(tilesToRender, _latestOrigW, _latestOrigH, _latestFrameSize);
                        }
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _isRenderPending, 0);
                    }
                });
            }
        }

        private void RenderTileDeltaFrame(List<DecodedTile> tiles, ushort origW, ushort origH, int frameSize)
        {
            _remoteWidth = origW;
            _remoteHeight = origH;
            overlayPlaceholder.Visibility = Visibility.Collapsed;
            txtResolution.Text = $"{origW}x{origH}";
            txtFrameSize.Text = $"{(frameSize / 1024.0):F1} KB";

            // Determine canvas resolution from first tile or max tile bounds
            int maxCanvasW = 0;
            int maxCanvasH = 0;
            foreach (var t in tiles)
            {
                if (t.X + t.Width > maxCanvasW) maxCanvasW = t.X + t.Width;
                if (t.Y + t.Height > maxCanvasH) maxCanvasH = t.Y + t.Height;
            }

            if (_screenBitmap == null)
            {
                _screenBitmap = new WriteableBitmap(maxCanvasW, maxCanvasH, 96, 96, PixelFormats.Bgra32, null);
                imgViewport.Source = _screenBitmap;
            }
            else if (_screenBitmap.PixelWidth < maxCanvasW || _screenBitmap.PixelHeight < maxCanvasH)
            {
                int canvasWidth = Math.Max(_screenBitmap.PixelWidth, maxCanvasW);
                int canvasHeight = Math.Max(_screenBitmap.PixelHeight, maxCanvasH);
                _screenBitmap = new WriteableBitmap(canvasWidth, canvasHeight, 96, 96, PixelFormats.Bgra32, null);
                imgViewport.Source = _screenBitmap;
            }

            _screenBitmap.Lock();
            try
            {
                foreach (DecodedTile tile in tiles)
                {
                    int destinationStride = _screenBitmap.BackBufferStride;
                    int destinationOffset = (tile.Y * destinationStride) + (tile.X * 4);
                    IntPtr destination = IntPtr.Add(_screenBitmap.BackBuffer, destinationOffset);
                    int destinationBufferSize = ((tile.Height - 1) * destinationStride) + (tile.Width * 4);

                    tile.Pixels.CopyPixels(
                        new Int32Rect(0, 0, tile.Width, tile.Height),
                        destination,
                        destinationBufferSize,
                        destinationStride);

                    _screenBitmap.AddDirtyRect(new Int32Rect(tile.X, tile.Y, tile.Width, tile.Height));
                }
            }
            finally
            {
                _screenBitmap.Unlock();
            }
        }

        private BitmapSource? DecodeTileJpeg(byte[] jpegBytes)
        {
            try
            {
                using MemoryStream ms = new MemoryStream(jpegBytes);
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                if (bitmap.Format == PixelFormats.Bgra32)
                    return bitmap;

                FormatConvertedBitmap converted = new FormatConvertedBitmap(
                    bitmap,
                    PixelFormats.Bgra32,
                    null,
                    0);
                converted.Freeze();
                return converted;
            }
            catch
            {
                return null;
            }
        }

        private sealed class DecodedTile
        {
            public ushort X { get; }
            public ushort Y { get; }
            public ushort Width { get; }
            public ushort Height { get; }
            public BitmapSource Pixels { get; }

            public DecodedTile(ushort x, ushort y, ushort width, ushort height, BitmapSource pixels)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Pixels = pixels;
            }
        }

        private void ServerService_OnStatsUpdated(double fps, double kbps)
        {
            Dispatcher.Invoke(() =>
            {
                txtFps.Text = fps.ToString("F1", CultureInfo.InvariantCulture);
                txtBandwidth.Text = kbps.ToString("F1", CultureInfo.InvariantCulture);
            });
        }

        private void UpdateStatusUI(bool isConnected, string statusText)
        {
            txtStatus.Text = statusText;
            ApplyStatusPalette(isConnected, _serverService.IsRunning);
        }

        private void ApplyStatusPalette(bool isConnected, bool isRunning)
        {
            string state = isConnected ? "Success" : isRunning ? "Warning" : "Danger";
            ellipseStatus.Fill = (MediaBrush)System.Windows.Application.Current.FindResource($"{state}Brush");
            borderStatusPill.Background = (MediaBrush)System.Windows.Application.Current.FindResource($"{state}SoftBrush");
            borderStatusPill.BorderBrush = (MediaBrush)System.Windows.Application.Current.FindResource($"{state}BorderBrush");
        }

        private bool _isLightTheme = false;

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            _isLightTheme = !_isLightTheme;
            string themeUri = _isLightTheme ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";

            var dict = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };
            var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
            if (dictionaries.Count == 0)
                dictionaries.Add(dict);
            else
                dictionaries[0] = dict;

            btnToggleTheme.Content = _isLightTheme ? "Tối" : "Sáng";
            ApplyStatusPalette(_serverService.IsClientConnected, _serverService.IsRunning);
        }

        #region Viewport Mouse & Keyboard Interactivity

        private void ImgViewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_serverService.IsClientConnected) return;

            if ((DateTime.Now - _lastMouseMoveTime).TotalMilliseconds < 20) return;
            _lastMouseMoveTime = DateTime.Now;

            Point pos = e.GetPosition(imgViewport);
            var (isValid, normX, normY) = WpfCoordinateMapper.GetNormalizedCoordinates(
                pos.X, pos.Y,
                imgViewport.ActualWidth, imgViewport.ActualHeight,
                _remoteWidth, _remoteHeight
            );

            if (isValid)
            {
                _serverService.SendMouseCommand("move", normX, normY);
            }
        }

        private void ImgViewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_serverService.IsClientConnected) return;
            viewportBorder.Focus();

            Point pos = e.GetPosition(imgViewport);
            var (isValid, normX, normY) = WpfCoordinateMapper.GetNormalizedCoordinates(
                pos.X, pos.Y,
                imgViewport.ActualWidth, imgViewport.ActualHeight,
                _remoteWidth, _remoteHeight
            );

            if (!isValid) return;

            string btnStr = e.ChangedButton switch
            {
                MouseButton.Right => "right",
                MouseButton.Middle => "middle",
                _ => "left"
            };

            string action = e.ClickCount >= 2 ? "dclick" : "down";
            _serverService.SendMouseCommand(action, normX, normY, btnStr);
        }

        private void ImgViewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_serverService.IsClientConnected) return;

            Point pos = e.GetPosition(imgViewport);
            var (isValid, normX, normY) = WpfCoordinateMapper.GetNormalizedCoordinates(
                pos.X, pos.Y,
                imgViewport.ActualWidth, imgViewport.ActualHeight,
                _remoteWidth, _remoteHeight
            );

            if (!isValid) return;

            string btnStr = e.ChangedButton switch
            {
                MouseButton.Right => "right",
                MouseButton.Middle => "middle",
                _ => "left"
            };

            _serverService.SendMouseCommand("up", normX, normY, btnStr);
        }

        private void ImgViewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_serverService.IsClientConnected) return;

            Point pos = e.GetPosition(imgViewport);
            var (isValid, normX, normY) = WpfCoordinateMapper.GetNormalizedCoordinates(
                pos.X, pos.Y,
                imgViewport.ActualWidth, imgViewport.ActualHeight,
                _remoteWidth, _remoteHeight
            );

            if (!isValid) return;

            _serverService.SendMouseCommand("scroll", normX, normY, "left", e.Delta);
        }

        private void Viewport_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_serverService.IsClientConnected) return;

            string keyStr = MapWpfKeyToString(e.Key == Key.System ? e.SystemKey : e.Key);
            if (!string.IsNullOrEmpty(keyStr))
            {
                _serverService.SendKeyboardCommand("down", keyStr);
                e.Handled = true;
            }
        }

        private void Viewport_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_serverService.IsClientConnected) return;

            string keyStr = MapWpfKeyToString(e.Key == Key.System ? e.SystemKey : e.Key);
            if (!string.IsNullOrEmpty(keyStr))
            {
                _serverService.SendKeyboardCommand("up", keyStr);
                e.Handled = true;
            }
        }

        private string MapWpfKeyToString(Key key)
        {
            return key switch
            {
                Key.Back => "backspace",
                Key.Tab => "tab",
                Key.Return => "enter",
                Key.LeftShift or Key.RightShift => "shift",
                Key.LeftCtrl or Key.RightCtrl => "ctrl",
                Key.LeftAlt or Key.RightAlt => "alt",
                Key.Escape => "esc",
                Key.Space => "space",
                Key.PageUp => "pageup",
                Key.PageDown => "pagedown",
                Key.End => "end",
                Key.Home => "home",
                Key.Left => "left",
                Key.Up => "up",
                Key.Right => "right",
                Key.Down => "down",
                Key.Insert => "insert",
                Key.Delete => "delete",
                Key.LWin or Key.RWin => "win",
                _ => key.ToString().ToLowerInvariant()
            };
        }

        #endregion

        #region Config Tuning Sliders

        private void BtnApplyConfig_Click(object sender, RoutedEventArgs e)
        {
            if (!_serverService.IsClientConnected)
            {
                WpfMessageBox.Show("Chưa có Client nào kết nối!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int quality = (int)sldQuality.Value;
            double scale = sldScale.Value;
            int fpsLimit = (int)sldFps.Value;

            _serverService.SendConfigCommand(quality, scale, fpsLimit);
        }

        private void SldQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtQualityVal != null)
                txtQualityVal.Text = $"{(int)e.NewValue}%";
        }

        private void SldScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtScaleVal != null)
                txtScaleVal.Text = $"{e.NewValue:F2}x";
        }

        private void SldFps_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtFpsVal != null)
                txtFpsVal.Text = $"{(int)e.NewValue} FPS";
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLogs.Clear();
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _serverService.Stop();
            base.OnClosed(e);
        }
    }
}
