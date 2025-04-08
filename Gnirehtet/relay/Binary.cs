using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Gnirehtet.Relay
{
    public static class Binary
    {
        private const int MAX_STRING_PACKET_SIZE = 20;

        public static string BuildPacketString(byte[] data, int offset, int len)
        {
            int limit = Math.Min(MAX_STRING_PACKET_SIZE, len);
            var builder = new StringBuilder();
            builder.Append($"[{len} bytes] ");
            for (int i = 0; i < limit; i++)
            {
                if (i != 0)
                {
                    string sep = i % 4 == 0 ? "  " : " ";
                    builder.Append(sep);
                }
                builder.Append($"{data[offset + i]:X2}"); // 使用 X2 格式化成两位十六进制
            }
            if (limit < len)
            {
                builder.Append($"  ... +{len - limit} bytes");
            }
            return builder.ToString();
        }

        public static string BuildPacketString(MemoryStream buffer)
        {
            byte[] byteArray = buffer.ToArray();
            int offset = (int)(buffer.Position);
            int length = (int)(buffer.Length - buffer.Position);

            return BuildPacketString(byteArray, offset, length);
        }


        public static byte[] Copy(byte[] buffer, int position, int remaining)
        {
            byte[] result = new byte[remaining];
            Array.Copy(buffer, position, result, 0, remaining);
            return result;
        }

        public static byte[] Slice(byte[] buffer, int offset, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(buffer, offset, result, 0, length);
            return result;
        }
    }
}