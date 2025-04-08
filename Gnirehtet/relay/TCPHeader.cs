using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.Remoting.Channels;

namespace Genymobile.Gnirehtet.Relay
{
    public class TCPHeader : TransportHeaders
    {
        public const int FLAG_FIN = 1 << 0;
        public const int FLAG_SYN = 1 << 1;
        public const int FLAG_RST = 1 << 2;
        public const int FLAG_PSH = 1 << 3;
        public const int FLAG_ACK = 1 << 4;
        public const int FLAG_URG = 1 << 5;

        private byte[] raw; // Remove readonly modifier to allow assignment
        private int sourcePort;
        private int destinationPort;
        private int headerLength;
        private int sequenceNumber;
        private int acknowledgementNumber;
        private int flags;
        private int window;

        public TCPHeader(byte[] raw)
        {
            this.raw = raw;

            sourcePort = BitConverter.ToUInt16(raw, 0);
            destinationPort = BitConverter.ToUInt16(raw, 2);

            sequenceNumber = BitConverter.ToInt32(raw, 4);
            acknowledgementNumber = BitConverter.ToInt32(raw, 8);

            short dataOffsetAndFlags = BitConverter.ToInt16(raw, 12);
            headerLength = (dataOffsetAndFlags & 0xf000) >> 12;
            flags = dataOffsetAndFlags & 0x1ff;

            window = BitConverter.ToUInt16(raw, 14);
        }

        public int Window => window;

        public int SourcePort => sourcePort;

        public int DestinationPort => destinationPort;

        public void SetSourcePort(int sourcePort)
        {
            this.sourcePort = sourcePort;
            BitConverter.GetBytes((short)sourcePort).CopyTo(raw, 0);
        }

        public void SetDestinationPort(int destinationPort)
        {
            this.destinationPort = destinationPort;
            BitConverter.GetBytes((short)destinationPort).CopyTo(raw, 2);
        }

        public int SequenceNumber => sequenceNumber;

        public void SetSequenceNumber(int sequenceNumber)
        {
            this.sequenceNumber = sequenceNumber;
            BitConverter.GetBytes(sequenceNumber).CopyTo(raw, 4);
        }

        public int AcknowledgementNumber => acknowledgementNumber;

        public void SetAcknowledgementNumber(int acknowledgementNumber)
        {
            this.acknowledgementNumber = acknowledgementNumber;
            BitConverter.GetBytes(acknowledgementNumber).CopyTo(raw, 8);
        }

        public int HeaderLength => headerLength;

        public void SetPayloadLength(int payloadLength)
        {
            // do nothing
        }

        public int Flags => flags;

        public void SetFlags(int flags)
        {
            this.flags = flags;
            short dataOffsetAndFlags = BitConverter.ToInt16(raw, 12);
            dataOffsetAndFlags = (short)(dataOffsetAndFlags & 0xfe00 | flags & 0x1ff);
            BitConverter.GetBytes(dataOffsetAndFlags).CopyTo(raw, 12);
        }

        public void ShrinkOptions()
        {
            SetDataOffset(5);
            byte[] resizedRaw = new byte[20];
            Array.Copy(raw, resizedRaw, Math.Min(raw.Length, 20));
            raw = resizedRaw; // This assignment is now valid
        }

        private void SetDataOffset(int dataOffset)
        {
            short dataOffsetAndFlags = BitConverter.ToInt16(raw, 12);
            dataOffsetAndFlags = (short)(dataOffsetAndFlags & 0x0fff | (dataOffset << 12));
            BitConverter.GetBytes(dataOffsetAndFlags).CopyTo(raw, 12);
            headerLength = dataOffset << 2;
        }

        public bool IsFin() => (flags & FLAG_FIN) != 0;

        public bool IsSyn() => (flags & FLAG_SYN) != 0;

        public bool IsRst() => (flags & FLAG_RST) != 0;

        public bool IsPsh() => (flags & FLAG_PSH) != 0;

        public bool IsAck() => (flags & FLAG_ACK) != 0;

        public bool IsUrg() => (flags & FLAG_URG) != 0;

        public byte[] GetRaw()
        {
            byte[] copy = new byte[raw.Length];
            Array.Copy(raw, copy, raw.Length);
            return copy;
        }

        public TCPHeader CopyTo(byte[] target)
        {
            Array.Copy(raw, 0, target, 0, headerLength);
            return new TCPHeader(target);
        }

        public TCPHeader Copy()
        {
            byte[] copy = new byte[raw.Length];
            Array.Copy(raw, copy, raw.Length);
            return new TCPHeader(copy);
        }

        public void ComputeChecksum(IPv4Header ipv4Header, byte[] payload)
        {
            int sum = 0;

            int source = ipv4Header.GetSource();
            int destination = ipv4Header.Destination;
            int length = ipv4Header.TotalLength - ipv4Header.HeaderLength;
            if ((length & ~0xffff) != 0)
                throw new InvalidOperationException("Length cannot take more than 16 bits");

            sum += source >> 16;
            sum += source & 0xffff;
            sum += destination >> 16;
            sum += destination & 0xffff;
            sum += (int)IPv4Header.Protocol.TCP;
            sum += length;

            // reset checksum field
            SetChecksum((short)0);

            for (int i = 0; i < headerLength / 2; ++i)
            {
                sum += ((raw[2 * i] & 0xff) << 8) | (raw[2 * i + 1] & 0xff);
            }

            int payloadLength = length - headerLength;
            if (payloadLength != payload.Length)
                throw new InvalidOperationException("Payload length does not match");

            for (int i = 0; i < payloadLength / 2; ++i)
            {
                sum += ((payload[2 * i] & 0xff) << 8) | (payload[2 * i + 1] & 0xff);
            }

            if (payloadLength % 2 != 0)
            {
                sum += (payload[payloadLength - 1] & 0xff) << 8;
            }

            while ((sum & ~0xffff) != 0)
            {
                sum = (sum & 0xffff) + (sum >> 16);
            }

            SetChecksum((short)~sum);
        }

        private void SetChecksum(short checksum)
        {
            BitConverter.GetBytes(checksum).CopyTo(raw, 16);
        }

        public short GetChecksum()
        {
            return BitConverter.ToInt16(raw, 16);
        }
    }
}
