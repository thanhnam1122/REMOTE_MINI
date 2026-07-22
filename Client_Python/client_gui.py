import tkinter as tk
from tkinter import ttk, messagebox, scrolledtext
import socket
import threading
import time
from network_client import NetworkClient

class ClientGUI:
    def __init__(self, root):
        self.root = root
        self.root.title("Remote Desktop Mini - Python Client (Máy Bị Điều Khiển)")
        self.root.geometry("640x600")
        self.root.resizable(False, False)
        self.root.configure(bg="#1E1E2E")  # Modern Dark Theme

        # Apply custom TTK styles
        self.style = ttk.Style()
        self.style.theme_use("clam")
        self._configure_styles()

        # State Variables
        self.client = None
        self.is_connected = False
        self.local_ip = self._get_local_ip()

        # UI Components
        self._create_header()
        self._create_connection_card()
        self._create_stat_card()
        self._create_log_area()

    def _configure_styles(self):
        self.style.configure("TFrame", background="#1E1E2E")
        self.style.configure("Card.TFrame", background="#2A2A3D", relief="flat")
        self.style.configure("TLabel", background="#1E1E2E", foreground="#D9E0EE", font=("Segoe UI", 10))
        self.style.configure("Card.TLabel", background="#2A2A3D", foreground="#D9E0EE", font=("Segoe UI", 10))
        self.style.configure("Header.TLabel", background="#1E1E2E", foreground="#89B4FA", font=("Segoe UI", 16, "bold"))
        self.style.configure("SubHeader.TLabel", background="#1E1E2E", foreground="#BAC2DE", font=("Segoe UI", 10, "italic"))
        self.style.configure("StatusOff.TLabel", background="#2A2A3D", foreground="#F38BA8", font=("Segoe UI", 11, "bold"))
        self.style.configure("StatusOn.TLabel", background="#2A2A3D", foreground="#A6E3A1", font=("Segoe UI", 11, "bold"))

        self.style.configure("Connect.TButton", font=("Segoe UI", 11, "bold"), foreground="#FFFFFF", background="#89B4FA")
        self.style.map("Connect.TButton", background=[("active", "#74C7EC"), ("disabled", "#45475A")])

        self.style.configure("Disconnect.TButton", font=("Segoe UI", 11, "bold"), foreground="#FFFFFF", background="#F38BA8")
        self.style.map("Disconnect.TButton", background=[("active", "#EBA0AC")])

    def _get_local_ip(self):
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            s.connect(("8.8.8.8", 80))
            ip = s.getsockname()[0]
            s.close()
            return ip
        except Exception:
            return "127.0.0.1"

    def _create_header(self):
        header_frame = ttk.Frame(self.root)
        header_frame.pack(fill="x", padx=20, pady=(15, 10))

        title_lbl = ttk.Label(header_frame, text="REMOTE DESKTOP MINI", style="Header.TLabel")
        title_lbl.pack(anchor="w")

        subtitle_lbl = ttk.Label(header_frame, text=f"Client (Máy bị điều khiển) | IP local của máy này: {self.local_ip}", style="SubHeader.TLabel")
        subtitle_lbl.pack(anchor="w", pady=(2, 0))

    def _create_connection_card(self):
        card = ttk.Frame(self.root, style="Card.TFrame", padding=15)
        card.pack(fill="x", padx=20, pady=10)

        card_title = ttk.Label(card, text="CẤU HÌNH KẾT NỐI TỚI SERVER", font=("Segoe UI", 12, "bold"), style="Card.TLabel", foreground="#89B4FA")
        card_title.grid(row=0, column=0, columnspan=4, sticky="w", pady=(0, 10))

        # Server IP
        ttk.Label(card, text="IP Server:", style="Card.TLabel").grid(row=1, column=0, sticky="w", pady=5)
        self.ip_entry = ttk.Entry(card, font=("Segoe UI", 10), width=18)
        self.ip_entry.insert(0, "127.0.0.1")
        self.ip_entry.grid(row=1, column=1, sticky="w", padx=(5, 15), pady=5)

        # Server Port
        ttk.Label(card, text="Cổng (Port):", style="Card.TLabel").grid(row=1, column=2, sticky="w", pady=5)
        self.port_entry = ttk.Entry(card, font=("Segoe UI", 10), width=8)
        self.port_entry.insert(0, "8888")
        self.port_entry.grid(row=1, column=3, sticky="w", padx=(5, 0), pady=5)

        # Passcode PIN
        ttk.Label(card, text="Mã PIN Bảo vệ:", style="Card.TLabel").grid(row=2, column=0, sticky="w", pady=5)
        self.pin_entry = ttk.Entry(card, font=("Segoe UI", 10, "bold"), width=18)
        self.pin_entry.insert(0, "1234")
        self.pin_entry.grid(row=2, column=1, sticky="w", padx=(5, 15), pady=5)

        # Status Badge
        ttk.Label(card, text="Trạng thái:", style="Card.TLabel").grid(row=2, column=2, sticky="w", pady=5)
        self.status_lbl = ttk.Label(card, text="ĐÃ NGẮT KẾT NỐI", style="StatusOff.TLabel")
        self.status_lbl.grid(row=2, column=3, sticky="w", padx=(5, 0), pady=5)

        # Action Buttons
        btn_frame = ttk.Frame(card, style="Card.TFrame")
        btn_frame.grid(row=3, column=0, columnspan=4, sticky="ew", pady=(15, 0))

        self.btn_toggle = ttk.Button(btn_frame, text="START STREAMING", style="Connect.TButton", command=self.toggle_connection)
        self.btn_toggle.pack(fill="x", ipady=4)

    def _create_stat_card(self):
        stat_frame = ttk.Frame(self.root, style="Card.TFrame", padding=10)
        stat_frame.pack(fill="x", padx=20, pady=5)

        self.fps_var = tk.StringVar(value="FPS: 0")
        self.bandwidth_var = tk.StringVar(value="Tốc độ: 0 KB/s")
        self.res_var = tk.StringVar(value="Độ phân giải: ---")

        lbl_fps = ttk.Label(stat_frame, textvariable=self.fps_var, style="Card.TLabel", font=("Segoe UI", 10, "bold"), foreground="#A6E3A1")
        lbl_fps.pack(side="left", padx=15)

        lbl_bw = ttk.Label(stat_frame, textvariable=self.bandwidth_var, style="Card.TLabel", font=("Segoe UI", 10, "bold"), foreground="#F9E2AF")
        lbl_bw.pack(side="left", padx=15)

        lbl_res = ttk.Label(stat_frame, textvariable=self.res_var, style="Card.TLabel", font=("Segoe UI", 10))
        lbl_res.pack(side="right", padx=15)

    def _create_log_area(self):
        log_frame = ttk.Frame(self.root)
        log_frame.pack(fill="both", expand=True, padx=20, pady=(10, 15))

        lbl_log = ttk.Label(log_frame, text="Nhật ký hoạt động (System Log):", font=("Segoe UI", 10, "bold"), foreground="#BAC2DE")
        lbl_log.pack(anchor="w", pady=(0, 5))

        self.log_box = scrolledtext.ScrolledText(log_frame, wrap="word", font=("Consolas", 9), bg="#181825", fg="#CDD6F4", insertbackground="#CDD6F4")
        self.log_box.pack(fill="both", expand=True)

        self.log("Hệ thống đã sẵn sàng. Vui lòng nhập IP Server và nhấn START STREAMING.")

    def log(self, text):
        def _update():
            timestamp = time.strftime("[%H:%M:%S]")
            self.log_box.insert("end", f"{timestamp} {text}\n")
            self.log_box.see("end")
        self.root.after(0, _update)

    def update_stats(self, fps, kbps, w, h):
        def _update():
            self.fps_var.set(f"FPS: {fps:.1f}")
            self.bandwidth_var.set(f"Tốc độ: {kbps:.1f} KB/s")
            self.res_var.set(f"Độ phân giải: {w}x{h}")
        self.root.after(0, _update)

    def toggle_connection(self):
        if not self.is_connected:
            host = self.ip_entry.get().strip()
            port = self.port_entry.get().strip()
            pin = self.pin_entry.get().strip()

            if not host or not port:
                messagebox.showerror("Lỗi", "Vui lòng nhập IP và Port hợp lệ!")
                return

            self.client = NetworkClient(host=host, port=port, pin=pin, log_callback=self.log, stat_callback=self.update_stats)
            self.client.start(host, port, pin)

            self.is_connected = True
            self.status_lbl.config(text="ĐÃ KẾT NỐI", style="StatusOn.TLabel")
            self.btn_toggle.config(text="STOP STREAMING", style="Disconnect.TButton")
            self.ip_entry.config(state="disabled")
            self.port_entry.config(state="disabled")
            self.pin_entry.config(state="disabled")
        else:
            if self.client:
                self.client.stop()
                self.client = None

            self.is_connected = False
            self.status_lbl.config(text="ĐÃ NGẮT KẾT NỐI", style="StatusOff.TLabel")
            self.btn_toggle.config(text="START STREAMING", style="Connect.TButton")
            self.ip_entry.config(state="normal")
            self.port_entry.config(state="normal")
            self.pin_entry.config(state="normal")

            self.fps_var.set("FPS: 0")
            self.bandwidth_var.set("Tốc độ: 0 KB/s")
            self.res_var.set("Độ phân giải: ---")
