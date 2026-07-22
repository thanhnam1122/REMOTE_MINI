namespace RemoteDesktopServer
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            pnlHeader = new System.Windows.Forms.Panel();
            lblHeaderTitle = new System.Windows.Forms.Label();
            lblHeaderSub = new System.Windows.Forms.Label();
            lblStatusBadge = new System.Windows.Forms.Label();

            pnlSidebar = new System.Windows.Forms.Panel();
            
            grpConnection = new System.Windows.Forms.GroupBox();
            lblPort = new System.Windows.Forms.Label();
            txtPort = new System.Windows.Forms.TextBox();
            lblPin = new System.Windows.Forms.Label();
            txtPin = new System.Windows.Forms.TextBox();
            btnStartServer = new System.Windows.Forms.Button();
            lblClientStatus = new System.Windows.Forms.Label();

            grpStream = new System.Windows.Forms.GroupBox();
            lblQuality = new System.Windows.Forms.Label();
            lblQualityValue = new System.Windows.Forms.Label();
            trackQuality = new System.Windows.Forms.TrackBar();
            lblScale = new System.Windows.Forms.Label();
            cmbScale = new System.Windows.Forms.ComboBox();
            lblFpsCap = new System.Windows.Forms.Label();
            cmbFps = new System.Windows.Forms.ComboBox();
            btnApplyConfig = new System.Windows.Forms.Button();

            grpControl = new System.Windows.Forms.GroupBox();
            btnToggleControl = new System.Windows.Forms.Button();
            btnScreenshot = new System.Windows.Forms.Button();

            grpStats = new System.Windows.Forms.GroupBox();
            lblFps = new System.Windows.Forms.Label();
            lblBandwidth = new System.Windows.Forms.Label();
            lblResolution = new System.Windows.Forms.Label();

            lblLogHeader = new System.Windows.Forms.Label();
            txtLog = new System.Windows.Forms.TextBox();

            pnlMain = new System.Windows.Forms.Panel();
            pbScreen = new System.Windows.Forms.PictureBox();

            pnlHeader.SuspendLayout();
            pnlSidebar.SuspendLayout();
            grpConnection.SuspendLayout();
            grpStream.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trackQuality).BeginInit();
            grpControl.SuspendLayout();
            grpStats.SuspendLayout();
            pnlMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pbScreen).BeginInit();
            SuspendLayout();

            // 
            // pnlHeader
            // 
            pnlHeader.BackColor = System.Drawing.Color.FromArgb(24, 24, 37);
            pnlHeader.Controls.Add(lblStatusBadge);
            pnlHeader.Controls.Add(lblHeaderSub);
            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            pnlHeader.Location = new System.Drawing.Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new System.Drawing.Size(1280, 65);
            pnlHeader.TabIndex = 0;

            // 
            // lblHeaderTitle
            // 
            lblHeaderTitle.AutoSize = true;
            lblHeaderTitle.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblHeaderTitle.ForeColor = System.Drawing.Color.FromArgb(137, 180, 250);
            lblHeaderTitle.Location = new System.Drawing.Point(15, 10);
            lblHeaderTitle.Name = "lblHeaderTitle";
            lblHeaderTitle.Size = new System.Drawing.Size(430, 28);
            lblHeaderTitle.TabIndex = 0;
            lblHeaderTitle.Text = "🖥️ REMOTE DESKTOP MINI - MÁY ĐIỀU KHIỂN";

            // 
            // lblHeaderSub
            // 
            lblHeaderSub.AutoSize = true;
            lblHeaderSub.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point);
            lblHeaderSub.ForeColor = System.Drawing.Color.FromArgb(186, 194, 222);
            lblHeaderSub.Location = new System.Drawing.Point(18, 38);
            lblHeaderSub.Name = "lblHeaderSub";
            lblHeaderSub.Size = new System.Drawing.Size(480, 15);
            lblHeaderSub.TabIndex = 1;
            lblHeaderSub.Text = "Máy điều khiển (Server .NET 9 WinForms) | Nhóm: Thành Nam - Minh Hoàng - Tấn Phước";

            // 
            // lblStatusBadge
            // 
            lblStatusBadge.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            lblStatusBadge.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblStatusBadge.ForeColor = System.Drawing.Color.FromArgb(166, 227, 161);
            lblStatusBadge.Location = new System.Drawing.Point(960, 20);
            lblStatusBadge.Name = "lblStatusBadge";
            lblStatusBadge.Size = new System.Drawing.Size(300, 25);
            lblStatusBadge.TabIndex = 2;
            lblStatusBadge.Text = "🔴 SERVER OFF";
            lblStatusBadge.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

            // 
            // pnlSidebar
            // 
            pnlSidebar.AutoScroll = true;
            pnlSidebar.BackColor = System.Drawing.Color.FromArgb(42, 42, 61);
            pnlSidebar.Controls.Add(txtLog);
            pnlSidebar.Controls.Add(lblLogHeader);
            pnlSidebar.Controls.Add(grpStats);
            pnlSidebar.Controls.Add(grpControl);
            pnlSidebar.Controls.Add(grpStream);
            pnlSidebar.Controls.Add(grpConnection);
            pnlSidebar.Dock = System.Windows.Forms.DockStyle.Left;
            pnlSidebar.Location = new System.Drawing.Point(0, 65);
            pnlSidebar.Name = "pnlSidebar";
            pnlSidebar.Padding = new System.Windows.Forms.Padding(12);
            pnlSidebar.Size = new System.Drawing.Size(330, 655);
            pnlSidebar.TabIndex = 1;

            // 
            // grpConnection
            // 
            grpConnection.Controls.Add(lblClientStatus);
            grpConnection.Controls.Add(btnStartServer);
            grpConnection.Controls.Add(txtPin);
            grpConnection.Controls.Add(lblPin);
            grpConnection.Controls.Add(txtPort);
            grpConnection.Controls.Add(lblPort);
            grpConnection.Dock = System.Windows.Forms.DockStyle.Top;
            grpConnection.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            grpConnection.ForeColor = System.Drawing.Color.FromArgb(137, 180, 250);
            grpConnection.Location = new System.Drawing.Point(12, 12);
            grpConnection.Name = "grpConnection";
            grpConnection.Size = new System.Drawing.Size(306, 145);
            grpConnection.TabIndex = 0;
            grpConnection.TabStop = false;
            grpConnection.Text = "⚙️ CẤU HÌNH SERVER";

            // 
            // lblPort
            // 
            lblPort.AutoSize = true;
            lblPort.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblPort.ForeColor = System.Drawing.Color.FromArgb(217, 224, 238);
            lblPort.Location = new System.Drawing.Point(10, 25);
            lblPort.Name = "lblPort";
            lblPort.Size = new System.Drawing.Size(70, 15);
            lblPort.Text = "Port Listen:";

            // 
            // txtPort
            // 
            txtPort.BackColor = System.Drawing.Color.FromArgb(30, 30, 46);
            txtPort.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtPort.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtPort.ForeColor = System.Drawing.Color.White;
            txtPort.Location = new System.Drawing.Point(85, 22);
            txtPort.Name = "txtPort";
            txtPort.Size = new System.Drawing.Size(60, 24);
            txtPort.TabIndex = 1;
            txtPort.Text = "8888";

            // 
            // lblPin
            // 
            lblPin.AutoSize = true;
            lblPin.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblPin.ForeColor = System.Drawing.Color.FromArgb(217, 224, 238);
            lblPin.Location = new System.Drawing.Point(155, 25);
            lblPin.Name = "lblPin";
            lblPin.Size = new System.Drawing.Size(53, 15);
            lblPin.Text = "Mã PIN:";

            // 
            // txtPin
            // 
            txtPin.BackColor = System.Drawing.Color.FromArgb(30, 30, 46);
            txtPin.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtPin.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            txtPin.ForeColor = System.Drawing.Color.FromArgb(249, 226, 175);
            txtPin.Location = new System.Drawing.Point(212, 22);
            txtPin.Name = "txtPin";
            txtPin.Size = new System.Drawing.Size(80, 24);
            txtPin.TabIndex = 2;
            txtPin.Text = "1234";

            // 
            // btnStartServer
            // 
            btnStartServer.BackColor = System.Drawing.Color.FromArgb(137, 180, 250);
            btnStartServer.FlatAppearance.BorderSize = 0;
            btnStartServer.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnStartServer.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnStartServer.ForeColor = System.Drawing.Color.Black;
            btnStartServer.Location = new System.Drawing.Point(10, 55);
            btnStartServer.Name = "btnStartServer";
            btnStartServer.Size = new System.Drawing.Size(282, 32);
            btnStartServer.TabIndex = 3;
            btnStartServer.Text = "▶ START SERVER";
            btnStartServer.UseVisualStyleBackColor = false;
            btnStartServer.Click += BtnStartServer_Click;

            // 
            // lblClientStatus
            // 
            lblClientStatus.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblClientStatus.ForeColor = System.Drawing.Color.FromArgb(243, 139, 168);
            lblClientStatus.Location = new System.Drawing.Point(10, 95);
            lblClientStatus.Name = "lblClientStatus";
            lblClientStatus.Size = new System.Drawing.Size(282, 35);
            lblClientStatus.TabIndex = 4;
            lblClientStatus.Text = "DISCONNECTED";
            lblClientStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // 
            // grpStream
            // 
            grpStream.Controls.Add(btnApplyConfig);
            grpStream.Controls.Add(cmbFps);
            grpStream.Controls.Add(lblFpsCap);
            grpStream.Controls.Add(cmbScale);
            grpStream.Controls.Add(lblScale);
            grpStream.Controls.Add(lblQualityValue);
            grpStream.Controls.Add(trackQuality);
            grpStream.Controls.Add(lblQuality);
            grpStream.Dock = System.Windows.Forms.DockStyle.Top;
            grpStream.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            grpStream.ForeColor = System.Drawing.Color.FromArgb(137, 180, 250);
            grpStream.Location = new System.Drawing.Point(12, 157);
            grpStream.Name = "grpStream";
            grpStream.Size = new System.Drawing.Size(306, 175);
            grpStream.TabIndex = 1;
            grpStream.TabStop = false;
            grpStream.Text = "🎨 ĐIỀU CHỈNH CHẤT LƯỢNG HÌNH ẢNH";

            // 
            // lblQuality
            // 
            lblQuality.AutoSize = true;
            lblQuality.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblQuality.ForeColor = System.Drawing.Color.FromArgb(217, 224, 238);
            lblQuality.Location = new System.Drawing.Point(10, 25);
            lblQuality.Name = "lblQuality";
            lblQuality.Size = new System.Drawing.Size(102, 15);
            lblQuality.Text = "Chất lượng JPEG:";

            // 
            // lblQualityValue
            // 
            lblQualityValue.AutoSize = true;
            lblQualityValue.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblQualityValue.ForeColor = System.Drawing.Color.FromArgb(249, 226, 175);
            lblQualityValue.Location = new System.Drawing.Point(120, 25);
            lblQualityValue.Name = "lblQualityValue";
            lblQualityValue.Size = new System.Drawing.Size(31, 15);
            lblQualityValue.Text = "65%";

            // 
            // trackQuality
            // 
            trackQuality.AutoSize = false;
            trackQuality.LargeChange = 10;
            trackQuality.Location = new System.Drawing.Point(160, 20);
            trackQuality.Maximum = 100;
            trackQuality.Minimum = 10;
            trackQuality.Name = "trackQuality";
            trackQuality.Size = new System.Drawing.Size(135, 25);
            trackQuality.SmallChange = 5;
            trackQuality.TabIndex = 1;
            trackQuality.Value = 65;
            trackQuality.Scroll += TrackQuality_Scroll;

            // 
            // lblScale
            // 
            lblScale.AutoSize = true;
            lblScale.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblScale.ForeColor = System.Drawing.Color.FromArgb(217, 224, 238);
            lblScale.Location = new System.Drawing.Point(10, 58);
            lblScale.Name = "lblScale";
            lblScale.Size = new System.Drawing.Size(84, 15);
            lblScale.Text = "Tỉ lệ màn hình:";

            // 
            // cmbScale
            // 
            cmbScale.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbScale.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbScale.FormattingEnabled = true;
            cmbScale.Items.AddRange(new object[] { "100% (Gốc)", "75% (Khuyên dùng)", "50% (Tiết kiệm Băng thông)" });
            cmbScale.Location = new System.Drawing.Point(115, 55);
            cmbScale.Name = "cmbScale";
            cmbScale.Size = new System.Drawing.Size(175, 23);
            cmbScale.TabIndex = 2;

            // 
            // lblFpsCap
            // 
            lblFpsCap.AutoSize = true;
            lblFpsCap.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblFpsCap.ForeColor = System.Drawing.Color.FromArgb(217, 224, 238);
            lblFpsCap.Location = new System.Drawing.Point(10, 92);
            lblFpsCap.Name = "lblFpsCap";
            lblFpsCap.Size = new System.Drawing.Size(95, 15);
            lblFpsCap.Text = "Tốc độ khung hình:";

            // 
            // cmbFps
            // 
            cmbFps.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbFps.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbFps.FormattingEnabled = true;
            cmbFps.Items.AddRange(new object[] { "15 FPS", "30 FPS", "60 FPS" });
            cmbFps.Location = new System.Drawing.Point(115, 89);
            cmbFps.Name = "cmbFps";
            cmbFps.Size = new System.Drawing.Size(175, 23);
            cmbFps.TabIndex = 3;

            // 
            // btnApplyConfig
            // 
            btnApplyConfig.BackColor = System.Drawing.Color.FromArgb(116, 199, 236);
            btnApplyConfig.FlatAppearance.BorderSize = 0;
            btnApplyConfig.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnApplyConfig.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnApplyConfig.ForeColor = System.Drawing.Color.Black;
            btnApplyConfig.Location = new System.Drawing.Point(10, 125);
            btnApplyConfig.Name = "btnApplyConfig";
            btnApplyConfig.Size = new System.Drawing.Size(282, 30);
            btnApplyConfig.TabIndex = 4;
            btnApplyConfig.Text = "ÁP DỤNG CẤU HÌNH CHO CLIENT";
            btnApplyConfig.UseVisualStyleBackColor = false;
            btnApplyConfig.Click += BtnApplyConfig_Click;

            // 
            // grpControl
            // 
            grpControl.Controls.Add(btnScreenshot);
            grpControl.Controls.Add(btnToggleControl);
            grpControl.Dock = System.Windows.Forms.DockStyle.Top;
            grpControl.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            grpControl.ForeColor = System.Drawing.Color.FromArgb(137, 180, 250);
            grpControl.Location = new System.Drawing.Point(12, 332);
            grpControl.Name = "grpControl";
            grpControl.Size = new System.Drawing.Size(306, 110);
            grpControl.TabIndex = 2;
            grpControl.TabStop = false;
            grpControl.Text = "🎮 TƯƠNG TÁC ĐIỀU KHIỂN";

            // 
            // btnToggleControl
            // 
            btnToggleControl.BackColor = System.Drawing.Color.FromArgb(166, 227, 161);
            btnToggleControl.FlatAppearance.BorderSize = 0;
            btnToggleControl.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnToggleControl.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnToggleControl.ForeColor = System.Drawing.Color.Black;
            btnToggleControl.Location = new System.Drawing.Point(10, 25);
            btnToggleControl.Name = "btnToggleControl";
            btnToggleControl.Size = new System.Drawing.Size(282, 32);
            btnToggleControl.TabIndex = 0;
            btnToggleControl.Text = "🔒 KHÓA ĐIỀU KHIỂN CHUỘT/PHÍM";
            btnToggleControl.UseVisualStyleBackColor = false;
            btnToggleControl.Click += BtnToggleControl_Click;

            // 
            // btnScreenshot
            // 
            btnScreenshot.BackColor = System.Drawing.Color.FromArgb(203, 166, 247);
            btnScreenshot.FlatAppearance.BorderSize = 0;
            btnScreenshot.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnScreenshot.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnScreenshot.ForeColor = System.Drawing.Color.Black;
            btnScreenshot.Location = new System.Drawing.Point(10, 65);
            btnScreenshot.Name = "btnScreenshot";
            btnScreenshot.Size = new System.Drawing.Size(282, 30);
            btnScreenshot.TabIndex = 1;
            btnScreenshot.Text = "📸 CHỤP ẢNH MÀN HÌNH TỪ XA";
            btnScreenshot.UseVisualStyleBackColor = false;
            btnScreenshot.Click += BtnScreenshot_Click;

            // 
            // grpStats
            // 
            grpStats.Controls.Add(lblResolution);
            grpStats.Controls.Add(lblBandwidth);
            grpStats.Controls.Add(lblFps);
            grpStats.Dock = System.Windows.Forms.DockStyle.Top;
            grpStats.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            grpStats.ForeColor = System.Drawing.Color.FromArgb(137, 180, 250);
            grpStats.Location = new System.Drawing.Point(12, 442);
            grpStats.Name = "grpStats";
            grpStats.Size = new System.Drawing.Size(306, 95);
            grpStats.TabIndex = 3;
            grpStats.TabStop = false;
            grpStats.Text = "📈 THÔNG SỐ HIỆU NĂNG";

            // 
            // lblFps
            // 
            lblFps.AutoSize = true;
            lblFps.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblFps.ForeColor = System.Drawing.Color.FromArgb(166, 227, 161);
            lblFps.Location = new System.Drawing.Point(10, 22);
            lblFps.Name = "lblFps";
            lblFps.Size = new System.Drawing.Size(59, 17);
            lblFps.TabIndex = 0;
            lblFps.Text = "⚡ FPS: 0";

            // 
            // lblBandwidth
            // 
            lblBandwidth.AutoSize = true;
            lblBandwidth.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblBandwidth.ForeColor = System.Drawing.Color.FromArgb(249, 226, 175);
            lblBandwidth.Location = new System.Drawing.Point(130, 22);
            lblBandwidth.Name = "lblBandwidth";
            lblBandwidth.Size = new System.Drawing.Size(126, 17);
            lblBandwidth.TabIndex = 1;
            lblBandwidth.Text = "📊 Tốc độ: 0 KB/s";

            // 
            // lblResolution
            // 
            lblResolution.AutoSize = true;
            lblResolution.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblResolution.ForeColor = System.Drawing.Color.FromArgb(217, 224, 238);
            lblResolution.Location = new System.Drawing.Point(10, 50);
            lblResolution.Name = "lblResolution";
            lblResolution.Size = new System.Drawing.Size(130, 15);
            lblResolution.TabIndex = 2;
            lblResolution.Text = "Độ phân giải thực: ---";

            // 
            // lblLogHeader
            // 
            lblLogHeader.AutoSize = true;
            lblLogHeader.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblLogHeader.ForeColor = System.Drawing.Color.FromArgb(186, 194, 222);
            lblLogHeader.Location = new System.Drawing.Point(12, 545);
            lblLogHeader.Name = "lblLogHeader";
            lblLogHeader.Size = new System.Drawing.Size(122, 15);
            lblLogHeader.TabIndex = 4;
            lblLogHeader.Text = "Nhật ký hệ thống:";

            // 
            // txtLog
            // 
            txtLog.BackColor = System.Drawing.Color.FromArgb(24, 24, 37);
            txtLog.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtLog.Font = new System.Drawing.Font("Consolas", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtLog.ForeColor = System.Drawing.Color.FromArgb(205, 214, 244);
            txtLog.Location = new System.Drawing.Point(12, 565);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtLog.Size = new System.Drawing.Size(306, 75);
            txtLog.TabIndex = 5;

            // 
            // pnlMain
            // 
            pnlMain.BackColor = System.Drawing.Color.FromArgb(15, 15, 25);
            pnlMain.Controls.Add(pbScreen);
            pnlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlMain.Location = new System.Drawing.Point(330, 65);
            pnlMain.Name = "pnlMain";
            pnlMain.Padding = new System.Windows.Forms.Padding(5);
            pnlMain.Size = new System.Drawing.Size(950, 655);
            pnlMain.TabIndex = 2;

            // 
            // pbScreen
            // 
            pbScreen.BackColor = System.Drawing.Color.FromArgb(15, 15, 25);
            pbScreen.Dock = System.Windows.Forms.DockStyle.Fill;
            pbScreen.Location = new System.Drawing.Point(5, 5);
            pbScreen.Name = "pbScreen";
            pbScreen.Size = new System.Drawing.Size(940, 645);
            pbScreen.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            pbScreen.TabIndex = 0;
            pbScreen.TabStop = false;
            pbScreen.MouseDown += PbScreen_MouseDown;
            pbScreen.MouseUp += PbScreen_MouseUp;
            pbScreen.MouseMove += PbScreen_MouseMove;
            pbScreen.MouseDoubleClick += PbScreen_MouseDoubleClick;
            pbScreen.MouseWheel += PbScreen_MouseWheel;

            // 
            // MainForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(30, 30, 46);
            ClientSize = new System.Drawing.Size(1280, 720);
            Controls.Add(pnlMain);
            Controls.Add(pnlSidebar);
            Controls.Add(pnlHeader);
            DoubleBuffered = true;
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            KeyPreview = true;
            MinimumSize = new System.Drawing.Size(1024, 600);
            Name = "MainForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Remote Desktop Mini - Server (.NET 9 WinForms)";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            KeyDown += MainForm_KeyDown;
            KeyUp += MainForm_KeyUp;

            pnlHeader.ResumeLayout(false);
            pnlHeader.PerformLayout();
            pnlSidebar.ResumeLayout(false);
            pnlSidebar.PerformLayout();
            grpConnection.ResumeLayout(false);
            grpConnection.PerformLayout();
            grpStream.ResumeLayout(false);
            grpStream.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trackQuality).EndInit();
            grpControl.ResumeLayout(false);
            grpStats.ResumeLayout(false);
            grpStats.PerformLayout();
            pnlMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pbScreen).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblHeaderTitle;
        private System.Windows.Forms.Label lblHeaderSub;
        private System.Windows.Forms.Label lblStatusBadge;

        private System.Windows.Forms.Panel pnlSidebar;
        private System.Windows.Forms.GroupBox grpConnection;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Label lblPin;
        private System.Windows.Forms.TextBox txtPin;
        private System.Windows.Forms.Button btnStartServer;
        private System.Windows.Forms.Label lblClientStatus;

        private System.Windows.Forms.GroupBox grpStream;
        private System.Windows.Forms.Label lblQuality;
        private System.Windows.Forms.Label lblQualityValue;
        private System.Windows.Forms.TrackBar trackQuality;
        private System.Windows.Forms.Label lblScale;
        private System.Windows.Forms.ComboBox cmbScale;
        private System.Windows.Forms.Label lblFpsCap;
        private System.Windows.Forms.ComboBox cmbFps;
        private System.Windows.Forms.Button btnApplyConfig;

        private System.Windows.Forms.GroupBox grpControl;
        private System.Windows.Forms.Button btnToggleControl;
        private System.Windows.Forms.Button btnScreenshot;

        private System.Windows.Forms.GroupBox grpStats;
        private System.Windows.Forms.Label lblFps;
        private System.Windows.Forms.Label lblBandwidth;
        private System.Windows.Forms.Label lblResolution;

        private System.Windows.Forms.Label lblLogHeader;
        private System.Windows.Forms.TextBox txtLog;

        private System.Windows.Forms.Panel pnlMain;
        private System.Windows.Forms.PictureBox pbScreen;
    }
}
