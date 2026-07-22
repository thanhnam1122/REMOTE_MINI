import socket
import struct
import json
import time
import threading
from screen_capture import ScreenCapturer
from remote_executor import RemoteExecutor

# Protocol Constants
MAGIC_HEADER = b'RD'
PKT_TYPE_FRAME = 0x01
PKT_TYPE_CONTROL = 0x02
PKT_TYPE_CONFIG = 0x03

class NetworkClient:
    def __init__(self, host='127.0.0.1', port=8888, pin="1234", log_callback=None, stat_callback=None):
        self.host = host
        self.port = port
        self.pin = pin
        self.log_callback = log_callback
        self.stat_callback = stat_callback

        self.socket = None
        self.is_running = False
        self.capturer = ScreenCapturer(quality=65, scale=0.75)
        self.executor = RemoteExecutor()

        self.fps_limit = 30
        self.total_bytes_sent = 0
        self.frames_sent = 0

        self.send_thread = None
        self.recv_thread = None

    def log(self, message):
        print(f"[NetworkClient] {message}")
        if self.log_callback:
            self.log_callback(message)

    def start(self, host, port, pin):
        if self.is_running:
            self.stop()

        self.host = host
        self.port = int(port)
        self.pin = pin
        self.is_running = True

        self.send_thread = threading.Thread(target=self._connection_loop, daemon=True)
        self.send_thread.start()

    def stop(self):
        self.is_running = False
        sock = self.socket
        self.socket = None
        if sock:
            try:
                sock.shutdown(socket.SHUT_RDWR)
            except Exception:
                pass
            try:
                sock.close()
            except Exception:
                pass
        self.log("Đã ngắt kết nối khỏi Server.")

    def _connection_loop(self):
        while self.is_running:
            try:
                self.log(f"Đang kết nối tới Server {self.host}:{self.port}...")
                sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                sock.connect((self.host, self.port))
                self.socket = sock
                self.log(">> Kết nối TCP thành công! Đang gửi thông tin Handshake...")

                # Send Handshake Auth Packet: JSON with PIN
                handshake_payload = json.dumps({"pin": self.pin, "client_name": "PythonClient"}).encode('utf-8')
                hs_header = MAGIC_HEADER + struct.pack('>BI', 0x00, len(handshake_payload))
                sock.sendall(hs_header + handshake_payload)

                # Start Receiver thread for remote control inputs from Server
                self.recv_thread = threading.Thread(target=self._receiver_loop, daemon=True)
                self.recv_thread.start()

                # Start Frame Streamer
                self._stream_frames()

            except Exception as e:
                if self.is_running:
                    self.log(f"Lỗi kết nối / Truyền dữ liệu: {e}")
                if self.socket:
                    try:
                        self.socket.close()
                    except Exception:
                        pass
                    self.socket = None

                if self.is_running:
                    self.log("Thử lại kết nối sau 3 giây...")
                    time.sleep(3)

    def _stream_frames(self):
        last_stat_time = time.time()
        frame_count = 0
        bytes_count = 0

        while self.is_running:
            sock = self.socket
            if not sock:
                break

            start_time = time.time()

            jpeg_bytes, orig_w, orig_h = self.capturer.capture_frame()
            if not jpeg_bytes or not self.is_running:
                time.sleep(0.01)
                continue

            payload_len = len(jpeg_bytes)
            # Binary Header: Magic (2B) + Type (1B: 0x01) + OrigW (2B) + OrigH (2B) + PayloadLen (4B) = 11 Bytes
            header = MAGIC_HEADER + struct.pack('>BHH I', PKT_TYPE_FRAME, orig_w, orig_h, payload_len)

            try:
                sock.sendall(header + jpeg_bytes)
                frame_count += 1
                bytes_count += len(header) + payload_len
                self.total_bytes_sent += len(header) + payload_len
            except Exception as e:
                if self.is_running:
                    self.log(f"Lỗi gửi frame: {e}")
                break

            # Calculate FPS and data rate stats
            now = time.time()
            if now - last_stat_time >= 1.0:
                elapsed = now - last_stat_time
                current_fps = frame_count / elapsed
                kbps = (bytes_count / 1024) / elapsed
                if self.stat_callback:
                    self.stat_callback(current_fps, kbps, orig_w, orig_h)
                frame_count = 0
                bytes_count = 0
                last_stat_time = now

            # Frame rate cap
            target_delay = 1.0 / self.fps_limit
            elapsed_frame = time.time() - start_time
            if elapsed_frame < target_delay:
                time.sleep(target_delay - elapsed_frame)

    def _receiver_loop(self):
        """
        Receives control packets (mouse, keyboard, config) from Server over socket.
        Header: Magic (2B) + Type (1B) + PayloadSize (4B) = 7 Bytes
        """
        while self.is_running:
            sock = self.socket
            if not sock:
                break

            try:
                header = self._recv_exact(sock, 7)
                if not header:
                    break

                magic, pkt_type, payload_len = struct.unpack('>2s B I', header)
                if magic != MAGIC_HEADER:
                    self.log("[Lỗi Protocol] Header magic không hợp lệ!")
                    continue

                payload_bytes = self._recv_exact(sock, payload_len)
                if not payload_bytes:
                    break

                if pkt_type == PKT_TYPE_CONTROL:
                    cmd_json = payload_bytes.decode('utf-8')
                    cmd_dict = json.loads(cmd_json)
                    # Execute mouse or keyboard command
                    self.executor.execute_command(cmd_dict)

                elif pkt_type == PKT_TYPE_CONFIG:
                    cfg_json = payload_bytes.decode('utf-8')
                    cfg = json.loads(cfg_json)
                    self.log(f"Đã nhận cấu hình mới từ Server: {cfg}")
                    if "quality" in cfg or "scale" in cfg:
                        self.capturer.update_settings(quality=cfg.get("quality"), scale=cfg.get("scale"))
                    if "fps_limit" in cfg:
                        self.fps_limit = max(5, min(60, int(cfg.get("fps_limit"))))

            except Exception as e:
                if self.is_running:
                    self.log(f"Luồng nhận dữ liệu bị ngắt: {e}")
                break

    def _recv_exact(self, sock, length):
        data = b''
        while len(data) < length:
            try:
                more = sock.recv(length - len(data))
                if not more:
                    return None
                data += more
            except Exception:
                return None
        return data
