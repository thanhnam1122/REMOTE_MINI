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

        private UserSettings _userSettings;

        public MainWindow()
        {
            FontInstallerService.InstallFontFiles();

            InitializeComponent();

            _userSettings = ConfigService.Load();

            txtPort.Text = _userSettings.ServerPort.ToString();
            txtPin.Text = _userSettings.Pin;

            _isLightTheme = _userSettings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
            ApplyTheme(_isLightTheme);

            ConfigService.OnSettingsChanged += (newSettings) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _userSettings = newSettings;
                    bool isLight = _userSettings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
                    if (_isLightTheme != isLight)
                    {
                        ApplyTheme(isLight);
                    }
                    if (txtPort != null && !txtPort.IsFocused && _userSettings.ServerPort > 0)
                        txtPort.Text = _userSettings.ServerPort.ToString();
                    if (txtPin != null && !txtPin.IsFocused && _userSettings.Pin != null)
                        txtPin.Text = _userSettings.Pin;
                    if (sldQuality != null) sldQuality.Value = _userSettings.Quality;
                    if (sldScale != null) sldScale.Value = _userSettings.Scale;
                    if (sldFps != null) sldFps.Value = _userSettings.Fps;
                });
            };

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

                if (sldQuality != null && sldScale != null && sldFps != null)
                {
                    int quality = (int)sldQuality.Value;
                    double scale = sldScale.Value;
                    int fpsLimit = (int)sldFps.Value;
                    _serverService.SendConfigCommand(quality, scale, fpsLimit);
                }
            });
        }

        private void ServerService_OnClientDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusUI(false, "Chờ Client kết nối...");
                overlayPlaceholder.Visibility = Visibility.Visible;
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

        private bool _isLightTheme = true;

        private void ApplyTheme(bool isLight)
        {
            _isLightTheme = isLight;
            string themeUri = _isLightTheme ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";

            var dict = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };
            var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
            if (dictionaries.Count == 0)
                dictionaries.Add(dict);
            else
                dictionaries[0] = dict;

            btnToggleTheme.Content = _isLightTheme ? "Tối" : "Sáng";
            if (_serverService != null)
            {
                ApplyStatusPalette(_serverService.IsClientConnected, _serverService.IsRunning);
            }
        }

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!_isLightTheme);
            SaveCurrentSettings();
        }

        private void SaveCurrentSettings()
        {
            if (_userSettings == null) return;

            _userSettings.Theme = _isLightTheme ? "Light" : "Dark";
            _userSettings.FontFamily = "SF Pro Display";
            if (int.TryParse(txtPort.Text.Trim(), out int port)) _userSettings.ServerPort = port;
            _userSettings.Pin = txtPin.Text.Trim();

            ConfigService.Save(_userSettings);
        }

        #region Fullscreen Toggle

        private bool _isFullscreen = false;
        private WindowStyle _prevWindowStyle = WindowStyle.SingleBorderWindow;
        private WindowState _prevWindowState = WindowState.Normal;
        private ResizeMode _prevResizeMode = ResizeMode.CanResize;

        private MediaBrush? _origWindowBg;
        private MediaBrush? _origRootGridBg;
        private MediaBrush? _origCardRemoteBg;
        private MediaBrush? _origViewportBorderBg;
        private MediaBrush? _origViewportGridBg;

        private void ToggleFullscreen()
        {
            if (!_isFullscreen)
            {
                _prevWindowStyle = this.WindowStyle;
                _prevWindowState = this.WindowState;
                _prevResizeMode = this.ResizeMode;

                _origWindowBg = this.Background;
                _origRootGridBg = rootGrid?.Background;
                _origCardRemoteBg = cardRemoteScreen?.Background;
                _origViewportBorderBg = viewportBorder?.Background;
                _origViewportGridBg = viewportGrid?.Background;

                this.Background = System.Windows.Media.Brushes.Black;
                if (rootGrid != null) rootGrid.Background = System.Windows.Media.Brushes.Black;
                if (cardRemoteScreen != null) cardRemoteScreen.Background = System.Windows.Media.Brushes.Black;
                if (viewportBorder != null) viewportBorder.Background = System.Windows.Media.Brushes.Black;
                if (viewportGrid != null) viewportGrid.Background = System.Windows.Media.Brushes.Black;

                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Normal;
                this.WindowState = WindowState.Maximized;
                this.ResizeMode = ResizeMode.NoResize;

                if (borderProductBar != null) borderProductBar.Visibility = Visibility.Collapsed;
                if (gridWorkspace != null) gridWorkspace.Margin = new Thickness(0);
                if (colRightSidebar != null) colRightSidebar.Width = new GridLength(0);
                if (colRightSidebarGap != null) colRightSidebarGap.Width = new GridLength(0);
                if (rowStreamSettings != null) rowStreamSettings.Height = new GridLength(0);
                if (rowStreamSettingsGap != null) rowStreamSettingsGap.Height = new GridLength(0);
                if (rowHeaderRemoteScreen != null) rowHeaderRemoteScreen.Height = new GridLength(0);
                if (headerRemoteScreen != null) headerRemoteScreen.Visibility = Visibility.Collapsed;
                if (cardRemoteScreen != null)
                {
                    cardRemoteScreen.Padding = new Thickness(0);
                    cardRemoteScreen.CornerRadius = new CornerRadius(0);
                    cardRemoteScreen.BorderThickness = new Thickness(0);
                }
                if (cardStreamSettings != null) cardStreamSettings.Visibility = Visibility.Collapsed;
                if (viewportBorder != null)
                {
                    viewportBorder.Margin = new Thickness(0);
                    viewportBorder.CornerRadius = new CornerRadius(0);
                    viewportBorder.BorderThickness = new Thickness(0);
                }
                if (imgViewport != null) imgViewport.Stretch = System.Windows.Media.Stretch.Fill;
                if (pnlFloatingBar != null) pnlFloatingBar.Visibility = Visibility.Visible;

                _isFullscreen = true;

                if (btnToggleFullscreen != null) btnToggleFullscreen.Content = "Thoát Toàn màn hình";
                if (btnToggleFullscreenTop != null) btnToggleFullscreenTop.Content = "Thoát Toàn màn hình";
            }
            else
            {
                if (imgViewport != null) imgViewport.Stretch = System.Windows.Media.Stretch.Uniform;
                this.WindowStyle = _prevWindowStyle;
                this.WindowState = _prevWindowState;
                this.ResizeMode = _prevResizeMode;

                if (_origWindowBg != null) this.Background = _origWindowBg;
                else this.SetResourceReference(BackgroundProperty, "AppBackgroundBrush");

                if (rootGrid != null)
                {
                    if (_origRootGridBg != null) rootGrid.Background = _origRootGridBg;
                    else rootGrid.SetResourceReference(System.Windows.Controls.Grid.BackgroundProperty, "AppBackgroundBrush");
                }

                if (cardRemoteScreen != null)
                {
                    if (_origCardRemoteBg != null) cardRemoteScreen.Background = _origCardRemoteBg;
                    else cardRemoteScreen.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "SurfaceBrush");
                    cardRemoteScreen.Padding = new Thickness(0);
                    cardRemoteScreen.CornerRadius = new CornerRadius(16);
                    cardRemoteScreen.BorderThickness = new Thickness(1);
                }

                if (viewportBorder != null)
                {
                    if (_origViewportBorderBg != null) viewportBorder.Background = _origViewportBorderBg;
                    else viewportBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "ViewportGradientBrush");
                    viewportBorder.Margin = new Thickness(12, 0, 12, 12);
                    viewportBorder.CornerRadius = new CornerRadius(12);
                    viewportBorder.BorderThickness = new Thickness(1);
                }

                if (viewportGrid != null && _origViewportGridBg != null)
                {
                    viewportGrid.Background = _origViewportGridBg;
                }

                if (borderProductBar != null) borderProductBar.Visibility = Visibility.Visible;
                if (gridWorkspace != null) gridWorkspace.Margin = new Thickness(22);
                if (colRightSidebar != null) colRightSidebar.Width = new GridLength(356);
                if (colRightSidebarGap != null) colRightSidebarGap.Width = new GridLength(16);
                if (rowStreamSettings != null) rowStreamSettings.Height = new GridLength(184);
                if (rowStreamSettingsGap != null) rowStreamSettingsGap.Height = new GridLength(16);
                if (rowHeaderRemoteScreen != null) rowHeaderRemoteScreen.Height = new GridLength(62);
                if (headerRemoteScreen != null) headerRemoteScreen.Visibility = Visibility.Visible;
                if (cardStreamSettings != null) cardStreamSettings.Visibility = Visibility.Visible;
                if (pnlFloatingBar != null) pnlFloatingBar.Visibility = Visibility.Collapsed;

                _isFullscreen = false;

                if (btnToggleFullscreen != null) btnToggleFullscreen.Content = "Toàn màn hình";
                if (btnToggleFullscreenTop != null) btnToggleFullscreenTop.Content = "Toàn màn hình";
            }
        }

        private void BtnToggleFullscreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F11 || (e.Key == Key.Escape && _isFullscreen))
            {
                ToggleFullscreen();
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        #endregion

        #region Viewport Mouse & Keyboard Interactivity

        private void ImgViewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_serverService.IsClientConnected) return;

            if ((DateTime.Now - _lastMouseMoveTime).TotalMilliseconds < 20) return;
            _lastMouseMoveTime = DateTime.Now;

            bool isFill = imgViewport.Stretch == System.Windows.Media.Stretch.Fill;

            Point pos = e.GetPosition(imgViewport);
            var (isValid, normX, normY) = WpfCoordinateMapper.GetNormalizedCoordinates(
                pos.X, pos.Y,
                imgViewport.ActualWidth, imgViewport.ActualHeight,
                _remoteWidth, _remoteHeight,
                isFill
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

            bool isFill = imgViewport.Stretch == System.Windows.Media.Stretch.Fill;
            Point pos = e.GetPosition(imgViewport);
            var (isValid, normX, normY) = WpfCoordinateMapper.GetNormalizedCoordinates(
                pos.X, pos.Y,
                imgViewport.ActualWidth, imgViewport.ActualHeight,
                _remoteWidth, _remoteHeight,
                isFill
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

            bool isFill = imgViewport.Stretch == System.Windows.Media.Stretch.Fill;
            Point pos = e.GetPosition(imgViewport);
            var (isValid, normX, normY) = WpfCoordinateMapper.GetNormalizedCoordinates(
                pos.X, pos.Y,
                imgViewport.ActualWidth, imgViewport.ActualHeight,
                _remoteWidth, _remoteHeight,
                isFill
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

            bool isFill = imgViewport.Stretch == System.Windows.Media.Stretch.Fill;
            Point pos = e.GetPosition(imgViewport);
            var (isValid, normX, normY) = WpfCoordinateMapper.GetNormalizedCoordinates(
                pos.X, pos.Y,
                imgViewport.ActualWidth, imgViewport.ActualHeight,
                _remoteWidth, _remoteHeight,
                isFill
            );

            if (!isValid) return;

            _serverService.SendMouseCommand("scroll", normX, normY, "left", e.Delta);
        }

        private void Viewport_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F11 || (e.Key == Key.Escape && _isFullscreen))
            {
                ToggleFullscreen();
                e.Handled = true;
                return;
            }

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
                Key.D0 or Key.NumPad0 => "0",
                Key.D1 or Key.NumPad1 => "1",
                Key.D2 or Key.NumPad2 => "2",
                Key.D3 or Key.NumPad3 => "3",
                Key.D4 or Key.NumPad4 => "4",
                Key.D5 or Key.NumPad5 => "5",
                Key.D6 or Key.NumPad6 => "6",
                Key.D7 or Key.NumPad7 => "7",
                Key.D8 or Key.NumPad8 => "8",
                Key.D9 or Key.NumPad9 => "9",
                Key.OemQuestion => "/",
                Key.OemPeriod => ".",
                Key.OemComma => ",",
                Key.OemMinus => "-",
                Key.OemPlus => "+",
                Key.OemQuotes => "'",
                Key.OemSemicolon => ";",
                Key.OemOpenBrackets => "[",
                Key.OemCloseBrackets => "]",
                Key.OemBackslash => "\\",
                Key.OemTilde => "`",
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

        private void ApplyConfigLive()
        {
            if (sldQuality == null || sldScale == null || sldFps == null) return;
            int quality = (int)sldQuality.Value;
            double scale = sldScale.Value;
            int fpsLimit = (int)sldFps.Value;
            SaveCurrentSettings();
            if (_serverService != null && _serverService.IsClientConnected)
            {
                _serverService.SendConfigCommand(quality, scale, fpsLimit);
            }
        }

        private void SldQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtQualityVal != null)
                txtQualityVal.Text = $"{(int)e.NewValue}%";
            ApplyConfigLive();
        }

        private void SldScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtScaleVal != null)
                txtScaleVal.Text = $"{e.NewValue:F2}x";
            ApplyConfigLive();
        }

        private void SldFps_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtFpsVal != null)
                txtFpsVal.Text = $"{(int)e.NewValue} FPS";
            ApplyConfigLive();
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLogs.Clear();
        }

        private void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (_screenBitmap == null || _screenBitmap.PixelWidth == 0 || _screenBitmap.PixelHeight == 0)
            {
                WpfMessageBox.Show("Chưa có hình ảnh màn hình từ xa để chụp!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Create a copy of the WriteableBitmap frame
                BitmapSource frameCopy = _screenBitmap.Clone();
                frameCopy.Freeze();

                // Copy image to Clipboard for quick pasting
                System.Windows.Clipboard.SetImage(frameCopy);

                // Open SaveFileDialog to save image
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
                    DefaultExt = ".png",
                    FileName = $"Remote_Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                };

                if (dialog.ShowDialog() == true)
                {
                    string filePath = dialog.FileName;
                    BitmapEncoder encoder = filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                        ? new JpegBitmapEncoder { QualityLevel = 95 }
                        : new PngBitmapEncoder();

                    encoder.Frames.Add(BitmapFrame.Create(frameCopy));
                    using (var stream = File.Create(filePath))
                    {
                        encoder.Save(stream);
                    }

                    ServerService_OnLog($"Đã chụp màn hình và lưu tại: {filePath} (Đã sao chép vào Clipboard)");
                    WpfMessageBox.Show($"Đã lưu ảnh màn hình thành công!\n\nĐường dẫn: {filePath}\n(Đã tự động sao chép vào Clipboard)", "Chụp màn hình", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Không thể lưu ảnh màn hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            SaveCurrentSettings();
            _serverService?.Stop();
            base.OnClosed(e);
        }
    }
}
