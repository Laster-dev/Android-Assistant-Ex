using System;

namespace Genymobile.Gnirehtet.Relay
{
    public class ConnectionId
    {
        private readonly IPv4Header.Protocol protocol;
        private readonly int sourceIp;
        private readonly short sourcePort;
        private readonly int destIp;
        private readonly short destPort;
        private readonly string idString;

        public ConnectionId(IPv4Header.Protocol protocol, int sourceIp, short sourcePort, int destIp, short destPort)
        {
            this.protocol = protocol;
            this.sourceIp = sourceIp;
            this.sourcePort = sourcePort;
            this.destIp = destIp;
            this.destPort = destPort;

            // compute the String representation only once
            idString = $"{protocol} {Net.ToString(sourceIp, sourcePort)} -> {Net.ToString(destIp, destPort)}";
        }

        public IPv4Header.Protocol Protocol => protocol;

        public int SourceIp => sourceIp;

        public int SourcePort => ushort.ToUnsignedInt(sourcePort);

        public int DestinationIp => destIp;

        public int DestinationPort => ushort.ToUnsignedInt(destPort);

        public override bool Equals(object obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;

            var that = (ConnectionId)obj;
            return sourceIp == that.sourceIp
                   && sourcePort == that.sourcePort
                   && destIp == that.destIp
                   && destPort == that.destPort
                   && protocol == that.protocol;
        }

        public override int GetHashCode()
        {
            int result = protocol.GetHashCode();
            result = 31 * result + sourceIp;
            result = 31 * result + sourcePort;
            result = 31 * result + destIp;
            result = 31 * result + destPort;
            return result;
        }

        public override string ToString()
        {
            return idString;
        }

        public static ConnectionId From(IPv4Header ipv4Header, TransportHeader transportHeader)
        {
            var protocol = ipv4Header.Protocol;
            int sourceAddress = ipv4Header.Source;
            short sourcePort = (short)transportHeader.SourcePort;
            int destinationAddress = ipv4Header.Destination;
            short destinationPort = (short)transportHeader.DestinationPort;
            return new ConnectionId(protocol, sourceAddress, sourcePort, destinationAddress, destinationPort);
        }
    }
}
