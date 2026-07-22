import io
import mss
from PIL import Image, ImageGrab

class ScreenCapturer:
    def __init__(self, quality=60, scale=0.75):
        self.quality = quality
        self.scale = scale
        self.mss_instance = None
        try:
            self.mss_instance = mss.mss()
        except Exception as e:
            print(f"[ScreenCapturer] mss initialization failed, using ImageGrab fallback: {e}")

    def capture_frame(self):
        """
        Captures the primary monitor screenshot, resizes if scale < 1.0,
        compresses to JPEG byte stream, and returns (jpeg_bytes, orig_w, orig_h).
        """
        try:
            if self.mss_instance:
                monitor = self.mss_instance.monitors[1]
                orig_w = monitor["width"]
                orig_h = monitor["height"]
                sct_img = self.mss_instance.grab(monitor)
                img = Image.frombytes("RGB", sct_img.size, sct_img.bgra, "raw", "BGRX")
            else:
                img = ImageGrab.grab()
                orig_w, orig_h = img.size
                if img.mode != "RGB":
                    img = img.convert("RGB")

            # Apply resolution scaling if requested
            if 0.1 <= self.scale < 1.0:
                new_w = max(100, int(orig_w * self.scale))
                new_h = max(100, int(orig_h * self.scale))
                img = img.resize((new_w, new_h), Image.BILINEAR)

            # Compress to JPEG
            buffer = io.BytesIO()
            img.save(buffer, format="JPEG", quality=self.quality, optimize=True)
            jpeg_bytes = buffer.getvalue()
            return jpeg_bytes, orig_w, orig_h

        except Exception as e:
            print(f"[ScreenCapturer] Error capturing screen: {e}")
            return None, 0, 0

    def update_settings(self, quality=None, scale=None):
        if quality is not None:
            self.quality = max(10, min(100, int(quality)))
        if scale is not None:
            self.scale = max(0.2, min(1.0, float(scale)))

    def close(self):
        if self.mss_instance:
            try:
                self.mss_instance.close()
            except Exception:
                pass
