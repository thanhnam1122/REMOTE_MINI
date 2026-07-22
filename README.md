# ĐỀ TÀI: ĐIỀU KHIỂN MÁY TÍNH TỪ XA ĐƠN GIẢN (REMOTE DESKTOP MINI)

> **NHÓM THỰC HIỆN**: Thành Nam, Minh Hoàng, Tấn Phước  
> **CÔNG NGHỆ BÁO CÁO**: 100% C# .NET 8 WPF (Server: WPF & Client: WPF)  
> **GIAO THỨC TRUYỀN THÔNG**: TCP Socket Binary Framing Protocol

---

## I. MỤC ĐÍCH & TÌNH HUỐNG THỰC TẾ

1. **Mục đích**:
   - Xây dựng phiên bản thu nhỏ của phần mềm điều khiển từ xa (tương tự TeamViewer / Anydesk) viết **100% bằng WPF C# .NET 8**.
   - Cho phép chụp ảnh màn hình máy bị điều khiển (**Client - WPF**) liên tục với độ nén JPEG tùy chỉnh và truyền luồng ảnh byte stream qua TCP Socket về máy điều khiển (**Server - WPF**).
   - Truyền ngược tọa độ click chuột, thao tác kéo thả, cuộn chuột và phím bấm từ Server về Client để tương tác điều khiển trực tiếp qua Win32 API (`SendInput` / `SetCursorPos`).

2. **Tình huống ứng dụng**:
   - Bộ phận hỗ trợ kỹ thuật nội bộ (**IT Helpdesk**) của công ty cần một công cụ siêu nhẹ, không phụ thuộc môi trường Python hay thư viện ngoài, xem màn hình máy tính nhân viên và hỗ trợ thao tác xử lý từ xa qua mạng LAN/Internet.

---

## II. SƠ ĐỒ KIẾN TRÚC & GIAO THỨC TRUYỀN THÔNG

```
+------------------------------------+             TCP Socket Connection             +--------------------------------------+
|          Server (.NET 8 WPF)       | <===========================================> |         Client (.NET 8 WPF)          |
|          (Máy Điều Khiển)          |                                               |           (Máy Bị Điều Khiển)        |
+------------------------------------+                                               +--------------------------------------+
| - Custom Modern WPF Dark Theme UI  | --- Config / Input Commands (JSON Packet) --> | - Modern WPF Dashboard & Controls    |
| - Interactive WPF Screen Viewport  | <--- Screen Frames (JPEG Stream + Header) --- | - GDI+ Low-Latency Screen Capturer   |
| - WpfCoordinateMapper (Uniform)    |                                               | - Remote Control Executor (Win32 API)|
| - Realtime Performance Metrics     |                                               | - Multithreaded TCP Socket Handler   |
+------------------------------------+                                               +--------------------------------------+
```

### 1. Cấu trúc gói tin truyền thông (Custom Binary Protocol)

- **Handshake Packet (Client ➔ Server)**:
  - Header: `RD` (2B) + `0x00` (1B) + PayloadSize (4B Big-Endian)
  - Payload JSON: `{"pin": "1234", "client_name": "WPFClient"}`

- **Gói tin Luồng Hình Ảnh (Client ➔ Server)**:
  - Header (11 Bytes):
    - Magic (2 Bytes): `'R'`, `'D'`
    - Type (1 Byte): `0x01` (Frame data)
    - OrigWidth (2 Bytes uint16)
    - OrigHeight (2 Bytes uint16)
    - PayloadSize (4 Bytes uint32 Big-Endian)
  - Payload: Mảng byte hình ảnh JPEG.

- **Gói tin Thao tác Chuột / Bàn Phím (Server ➔ Client)**:
  - Header (7 Bytes): Magic `'RD'` + Type `0x02` + PayloadSize (4B)
  - Payload JSON: Mouse / Keyboard command

- **Gói tin Cấu hình Động (Server ➔ Client)**:
  - Header (7 Bytes) + Payload JSON: `{"type": "config", "quality": 65, "scale": 0.75, "fps_limit": 30}`

---

## III. CẤU TRÚC THƯ MỤC DỰ ÁN

```
REMOTE_MINI/
├── Client_WPF/                     # Ứng dụng Client WPF (Máy bị điều khiển)
│   ├── App.xaml / App.xaml.cs      # Entry point & Visual Resource Dictionary
│   ├── MainWindow.xaml / .cs       # Modern Dark Mode WPF Dashboard UI
│   ├── Services/
│   │   ├── ScreenCapturer.cs       # Chụp màn hình GDI+ & nén JPEG siêu tốc
│   │   ├── RemoteExecutor.cs       # Giả lập chuột & phím bằng Win32 API
│   │   └── NetworkClientService.cs # Socket TCP Client multithreaded async
│   └── RemoteDesktopClient.csproj  # WPF Project file (.NET 8)
│
├── Server_WPF/                     # Ứng dụng Server WPF (Máy điều khiển)
│   ├── App.xaml / App.xaml.cs      # Entry point & Visual Resource Dictionary
│   ├── MainWindow.xaml / .cs       # Modern Viewport UI & Input Handler
│   ├── Models/PacketProtocol.cs    # Binary Framing Protocol
│   ├── Helpers/WpfCoordinateMapper.cs # Thuật toán tính tọa độ chuẩn hóa WPF
│   ├── Services/TcpServerService.cs# Async TCP Listener
│   └── RemoteDesktopServer.csproj  # WPF Project file (.NET 8)
│
├── build_client.bat                # Script biên dịch Client WPF
├── build_server.bat                # Script biên dịch Server WPF
├── run_server.bat                  # Script khởi chạy Server WPF
├── run_client.bat                  # Script khởi chạy Client WPF
├── run_all.bat                     # Script khởi chạy cả 2 ứng dụng WPF
├── r.ps1                           # PowerShell 1-Click build & restart tool
└── README.md                       # Báo cáo dự án
```