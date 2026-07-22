#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using RemoteDesktopClient.Services;
using WpfMessageBox = System.Windows.MessageBox;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace RemoteDesktopClient
{
    public partial class MainWindow : Window
    {
        private readonly NetworkClientService _clientService;

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
                    ellipseStatus.Fill = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#10B981")); // Green
                    borderStatusPill.Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#065F46"));
                    borderStatusPill.BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#059669"));
                    btnConnect.Content = "⛔ Ngắt Kết Nối Server";
                }
                else if (_clientService.IsRunning)
                {
                    ellipseStatus.Fill = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#F59E0B")); // Yellow
                    borderStatusPill.Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#78350F"));
                    borderStatusPill.BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#D97706"));
                    btnConnect.Content = "⏹ Hủy Kết Nối";
                }
                else
                {
                    ellipseStatus.Fill = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#EF4444")); // Red
                    borderStatusPill.Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#1F2937"));
                    borderStatusPill.BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#374151"));
                    btnConnect.Content = "🚀 Bắt đầu Kết nối Server";
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

        private void BtnClearLog_Click(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLogs.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            _clientService.Stop();
            _clientService.Capturer.Dispose();
            base.OnClosed(e);
        }
    }
}
