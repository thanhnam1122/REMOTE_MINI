#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace RemoteDesktopClient.Services
{
    public class RemoteExecutor
    {
        #region Win32 API Definitions

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        #endregion

        private static readonly Dictionary<string, byte> KeyMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "backspace", (byte)Keys.Back },
            { "tab", (byte)Keys.Tab },
            { "enter", (byte)Keys.Return },
            { "return", (byte)Keys.Return },
            { "shift", (byte)Keys.ShiftKey },
            { "ctrl", (byte)Keys.ControlKey },
            { "control", (byte)Keys.ControlKey },
            { "alt", (byte)Keys.Menu },
            { "pause", (byte)Keys.Pause },
            { "capslock", (byte)Keys.Capital },
            { "esc", (byte)Keys.Escape },
            { "escape", (byte)Keys.Escape },
            { "space", (byte)Keys.Space },
            { "pageup", (byte)Keys.Prior },
            { "pagedown", (byte)Keys.Next },
            { "end", (byte)Keys.End },
            { "home", (byte)Keys.Home },
            { "left", (byte)Keys.Left },
            { "up", (byte)Keys.Up },
            { "right", (byte)Keys.Right },
            { "down", (byte)Keys.Down },
            { "insert", (byte)Keys.Insert },
            { "delete", (byte)Keys.Delete },
            { "win", (byte)Keys.LWin },
            { "super", (byte)Keys.LWin },
            { "f1", (byte)Keys.F1 },
            { "f2", (byte)Keys.F2 },
            { "f3", (byte)Keys.F3 },
            { "f4", (byte)Keys.F4 },
            { "f5", (byte)Keys.F5 },
            { "f6", (byte)Keys.F6 },
            { "f7", (byte)Keys.F7 },
            { "f8", (byte)Keys.F8 },
            { "f9", (byte)Keys.F9 },
            { "f10", (byte)Keys.F10 },
            { "f11", (byte)Keys.F11 },
            { "f12", (byte)Keys.F12 },
        };

        public void ExecuteCommand(string jsonCommand)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonCommand);
                JsonElement root = doc.RootElement;

                if (!root.TryGetProperty("type", out JsonElement typeElem))
                    return;

                string type = typeElem.GetString() ?? "";

                if (type == "mouse")
                {
                    ExecuteMouse(root);
                }
                else if (type == "keyboard")
                {
                    ExecuteKeyboard(root);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RemoteExecutor Error] {ex.Message}");
            }
        }

        private void ExecuteMouse(JsonElement root)
        {
            string action = root.TryGetProperty("action", out var actElem) ? actElem.GetString() ?? "" : "";
            string button = root.TryGetProperty("button", out var btnElem) ? btnElem.GetString() ?? "left" : "left";
            double normX = root.TryGetProperty("x", out var xElem) ? xElem.GetDouble() : 0.0;
            double normY = root.TryGetProperty("y", out var yElem) ? yElem.GetDouble() : 0.0;
            int delta = root.TryGetProperty("delta", out var dElem) ? dElem.GetInt32() : 0;

            int screenW = Screen.PrimaryScreen?.Bounds.Width ?? 1920;
            int screenH = Screen.PrimaryScreen?.Bounds.Height ?? 1080;

            int targetX = (int)Math.Clamp(Math.Round(normX * screenW), 0, screenW - 1);
            int targetY = (int)Math.Clamp(Math.Round(normY * screenH), 0, screenH - 1);

            SetCursorPos(targetX, targetY);

            uint downFlag = button.ToLowerInvariant() switch
            {
                "right" => MOUSEEVENTF_RIGHTDOWN,
                "middle" => MOUSEEVENTF_MIDDLEDOWN,
                _ => MOUSEEVENTF_LEFTDOWN
            };

            uint upFlag = button.ToLowerInvariant() switch
            {
                "right" => MOUSEEVENTF_RIGHTUP,
                "middle" => MOUSEEVENTF_MIDDLEUP,
                _ => MOUSEEVENTF_LEFTUP
            };

            switch (action.ToLowerInvariant())
            {
                case "move":
                    // SetCursorPos is sufficient
                    break;

                case "down":
                    mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                    break;

                case "up":
                    mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                    break;

                case "click":
                    mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                    break;

                case "dclick":
                    mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                    break;

                case "scroll":
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, UIntPtr.Zero);
                    break;
            }
        }

        private void ExecuteKeyboard(JsonElement root)
        {
            string action = root.TryGetProperty("action", out var actElem) ? actElem.GetString() ?? "" : "";
            string key = root.TryGetProperty("key", out var keyElem) ? keyElem.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(key)) return;

            byte vk = GetVirtualKeyCode(key);
            if (vk == 0) return;

            switch (action.ToLowerInvariant())
            {
                case "down":
                    keybd_event(vk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                    break;

                case "up":
                    keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    break;

                case "press":
                    keybd_event(vk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                    keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    break;
            }
        }

        private byte GetVirtualKeyCode(string key)
        {
            if (KeyMap.TryGetValue(key, out byte vk))
            {
                return vk;
            }

            if (key.Length == 1)
            {
                char ch = key[0];
                short scan = VkKeyScan(ch);
                return (byte)(scan & 0xFF);
            }

            return 0;
        }
    }
}
