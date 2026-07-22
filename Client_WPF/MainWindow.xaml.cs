#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using RemoteDesktopClient.Services;
using RemoteDesktopClient.UI;
using WpfMessageBox = System.Windows.MessageBox;
using MediaBrush = System.Windows.Media.Brush;

namespace RemoteDesktopClient
{
    public partial class MainWindow : Window
    {
        private readonly NetworkClientService _clientService;
        private YellowBorderOverlay? _borderOverlay;

        public MainWindow()
        {
            InitializeComponent();

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

                    // Show native glowing yellow screen-sharing border
                    if (_borderOverlay == null)
                    {
                        _borderOverlay = new YellowBorderOverlay();
                    }
                    _borderOverlay.Show();
                }
                else if (_clientService.IsRunning)
                {
                    ApplyStatusPalette("Warning");
                    btnConnect.Content = "Hủy kết nối";

                    _borderOverlay?.Hide();
                }
                else
                {
                    ApplyStatusPalette("Danger");
                    btnConnect.Content = "Kết nối và chia sẻ";

                    _borderOverlay?.Hide();
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
        private void ApplyStatusPalette(string state)
        {
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
            ApplyStatusPalette(_clientService.IsConnected ? "Success" : _clientService.IsRunning ? "Warning" : "Danger");
        }

        protected override void OnClosed(EventArgs e)
        {
            _borderOverlay?.Close();
            _clientService.Stop();
            _clientService.Capturer.Dispose();
            base.OnClosed(e);
        }
    }
}
