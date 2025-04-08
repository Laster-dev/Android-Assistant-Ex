using System;
using System.IO;
using System.Text;

namespace Genymobile.Gnirehtet.Relay
{
    public class IPv4Header
    {
        public enum Protocol
        {
            TCP = 6,
            UDP = 17,
            OTHER = -1
        }

        private const int MIN_IPV4_HEADER_LENGTH = 20;

        private byte[] raw;
        private byte version;
        private int headerLength;
        private int totalLength;
        private Protocol protocol;
        private int source;
        private int destination;

        public IPv4Header(byte[] raw)
        {
            if (raw.Length < MIN_IPV4_HEADER_LENGTH)
            {
                throw new ArgumentException("IPv4 header length must be at least 20 bytes");
            }

            this.raw = raw;

            byte versionAndIHL = raw[0];
            version = (byte)(versionAndIHL >> 4);

            byte ihl = (byte)(versionAndIHL & 0xf);
            headerLength = ihl << 2;

            totalLength = BitConverter.ToUInt16(raw, 2);

            int protocolNumber = raw[9];
            protocol = ProtocolFromNumber(protocolNumber);

            source = BitConverter.ToInt32(raw, 12);
            destination = BitConverter.ToInt32(raw, 16);
        }

        public bool IsSupported()
        {
            return version == 4 && protocol != Protocol.OTHER;
        }

        public Protocol GetProtocol()
        {
            return protocol;
        }

        public int GetHeaderLength()
        {
            return headerLength;
        }

        public int GetTotalLength()
        {
            return totalLength;
        }

        public void SetTotalLength(int totalLength)
        {
            this.totalLength = totalLength;
            Array.Copy(BitConverter.GetBytes((ushort)totalLength), 0, raw, 2, 2);
        }

        public int GetSource()
        {
            return source;
        }

        public int GetDestination()
        {
            return destination;
        }

        public void SetSource(int source)
        {
            this.source = source;
            Array.Copy(BitConverter.GetBytes(source), 0, raw, 12, 4);
        }

        public void SetDestination(int destination)
        {
            this.destination = destination;
            Array.Copy(BitConverter.GetBytes(destination), 0, raw, 16, 4);
        }

        public void SwapSourceAndDestination()
        {
            int temp = source;
            SetSource(destination);
            SetDestination(temp);
        }

        public byte[] GetRaw()
        {
            byte[] slice = new byte[headerLength];
            Array.Copy(raw, slice, headerLength);
            return slice;
        }

        public IPv4Header CopyTo(byte[] target)
        {
            byte[] slice = new byte[headerLength];
            Array.Copy(raw, slice, headerLength);
            Array.Copy(slice, 0, target, 0, headerLength);
            return new IPv4Header(slice);
        }

        public IPv4Header Copy()
        {
            byte[] copy = new byte[raw.Length];
            Array.Copy(raw, copy, raw.Length);
            return new IPv4Header(copy);
        }

        public void ComputeChecksum()
        {
            SetChecksum((short)0);

            // checksum computation is the most CPU-intensive task in gnirehtet
            // prefer optimization over readability
            int sum = 0;
            for (int i = 0; i < headerLength / 2; ++i)
            {
                // compute a 16-bit value from two 8-bit values manually
                sum += (raw[2 * i] << 8) | raw[2 * i + 1];
            }

            while ((sum & ~0xffff) != 0)
            {
                sum = (sum & 0xffff) + (sum >> 16);
            }
            SetChecksum((short)~sum);
        }

        private void SetChecksum(short checksum)
        {
            Array.Copy(BitConverter.GetBytes(checksum), 0, raw, 10, 2);
        }

        public short GetChecksum()
        {
            return BitConverter.ToInt16(raw, 10);
        }

        public static int ReadVersion(byte[] buffer)
        {
            if (buffer.Length == 0)
            {
                return -1; // buffer is empty
            }
            // version is stored in the 4 first bits
            byte versionAndIHL = buffer[0];
            return (versionAndIHL & 0xf0) >> 4;
        }

        public static int ReadLength(byte[] buffer)
        {
            if (buffer.Length < 4)
            {
                return -1; // buffer does not even contain the length field
            }
            // packet length is 16 bits starting at offset 2
            return BitConverter.ToUInt16(buffer, 2);
        }

        private Protocol ProtocolFromNumber(int number)
        {
            switch (number)
            {
                case 6:
                    return Protocol.TCP;
                case 17:
                    return Protocol.UDP;
                default:
                    return Protocol.OTHER;
            }
        }
    }
}
