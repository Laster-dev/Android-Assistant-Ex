using System;
using System.IO;
using System.Net;

namespace Genymobile.Gnirehtet.Relay
{
    public class UDPHeader : TransportHeader
    {
        private static readonly int UDP_HEADER_LENGTH = 8;

        private readonly byte[] raw;
        private int sourcePort;
        private int destinationPort;

        public UDPHeader(byte[] raw)
        {
            this.raw = raw;
            sourcePort = BitConverter.ToUInt16(raw, 0);
            destinationPort = BitConverter.ToUInt16(raw, 2);
        }

        public int SourcePort => sourcePort;

        public int DestinationPort => destinationPort;

        public int HeaderLength => throw new NotImplementedException();

        public void SetSourcePort(int sourcePort)
        {
            this.sourcePort = sourcePort;
            BitConverter.GetBytes((ushort)sourcePort).CopyTo(raw, 0);
        }

        public void SetDestinationPort(int destinationPort)
        {
            this.destinationPort = destinationPort;
            BitConverter.GetBytes((ushort)destinationPort).CopyTo(raw, 2);
        }

        public int GetHeaderLength() => UDP_HEADER_LENGTH;

        public void SetPayloadLength(int payloadLength)
        {
            int length = GetHeaderLength() + payloadLength;
            BitConverter.GetBytes((ushort)length).CopyTo(raw, 4);
        }

        public byte[] GetRaw()
        {
            byte[] slice = new byte[raw.Length];
            Array.Copy(raw, slice, raw.Length);
            return slice;
        }

        public UDPHeader CopyTo(byte[] target)
        {
            Array.Copy(raw, 0, target, 0, GetHeaderLength());
            byte[] targetSlice = new byte[GetHeaderLength()];
            Array.Copy(target, targetSlice, targetSlice.Length);
            return new UDPHeader(targetSlice);
        }

        public void ComputeChecksum(IPv4Header ipv4Header, byte[] payload)
        {
            // Disable checksum validation (set checksum to 0)
            BitConverter.GetBytes((ushort)0).CopyTo(raw, 6);
        }

        TransportHeader TransportHeader.CopyTo(byte[] buffer)
        {
            return CopyTo(buffer);
        }

        public void SwapSourceAndDestination()
        {
            throw new NotImplementedException();
        }
    }
}
