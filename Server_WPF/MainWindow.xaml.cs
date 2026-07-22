#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RemoteDesktopServer.Helpers;
using RemoteDesktopServer.Services;

using WpfMessageBox = System.Windows.MessageBox;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;

namespace RemoteDesktopServer
{
    public partial class MainWindow : Window
    {
        private readonly TcpServerService _serverService;
        private ushort _remoteWidth = 1920;
        private ushort _remoteHeight = 1080;
        private DateTime _lastMouseMoveTime = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();

            _serverService = new TcpServerService();
            _serverService.OnLog += ServerService_OnLog;
            _serverService.OnClientConnected += ServerService_OnClientConnected;
            _serverService.OnClientDisconnected += ServerService_OnClientDisconnected;
            _serverService.OnFrameReceived += ServerService_OnFrameReceived;
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

            btnToggleServer.Content = "🛑 Dừng Server";
            UpdateStatusUI(false, $"Đang lắng nghe Cổng {port}...");
        }

        private void StopServer()
        {
            _serverService.Stop();
            btnToggleServer.Content = "🚀 Chạy Server";
            UpdateStatusUI(false, "Server đã dừng");
            overlayPlaceholder.Visibility = Visibility.Visible;
            imgViewport.Source = null;
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
            });
        }

        private void ServerService_OnClientDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusUI(false, "Chờ Client kết nối...");
                overlayPlaceholder.Visibility = Visibility.Visible;
                imgViewport.Source = null;
                txtFps.Text = "0.0";
                txtBandwidth.Text = "0.0";
                txtResolution.Text = "N/A";
                txtFrameSize.Text = "0 KB";
            });
        }

        private void ServerService_OnFrameReceived(BitmapSource bitmap, ushort origW, ushort origH, int frameSize)
        {
            Dispatcher.Invoke(() =>
            {
                _remoteWidth = origW;
                _remoteHeight = origH;
                imgViewport.Source = bitmap;
                txtResolution.Text = $"{origW}x{origH}";
                txtFrameSize.Text = $"{(frameSize / 1024.0):F1} KB";
            });
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
            if (isConnected)
            {
                ellipseStatus.Fill = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#10B981")); // Green
                borderStatusPill.Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#065F46"));
                borderStatusPill.BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#059669"));
            }
            else if (_serverService.IsRunning)
            {
                ellipseStatus.Fill = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#F59E0B")); // Yellow
                borderStatusPill.Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#78350F"));
                borderStatusPill.BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#D97706"));
            }
            else
            {
                ellipseStatus.Fill = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#EF4444")); // Red
                borderStatusPill.Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#1F2937"));
                borderStatusPill.BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#374151"));
            }
        }

        #region Viewport Mouse & Keyboard Interactivity

        private void ImgViewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_serverService.IsClientConnected) return;

            // Rate limit mouse move packets to ~50 Hz (every 20ms) when dragging/moving
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
            viewportBorder.Focus(); // Take focus for keyboard events

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
