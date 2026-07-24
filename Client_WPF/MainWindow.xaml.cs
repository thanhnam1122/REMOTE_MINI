#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RemoteDesktopClient.Services;
using RemoteDesktopClient.UI;
using WpfMessageBox = System.Windows.MessageBox;
using MediaBrush = System.Windows.Media.Brush;

namespace RemoteDesktopClient
{
    public partial class MainWindow : Window
    {
        private readonly NetworkClientService _clientService;
        private UserSettings _userSettings;

        public MainWindow()
        {
            FontInstallerService.InstallFontFiles();

            InitializeComponent();

            _userSettings = ConfigService.Load();

            txtIp.Text = _userSettings.ServerIp;
            txtPort.Text = _userSettings.ServerPort.ToString();
            txtPin.Text = _userSettings.Pin;
            sldQuality.Value = _userSettings.Quality;
            sldScale.Value = _userSettings.Scale;
            sldFps.Value = _userSettings.Fps;

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
                });
            };

            _clientService = new NetworkClientService();
            _clientService.OnLog += ClientService_OnLog;
            _clientService.OnStatsUpdated += ClientService_OnStatsUpdated;
            _clientService.OnStatusChanged += ClientService_OnStatusChanged;

            // Log startup message
            Log("[System] WPF Remote Desktop Client đã khởi tạo thành công.");
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_clientService.IsRunning)
            {
                _clientService.Stop();
            }
            else
            {
                string ip = txtIp.Text.Trim();
                if (!int.TryParse(txtPort.Text.Trim(), out int port))
                {
                    WpfMessageBox.Show("Cổng (Port) không hợp lệ!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string pin = txtPin.Text.Trim();
                if (string.IsNullOrEmpty(pin))
                {
                    WpfMessageBox.Show("Vui lòng nhập Mã PIN!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Apply initial settings from UI sliders
                _clientService.Capturer.UpdateSettings((int)sldQuality.Value, sldScale.Value, (int)sldFps.Value);
                SaveCurrentSettings();
                _clientService.Start(ip, port, pin);
            }
        }

        private void ClientService_OnLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                Log(message);
            });
        }

        private void Log(string message)
        {
            string timeStr = DateTime.Now.ToString("HH:mm:ss");
            txtLogs.AppendText($"[{timeStr}] {message}\n");
            txtLogs.ScrollToEnd();
        }

        private void ClientService_OnStatsUpdated(double fps, double kbps, int width, int height, long totalBytes)
        {
            Dispatcher.Invoke(() =>
            {
                txtFps.Text = fps.ToString("F1", CultureInfo.InvariantCulture);
                txtBandwidth.Text = kbps.ToString("F1", CultureInfo.InvariantCulture);
                txtResolution.Text = $"{width}x{height}";

                double totalMb = totalBytes / (1024.0 * 1024.0);
                txtTotalData.Text = totalMb < 1024 
                    ? $"{totalMb:F1} MB" 
                    : $"{(totalMb / 1024.0):F2} GB";
            });
        }

        private void ClientService_OnStatusChanged(bool isConnected, string statusMessage)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = statusMessage;

                if (isConnected)
                {
                    ApplyStatusPalette("Success");
                    btnConnect.Content = "Ngắt kết nối";
                }
                else if (_clientService.IsRunning)
                {
                    ApplyStatusPalette("Warning");
                    btnConnect.Content = "Hủy kết nối";
                }
                else
                {
                    ApplyStatusPalette("Danger");
                    btnConnect.Content = "Kết nối và chia sẻ";
                }
            });
        }

        private void SldQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtQualityVal == null) return;
            int val = (int)e.NewValue;
            txtQualityVal.Text = $"{val}%";
            _clientService?.Capturer.UpdateSettings(val, null, null);
        }

        private void SldScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtScaleVal == null) return;
            double val = e.NewValue;
            txtScaleVal.Text = $"{val:F2}x";
            _clientService?.Capturer.UpdateSettings(null, val, null);
        }

        private void SldFps_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtFpsVal == null) return;
            int val = (int)e.NewValue;
            txtFpsVal.Text = $"{val} FPS";
            _clientService?.Capturer.UpdateSettings(null, null, val);
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLogs.Clear();
        }

        private void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int width = (int)SystemParameters.PrimaryScreenWidth;
                int height = (int)SystemParameters.PrimaryScreenHeight;

                using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(width, height), System.Drawing.CopyPixelOperation.SourceCopy);
                }

                IntPtr hBitmap = bmp.GetHbitmap();
                BitmapSource bitmapSource;
                try
                {
                    bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    DeleteObject(hBitmap);
                }

                System.Windows.Clipboard.SetImage(bitmapSource);

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
                    DefaultExt = ".png",
                    FileName = $"Client_Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                };

                if (dialog.ShowDialog() == true)
                {
                    string filePath = dialog.FileName;
                    BitmapEncoder encoder = filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                        ? new JpegBitmapEncoder { QualityLevel = 95 }
                        : new PngBitmapEncoder();

                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    using (var stream = File.Create(filePath))
                    {
                        encoder.Save(stream);
                    }

                    Log($"Đã chụp màn hình và lưu tại: {filePath} (Đã sao chép vào Clipboard)");
                    WpfMessageBox.Show($"Đã lưu ảnh màn hình thành công!\n\nĐường dẫn: {filePath}\n(Đã tự động sao chép vào Clipboard)", "Chụp màn hình", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Không thể lưu ảnh màn hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
        private void ApplyStatusPalette(string state)
        {
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
            ApplyStatusPalette(_clientService != null && _clientService.IsConnected ? "Success" : _clientService != null && _clientService.IsRunning ? "Warning" : "Danger");
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
            _userSettings.ServerIp = txtIp.Text.Trim();
            if (int.TryParse(txtPort.Text.Trim(), out int port)) _userSettings.ServerPort = port;
            _userSettings.Pin = txtPin.Text.Trim();
            _userSettings.Quality = (int)sldQuality.Value;
            _userSettings.Scale = sldScale.Value;
            _userSettings.Fps = (int)sldFps.Value;

            ConfigService.Save(_userSettings);
        }

        #region Fullscreen Toggle

        private bool _isFullscreen = false;
        private WindowStyle _prevWindowStyle = WindowStyle.SingleBorderWindow;
        private WindowState _prevWindowState = WindowState.Normal;
        private ResizeMode _prevResizeMode = ResizeMode.CanResize;

        private void ToggleFullscreen()
        {
            if (!_isFullscreen)
            {
                _prevWindowStyle = this.WindowStyle;
                _prevWindowState = this.WindowState;
                _prevResizeMode = this.ResizeMode;

                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Normal;
                this.WindowState = WindowState.Maximized;
                this.ResizeMode = ResizeMode.NoResize;
                _isFullscreen = true;

                if (btnToggleFullscreen != null) btnToggleFullscreen.Content = "Thoát Toàn màn hình";
            }
            else
            {
                this.WindowStyle = _prevWindowStyle;
                this.WindowState = _prevWindowState;
                this.ResizeMode = _prevResizeMode;
                _isFullscreen = false;

                if (btnToggleFullscreen != null) btnToggleFullscreen.Content = "Toàn màn hình";
            }
        }

        private void BtnToggleFullscreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F11 || (e.Key == System.Windows.Input.Key.Escape && _isFullscreen))
            {
                ToggleFullscreen();
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            SaveCurrentSettings();
            _clientService?.Stop();
            _clientService?.Capturer.Dispose();
            base.OnClosed(e);
        }
    }
}
