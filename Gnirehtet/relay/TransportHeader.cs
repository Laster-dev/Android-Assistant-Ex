using System;

namespace Genymobile.Gnirehtet.Relay
{
    public interface TransportHeader
    {
        int SourcePort { get; }
        int DestinationPort { get; }

        void SetSourcePort(int port);
        void SetDestinationPort(int port);

        int HeaderLength { get; }

        void SetPayloadLength(int payloadLength);

        byte[] GetRaw();

        TransportHeader CopyTo(byte[] buffer);

        void ComputeChecksum(IPv4Header ipv4Header, byte[] payload);

        // Removed default implementation to fix CS8370 and CS8701
        void SwapSourceAndDestination();
    }

    // Moved the implementation to a helper class
    public static class TransportHeaderExtensions
    {
        public static void SwapSourceAndDestination(this TransportHeader header)
        {
            int tmp = header.SourcePort;
            header.SetSourcePort(header.DestinationPort);
            header.SetDestinationPort(tmp);
        }
    }
}
