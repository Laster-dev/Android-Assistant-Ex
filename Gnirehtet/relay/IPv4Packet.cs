using Gnirehtet.Relay;
using System;
using System.IO;
using System.Text;

namespace Genymobile.Gnirehtet.Relay
{
    public class IPv4Packet
    {
        private static readonly string TAG = typeof(IPv4Packet).Name;

        // Packet length is stored on 16 bits
        public static readonly int MAX_PACKET_LENGTH = 1 << 16;

        private readonly byte[] raw;
        private readonly IPv4Header ipv4Header;
        private readonly TransportHeader transportHeader;

        public IPv4Packet(byte[] raw)
        {
            this.raw = raw;

            if (Log.IsVerboseEnabled())
            {
                Log.V(TAG, "IPv4Packet: " + Binary.BuildPacketString(raw));
            }

            ipv4Header = new IPv4Header(raw);
            if (!ipv4Header.IsSupported())
            {
                Log.D(TAG, "Unsupported IPv4 headers");
                transportHeader = null;
                return;
            }

            transportHeader = CreateTransportHeader();
            Array.Copy(raw, 0, raw, 0, ipv4Header.GetTotalLength()); // Limiting the raw buffer
        }

        public bool IsValid()
        {
            return transportHeader != null;
        }

        private TransportHeader CreateTransportHeader()
        {
            var protocol = ipv4Header.GetProtocol();
            switch (protocol)
            {
                case IPv4Header.Protocol.UDP:
                    return new UDPHeader(GetRawTransport());
                case IPv4Header.Protocol.TCP:
                    return new TCPHeader(GetRawTransport());
                default:
                    throw new InvalidOperationException("Should be unreachable if ipv4Header.IsSupported()");
            }
        }

        private byte[] GetRawTransport()
        {
            int position = ipv4Header.GetHeaderLength();
            byte[] transportBytes = new byte[raw.Length - position];
            Array.Copy(raw, position, transportBytes, 0, transportBytes.Length);
            return transportBytes;
        }

        public IPv4Header GetIpv4Header()
        {
            return ipv4Header;
        }

        public TransportHeader GetTransportHeader()
        {
            return transportHeader;
        }

        public void SwapSourceAndDestination()
        {
            ipv4Header.SwapSourceAndDestination();
            transportHeader.SwapSourceAndDestination();
        }

        public byte[] GetRaw()
        {
            byte[] rawCopy = new byte[raw.Length];
            Array.Copy(raw, rawCopy, raw.Length);
            return rawCopy;
        }

        public int GetRawLength()
        {
            return raw.Length;
        }

        public byte[] GetPayload()
        {
            int headersLength = ipv4Header.GetHeaderLength() + transportHeader.GetHeaderLength();
            byte[] payload = new byte[raw.Length - headersLength];
            Array.Copy(raw, headersLength, payload, 0, payload.Length);
            return payload;
        }

        public int GetPayloadLength()
        {
            return raw.Length - ipv4Header.GetHeaderLength() - transportHeader.GetHeaderLength();
        }

        public void ComputeChecksums()
        {
            ipv4Header.ComputeChecksum();
            transportHeader.ComputeChecksum(ipv4Header, GetPayload());
        }
    }
}
