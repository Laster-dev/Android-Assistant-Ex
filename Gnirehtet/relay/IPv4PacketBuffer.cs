using System;
using System.IO;
using System.Text;

namespace Genymobile.Gnirehtet.Relay
{
    public class IPv4PacketBuffer
    {
        private readonly byte[] buffer = new byte[IPv4Packet.MAX_PACKET_LENGTH];
        private int position = 0;

        public int ReadFrom(Stream channel)
        {
            int bytesRead = channel.Read(buffer, position, buffer.Length - position);
            position += bytesRead;
            return bytesRead;
        }

        // Helper to get available packet length from the buffer
        private int GetAvailablePacketLength()
        {
            int length = IPv4Header.ReadLength(buffer);
            if (length == -1 || IPv4Header.ReadVersion(buffer) != 4)
            {
                throw new InvalidOperationException("This function must not be called when the packet is not IPv4.");
            }

            if (length == -1)
            {
                // No packet available
                return 0;
            }

            if (length > buffer.Length - position)
            {
                // No full packet available
                return 0;
            }

            return length;
        }

        public IPv4Packet AsIPv4Packet()
        {
            // Simulate ByteBuffer flip by resetting the position and adjusting the limit
            int length = GetAvailablePacketLength();
            if (length == 0)
            {
                // No packet available, compact buffer (actually just reset in C#)
                Compact();
                return null;
            }

            // Slice the buffer for the packet and reset position for the remaining data
            byte[] packetBuffer = new byte[length];
            Array.Copy(buffer, 0, packetBuffer, 0, length);

            // Shift the remaining data in the buffer
            Array.Copy(buffer, length, buffer, 0, buffer.Length - length);
            position -= length;

            return new IPv4Packet(packetBuffer);
        }

        // Helper to simulate buffer compact by shifting unprocessed data
        public void Next()
        {
            Compact();
        }

        // Reset buffer to simulate compact in ByteBuffer
        private void Compact()
        {
            Array.Copy(buffer, position, buffer, 0, buffer.Length - position);
            position = 0;
        }
    }
}
