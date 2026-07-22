import pyautogui
import threading
import sys

# Disable pyautogui safety failsafe to prevent unintended crashes during remote control
pyautogui.FAILSAFE = False
pyautogui.PAUSE = 0.001

class RemoteExecutor:
    def __init__(self):
        # Cache primary screen width and height
        self.screen_w, self.screen_h = pyautogui.size()
        print(f"[RemoteExecutor] Initialized. Target Screen Resolution: {self.screen_w}x{self.screen_h}")

    def execute_command(self, cmd_dict):
        """
        Parses JSON control payload and executes corresponding PyAutoGUI action.
        """
        try:
            cmd_type = cmd_dict.get("type")
            if cmd_type == "mouse":
                self._handle_mouse(cmd_dict)
            elif cmd_type == "keyboard":
                self._handle_keyboard(cmd_dict)
        except Exception as e:
            print(f"[RemoteExecutor] Error executing command {cmd_dict}: {e}")

    def _handle_mouse(self, data):
        action = data.get("action")
        norm_x = data.get("x", 0.0)
        norm_y = data.get("y", 0.0)
        button = data.get("button", "left")

        # Convert normalized float coordinates (0.0 to 1.0) to absolute monitor pixels
        target_x = int(norm_x * self.screen_w)
        target_y = int(norm_y * self.screen_h)
        target_x = max(0, min(self.screen_w - 1, target_x))
        target_y = max(0, min(self.screen_h - 1, target_y))

        if action == "move":
            pyautogui.moveTo(target_x, target_y)
        elif action == "down":
            pyautogui.moveTo(target_x, target_y)
            pyautogui.mouseDown(button=button)
        elif action == "up":
            pyautogui.moveTo(target_x, target_y)
            pyautogui.mouseUp(button=button)
        elif action == "click":
            pyautogui.moveTo(target_x, target_y)
            pyautogui.click(button=button)
        elif action == "rclick":
            pyautogui.moveTo(target_x, target_y)
            pyautogui.rightClick()
        elif action == "dclick":
            pyautogui.moveTo(target_x, target_y)
            pyautogui.doubleClick(button=button)
        elif action == "scroll":
            delta = data.get("delta", 0)
            pyautogui.moveTo(target_x, target_y)
            # PyAutoGUI scroll amount positive = up, negative = down
            pyautogui.scroll(delta)

    def _handle_keyboard(self, data):
        action = data.get("action")
        key = data.get("key", "").lower()
        if not key:
            return

        # Map common C# Key names to PyAutoGUI key names if necessary
        key_map = {
            "return": "enter",
            "back": "backspace",
            "capital": "capslock",
            "oemperiod": ".",
            "oemcomma": ",",
            "oemminus": "-",
            "oemplus": "=",
            "space": "space",
            "escape": "esc",
        }
        key = key_map.get(key, key)

        if action == "down":
            pyautogui.keyDown(key)
        elif action == "up":
            pyautogui.keyUp(key)
        elif action == "press":
            pyautogui.press(key)
        elif action == "write":
            text = data.get("text", "")
            if text:
                pyautogui.write(text)
