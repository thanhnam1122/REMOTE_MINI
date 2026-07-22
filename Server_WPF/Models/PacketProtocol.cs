#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RemoteDesktopServer.Models
{
    public static class PacketProtocol
    {
        public static readonly byte[] MAGIC = new byte[] { (byte)'R', (byte)'D' };
        public const byte PKT_TYPE_HANDSHAKE = 0x00;
        public const byte PKT_TYPE_FRAME = 0x01;
        public const byte PKT_TYPE_CONTROL = 0x02;
        public const byte PKT_TYPE_CONFIG = 0x03;
        public const byte PKT_TYPE_TILE_FRAME = 0x04;

        public static byte[] CreateMousePacket(string action, float x, float y, string button = "left", int delta = 0)
        {
            string json = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"type\":\"mouse\",\"action\":\"{0}\",\"x\":{1:F4},\"y\":{2:F4},\"button\":\"{3}\",\"delta\":{4}}}",
                action, x, y, button, delta
            );
            return WrapPayload(PKT_TYPE_CONTROL, json);
        }

        public static byte[] CreateKeyboardPacket(string action, string key)
        {
            string json = string.Format(
                "{{\"type\":\"keyboard\",\"action\":\"{0}\",\"key\":\"{1}\"}}",
                action, EscapeJson(key)
            );
            return WrapPayload(PKT_TYPE_CONTROL, json);
        }

        public static byte[] CreateConfigPacket(int quality, double scale, int fpsLimit)
        {
            string json = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"type\":\"config\",\"quality\":{0},\"scale\":{1:F2},\"fps_limit\":{2}}}",
                quality, scale, fpsLimit
            );
            return WrapPayload(PKT_TYPE_CONFIG, json);
        }

        private static byte[] WrapPayload(byte pktType, string json)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(json);

            // Header: Magic (2B) + Type (1B) + PayloadLen (4B Big-Endian) = 7 Bytes
            byte[] header = new byte[7];
            header[0] = MAGIC[0];
            header[1] = MAGIC[1];
            header[2] = pktType;

            byte[] lenBytes = BitConverter.GetBytes((uint)payloadBytes.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lenBytes);
            }
            Array.Copy(lenBytes, 0, header, 3, 4);

            byte[] packet = new byte[header.Length + payloadBytes.Length];
            Array.Copy(header, 0, packet, 0, header.Length);
            Array.Copy(payloadBytes, 0, packet, header.Length, payloadBytes.Length);

            return packet;
        }

        public static HandshakeInfo ParseHandshake(string json)
        {
            var info = new HandshakeInfo();
            Match matchPin = Regex.Match(json, "\"pin\"\\s*:\\s*\"([^\"]+)\"");
            if (matchPin.Success)
            {
                info.Pin = matchPin.Groups[1].Value;
            }

            Match matchName = Regex.Match(json, "\"client_name\"\\s*:\\s*\"([^\"]+)\"");
            if (matchName.Success)
            {
                info.ClientName = matchName.Groups[1].Value;
            }

            return info;
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }

    public class HandshakeInfo
    {
        public string Pin { get; set; } = "";
        public string ClientName { get; set; } = "";
    }

    public class TileEntry
    {
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public byte[] JpegBytes { get; set; } = Array.Empty<byte>();
    }
}
