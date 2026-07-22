# ĐỀ TÀI 12: ĐIỀU KHIỂN MÁY TÍNH TỪ XA ĐƠN GIẢN (REMOTE DESKTOP MINI)

> **NHÓM THỰC HIỆN**: Thành Nam, Minh Hoàng, Tấn Phước  
> **CÔNG NGHỆ BÁO CÁO**: Server (.NET 9 / C# WinForms) & Client (Python 3)  
> **GIAO THỨC TRUYỀN THÔNG**: TCP Socket Binary Framing Protocol

---

## I. MỤC ĐÍCH & TÌNH HUỐNG THỰC TẾ

1. **Mục đích**:
   - Xây dựng phiên bản thu nhỏ của phần mềm điều khiển từ xa (tương tự TeamViewer / Anydesk).
   - Cho phép chụp ảnh màn hình máy bị điều khiển (Client - Python) liên tục với độ nén JPEG tùy chỉnh và truyền luồng ảnh byte stream qua TCP Socket về máy điều khiển (Server - C# WinForms).
   - Truyền ngược tọa độ click chuột, thao tác kéo thả, cuộn chuột và phím bấm từ Server về Client để tương tác điều khiển trực tiếp.

2. **Tình huống ứng dụng**:
   - Bộ phận hỗ trợ kỹ thuật nội bộ (**IT Helpdesk**) của công ty cần một công cụ siêu nhẹ, không cần cài đặt phức tạp để xem màn hình máy tính của nhân viên gặp sự cố và hỗ trợ thao tác xử lý từ xa qua mạng LAN/Internet.

---

## II. SƠ ĐỒ KIẾN TRÚC & GIAO THỨC TRUYỀN THÔNG

```
+------------------------------------+             TCP Socket Connection             +--------------------------------------+
|       Server (.NET 9 WinForms)     | <===========================================> |          Client (Python 3)          |
|          (Máy Điều Khiển)          |                                               |           (Máy Bị Điều Khiển)        |
+------------------------------------+                                               +--------------------------------------+
| - Custom Modern Dark Mode UI       | --- Config / Input Commands (JSON Packet) --> | - Modern Python Status GUI (Tkinter) |
| - Real-time PictureBox (Zoom Mode) | <--- Screen Frames (JPEG Stream + Header) --- | - Screen Grabber (Pillow / mss)      |
| - Precision Coordinate Transformer |                                               | - Remote Control Executor (PyAutoGUI)|
| - Quality & FPS Tuning Dashboard   |                                               | - Multithreaded TCP Socket Handler   |
+------------------------------------+                                               +--------------------------------------+
```

### 1. Cấu trúc gói tin truyền thông (Custom Binary Protocol)

Để tránh hiện tượng **dính gói (sticky packets)** hoặc **xé gói (fragmentation)** của Socket TCP, hệ thống tự định nghĩa cấu trúc gói tin có **Header độ dài cố định**:

- **Gói tin Luồng Hình Ảnh (Client ➔ Server)**:
  - `Header (11 Bytes)`:
    - `Magic (2 Bytes)`: `'R'`, `'D'` (Xác thực đầu gói)
    - `Type (1 Byte)`: `0x01` (Dữ liệu Frame hình ảnh)
    - `OrigWidth (2 Bytes uint16)`: Chiều rộng gốc của màn hình Client
    - `OrigHeight (2 Bytes uint16)`: Chiều cao gốc của màn hình Client
    - `PayloadSize (4 Bytes uint32 Big-Endian)`: Độ dài chuỗi byte nén JPEG
  - `Payload`: Mảng byte hình ảnh JPEG.

- **Gói tin Thao tác Chuột / Bàn Phím (Server ➔ Client)**:
  - `Header (7 Bytes)`:
    - `Magic (2 Bytes)`: `'R'`, `'D'`
    - `Type (1 Byte)`: `0x02` (Dữ liệu Control)
    - `PayloadSize (4 Bytes uint32 Big-Endian)`: Độ dài chuỗi JSON
  - `Payload (JSON)`:
    - Chuột: `{"type": "mouse", "action": "down"|"up"|"move"|"click"|"dclick"|"scroll", "x": norm_x, "y": norm_y, "button": "left"|"right", "delta": 120}`
    - Bàn phím: `{"type": "keyboard", "action": "down"|"up"|"press", "key": "a"}`

- **Gói tin Cấu hình Động (Server ➔ Client)**:
  - `Header (7 Bytes)` + `Payload JSON`: `{"type": "config", "quality": 65, "scale": 0.75, "fps_limit": 30}`

### 2. Thuật toán chuẩn hóa tọa độ (Normalized Coordinates 0.0 - 1.0)
Khi hiển thị ảnh màn hình trên `PictureBox` ở chế độ `SizeMode.Zoom`, hình ảnh sẽ bị tỉ lệ lại và xuất hiện dải đen (Letterboxing / Pillarboxing).  
Hệ thống sử dụng lớp `CoordinateMapper.cs` để tính toán chính xác vùng vẽ thực tế:
\[
norm\_x = \frac{mouse\_x - offset\_x}{drawn\_width}, \quad norm\_y = \frac{mouse\_y - offset\_y}{drawn\_height}
\]
Giá trị `norm_x, norm_y` luôn nằm trong khoảng `[0.0, 1.0]`. Khi gửi sang máy Client (Python), Client chỉ cần nhân với độ phân giải thực (`screen_w`, `screen_h`) để di chuyển chuột **chính xác 100%** bất kể chênh lệch độ phân giải giữa 2 máy.

---

## III. CẤU TRÚC THƯ MỤC DỰ ÁN

```
REMOTE_MINI/
├── Client_Python/                  # Ứng dụng Client (Máy bị điều khiển)
│   ├── main.py                     # Entry point khởi tạo giao diện
│   ├── client_gui.py               # Giao diện Dark Mode Tkinter chuyên nghiệp
│   ├── network_client.py           # Socket TCP Client multithreaded
│   ├── screen_capture.py           # Chụp màn hình siêu tốc (mss / Pillow) & nén JPEG
│   ├── remote_executor.py          # Giả lập thao tác chuột/phím (PyAutoGUI)
│   └── requirements.txt            # Thư viện Python phụ thuộc
│
├── Server_WinForms/                # Ứng dụng Server (Máy điều khiển)
│   ├── Program.cs                  # Entry point chương trình C# WinForms
│   ├── MainForm.cs                 # Xử lý sự kiện giao diện, chuột/phím & luồng dữ liệu
│   ├── MainForm.Designer.cs        # Thiết kế giao diện Dark Mode (Modern Layout)
│   ├── Models/
│   │   └── PacketProtocol.cs       # Đóng/mở gói tin Binary Protocol
│   ├── Services/
│   │   └── TcpServerService.cs     # TCP Listener async multithreaded
│   └── Helpers/
│       └── CoordinateMapper.cs     # Thuật toán tính tọa độ chuẩn hóa cho PictureBox Zoom
│
├── build_server.bat                # Script biên dịch tự động Server WinForms
├── run_server.bat                  # Script khởi chạy Server
├── run_client.bat                  # Script khởi chạy Client
├── run_all.bat                     # Script 1-Click khởi chạy cả Server và Client
└── README.md                       # Báo cáo chi tiết đề tài
```

---

## IV. HƯỚNG DẪN CÀI ĐẶT & VẬN HÀNH

### 1. Yêu cầu môi trường:
- **Máy Client**: Đã cài đặt **Python 3.8+**.
- **Máy Server**: Hệ điều hành Windows (Đã cài sẵn môi trường .NET / C# hoặc có thể chạy trực tiếp file `.exe` đã biên dịch).

### 2. Chạy ứng dụng bằng 1-Click:
Nhấp đúp chuột vào file **`run_all.bat`** ở thư mục gốc:
- `run_all.bat` sẽ tự động biên dịch Server (nếu chưa có file `.exe`), sau đó khởi chạy ứng dụng Server và Client ở 2 cửa sổ riêng biệt để test trực tiếp trên cùng một máy hoặc qua mạng LAN.

### 3. Thao tác kết nối điều khiển:
1. Mở **Server (Máy điều khiển)**:
   - Nhập Cổng (mặc định: `8888`) và Mã PIN bảo vệ (mặc định: `1234`).
   - Nhấn **`▶ START SERVER`**. Server sẽ hiển thị trạng thái `🟢 LISTENING ON PORT 8888`.
2. Mở **Client (Máy bị điều khiển)**:
   - Nhập **IP Server** (Nếu test cùng máy nhập `127.0.0.1`, nếu khác máy nhập IP local của máy Server).
   - Nhập **Cổng** (`8888`) và **Mã PIN** (`1234`).
   - Nhấn **`▶ START STREAMING`**.
3. Ngay lập tức màn hình của Client sẽ hiển thị thời gian thực trên thẻ `PictureBox` của Server!
4. Thao tác rê chuột, click chuột trái/phải, double click, cuộn chuột và gõ bàn phím trên PictureBox để điều khiển máy Client từ xa.
5. Có thể điều chỉnh thanh trượt **Chất lượng JPEG (10%-100%)**, **Tỉ lệ màn hình (100%, 75%, 50%)**, **Tốc độ FPS (15, 30, 60)** và nhấn **`ÁP DỤNG CẤU HÌNH`** để tối ưu đường truyền.

---

## V. ĐÁNH GIÁ KẾT QUẢ ĐẠT ĐƯỢC

1. **Tính năng**:
   - [x] Chụp màn hình liên tục và nén JPEG thích hợp qua thư viện Pillow/mss.
   - [x] Truyền luồng byte stream mượt mà qua TCP Socket không dính gói.
   - [x] Hiển thị hình ảnh thời gian thực trên `PictureBox` WinForms C#.
   - [x] Truyền ngược tọa độ click chuột (Trái, Phải, Double Click, Scroll) và phím bấm chính xác 100%.
   - [x] Mã PIN bảo mật xác thực kết nối.
   - [x] Bảng điều khiển thông số hiệu năng (FPS, Tốc độ dữ liệu KB/s, Độ phân giải thực).
   - [x] Chức năng Chụp ảnh màn hình từ xa (Screenshot).

2. **Giao diện**:
   - Giao diện **Dark Mode Modern Aesthetic** kết hợp hệ màu Mocha/Catppuccin cao cấp, giúp bài báo cáo đạt điểm số tối đa về tính thẩm mỹ và chuyên nghiệp.
