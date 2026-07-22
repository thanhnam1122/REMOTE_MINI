#nullable enable
using System;
using System.Drawing;
using System.IO;

using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using RemoteDesktopServer.Helpers;
using RemoteDesktopServer.Services;

namespace RemoteDesktopServer
{
    public partial class MainForm : Form
    {
        private readonly TcpServerService _serverService;
        private bool _isControlEnabled = true;
        private Image? _lastFrame = null;
        private string _localIp = "127.0.0.1";

        public MainForm()
        {
            InitializeComponent();
            _serverService = new TcpServerService();

            // Wire Service Events
            _serverService.OnLog += Server_OnLog;
            _serverService.OnClientConnected += Server_OnClientConnected;
            _serverService.OnClientDisconnected += Server_OnClientDisconnected;
            _serverService.OnFrameReceived += Server_OnFrameReceived;
            _serverService.OnStatsUpdated += Server_OnStatsUpdated;

            // Enable double buffering for smooth PictureBox rendering
            typeof(PictureBox).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(pbScreen, true, null);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            _localIp = GetLocalIPAddress();
            lblHeaderSub.Text = $"Máy điều khiển | Server IP Local: {_localIp} | Nhóm: Thành Nam - Minh Hoàng - Tấn Phước";

            cmbScale.SelectedIndex = 0; // 100%
            cmbFps.SelectedIndex = 1;   // 30 FPS

            // Auto start server on load
            BtnStartServer_Click(this, EventArgs.Empty);
        }

        private string GetLocalIPAddress()
        {
            try
            {
                using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private void BtnStartServer_Click(object? sender, EventArgs e)
        {
            if (!_serverService.IsRunning)
            {
                if (!int.TryParse(txtPort.Text.Trim(), out int port))
                {
                    MessageBox.Show("Cổng (Port) không hợp lệ!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _serverService.ExpectedPin = txtPin.Text.Trim();
                _serverService.Start(port);

                btnStartServer.Text = "⏹ STOP SERVER";
                btnStartServer.BackColor = Color.FromArgb(243, 139, 168); // Pink/Red
                lblStatusBadge.Text = $"🟢 LISTENING ON PORT {port}";
                lblStatusBadge.ForeColor = Color.FromArgb(166, 227, 161); // Light Green
            }
            else
            {
                _serverService.Stop();

                btnStartServer.Text = "▶ START SERVER";
                btnStartServer.BackColor = Color.FromArgb(137, 180, 250); // Blue
                lblStatusBadge.Text = "🔴 SERVER OFF";
                lblStatusBadge.ForeColor = Color.FromArgb(243, 139, 168);

                pbScreen.Image = null;
                _lastFrame?.Dispose();
                _lastFrame = null;
            }
        }

        #region Server Events

        private void Server_OnLog(string msg)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Server_OnLog(msg)));
                return;
            }

            string time = DateTime.Now.ToString("[HH:mm:ss]");
            txtLog.AppendText($"{time} {msg}\r\n");
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void Server_OnClientConnected(string clientEp, string clientName)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Server_OnClientConnected(clientEp, clientName)));
                return;
            }

            lblClientStatus.Text = $"CONNECTED: {clientName}\n({clientEp})";
            lblClientStatus.ForeColor = Color.FromArgb(166, 227, 161);
        }

        private void Server_OnClientDisconnected()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Server_OnClientDisconnected()));
                return;
            }

            lblClientStatus.Text = "DISCONNECTED";
            lblClientStatus.ForeColor = Color.FromArgb(243, 139, 168);
            lblFps.Text = "⚡ FPS: 0";
            lblBandwidth.Text = "📊 Tốc độ: 0 KB/s";
        }

        private void Server_OnFrameReceived(Image newFrame, ushort origW, ushort origH, int frameSize)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Server_OnFrameReceived(newFrame, origW, origH, frameSize)));
                return;
            }

            _lastFrame?.Dispose();
            _lastFrame = newFrame;
            pbScreen.Image = _lastFrame;
            lblResolution.Text = $"Độ phân giải thực: {origW}x{origH}";
        }

        private void Server_OnStatsUpdated(double fps, double kbps)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Server_OnStatsUpdated(fps, kbps)));
                return;
            }

            lblFps.Text = $"⚡ FPS: {fps:F1}";
            lblBandwidth.Text = $"📊 Tốc độ: {kbps:F1} KB/s";
        }

        #endregion

        #region Control & Configuration

        private void BtnApplyConfig_Click(object sender, EventArgs e)
        {
            if (!_serverService.IsClientConnected)
            {
                MessageBox.Show("Chưa có Client nào kết nối!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int quality = trackQuality.Value;
            double scale = 1.0;
            if (cmbScale.SelectedIndex == 0) scale = 1.0;
            else if (cmbScale.SelectedIndex == 1) scale = 0.75;
            else if (cmbScale.SelectedIndex == 2) scale = 0.5;

            int fpsLimit = 30;
            if (cmbFps.SelectedIndex == 0) fpsLimit = 15;
            else if (cmbFps.SelectedIndex == 1) fpsLimit = 30;
            else if (cmbFps.SelectedIndex == 2) fpsLimit = 60;

            _ = _serverService.SendConfigUpdateAsync(quality, scale, fpsLimit);
        }

        private void TrackQuality_Scroll(object sender, EventArgs e)
        {
            lblQualityValue.Text = $"{trackQuality.Value}%";
        }

        private void BtnToggleControl_Click(object sender, EventArgs e)
        {
            _isControlEnabled = !_isControlEnabled;
            if (_isControlEnabled)
            {
                btnToggleControl.Text = "🔒 KHÓA ĐIỀU KHIỂN CHUỘT/PHÍM";
                btnToggleControl.BackColor = Color.FromArgb(166, 227, 161);
                btnToggleControl.ForeColor = Color.Black;
            }
            else
            {
                btnToggleControl.Text = "🔓 MỞ ĐIỀU KHIỂN CHUỘT/PHÍM";
                btnToggleControl.BackColor = Color.FromArgb(249, 226, 175);
                btnToggleControl.ForeColor = Color.Black;
            }
        }

        private void BtnScreenshot_Click(object sender, EventArgs e)
        {
            if (pbScreen.Image == null)
            {
                MessageBox.Show("Chưa có hình ảnh màn hình để chụp!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg";
            sfd.FileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                pbScreen.Image.Save(sfd.FileName);
                MessageBox.Show($"Đã lưu ảnh màn hình thành công tại:\n{sfd.FileName}", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #endregion

        #region Mouse & Keyboard Remote Dispatcher

        private void PbScreen_MouseDown(object sender, MouseEventArgs e)
        {
            if (!_isControlEnabled || !_serverService.IsClientConnected) return;

            if (CoordinateMapper.GetNormalizedCoordinates(pbScreen, e.X, e.Y, out float normX, out float normY))
            {
                string buttonStr = e.Button switch
                {
                    MouseButtons.Right => "right",
                    MouseButtons.Middle => "middle",
                    _ => "left"
                };

                _ = _serverService.SendMouseCommandAsync("down", normX, normY, buttonStr);
            }
        }

        private void PbScreen_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_isControlEnabled || !_serverService.IsClientConnected) return;

            if (CoordinateMapper.GetNormalizedCoordinates(pbScreen, e.X, e.Y, out float normX, out float normY))
            {
                string buttonStr = e.Button switch
                {
                    MouseButtons.Right => "right",
                    MouseButtons.Middle => "middle",
                    _ => "left"
                };

                _ = _serverService.SendMouseCommandAsync("up", normX, normY, buttonStr);
            }
        }

        private void PbScreen_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isControlEnabled || !_serverService.IsClientConnected) return;

            // Only send mouse move when dragging to conserve bandwidth
            if (e.Button != MouseButtons.None)
            {
                if (CoordinateMapper.GetNormalizedCoordinates(pbScreen, e.X, e.Y, out float normX, out float normY))
                {
                    _ = _serverService.SendMouseCommandAsync("move", normX, normY);
                }
            }
        }

        private void PbScreen_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!_isControlEnabled || !_serverService.IsClientConnected) return;

            if (CoordinateMapper.GetNormalizedCoordinates(pbScreen, e.X, e.Y, out float normX, out float normY))
            {
                string buttonStr = e.Button == MouseButtons.Right ? "right" : "left";
                _ = _serverService.SendMouseCommandAsync("dclick", normX, normY, buttonStr);
            }
        }

        private void PbScreen_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!_isControlEnabled || !_serverService.IsClientConnected) return;

            if (CoordinateMapper.GetNormalizedCoordinates(pbScreen, e.X, e.Y, out float normX, out float normY))
            {
                _ = _serverService.SendMouseCommandAsync("scroll", normX, normY, "left", e.Delta);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_isControlEnabled && _serverService.IsClientConnected && pbScreen.Focused)
            {
                Keys key = keyData & Keys.KeyCode;
                string keyName = key.ToString().ToLower();

                _ = _serverService.SendKeyboardCommandAsync("press", keyName);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isControlEnabled || !_serverService.IsClientConnected) return;

            string keyName = e.KeyCode.ToString().ToLower();
            _ = _serverService.SendKeyboardCommandAsync("down", keyName);
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (!_isControlEnabled || !_serverService.IsClientConnected) return;

            string keyName = e.KeyCode.ToString().ToLower();
            _ = _serverService.SendKeyboardCommandAsync("up", keyName);
        }

        #endregion

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _serverService.Stop();
        }
    }
}
