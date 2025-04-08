using Gnirehtet.Relay;
using java.nio.channels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace Genymobile.Gnirehtet.Relay
{
    public class Router
    {
        private static readonly string TAG = typeof(Router).Name;

        private readonly Client client;
        private readonly Selector selector;

        // There are typically only few connections per client, HashMap would be less efficient
        private readonly List<Connection> connections = new List<Connection>();

        public Router(Client client, Selector selector)
        {
            this.client = client;
            this.selector = selector;
        }

        public void SendToNetwork(IPv4Packet packet)
        {
            if (!packet.IsValid())
            {
                Log.W(TAG, "Dropping invalid packet");
                if (Log.IsVerboseEnabled())
                {
                    Log.V(TAG, Binary.BuildPacketString(packet.GetRaw()));
                }
                return;
            }
            try
            {
                Connection connection = GetConnection(packet.GetIpv4Header(), packet.GetTransportHeader());
                connection.SendToNetwork(packet);
            }
            catch (IOException e)
            {
                Log.E(TAG, "Cannot create connection, dropping packet", e);
            }
        }

        private Connection GetConnection(IPv4Header ipv4Header, TransportHeader transportHeader)
        {
            ConnectionId id = ConnectionId.From(ipv4Header, transportHeader);
            Connection connection = Find(id);
            if (connection == null)
            {
                connection = CreateConnection(id, ipv4Header, transportHeader);
                connections.Add(connection);
            }
            return connection;
        }

        private Connection CreateConnection(ConnectionId id, IPv4Header ipv4Header, TransportHeader transportHeader)
        {
            IPv4Header.Protocol protocol = id.GetProtocol();
            if (protocol == IPv4Header.Protocol.UDP)
            {
                return new UDPConnection(id, client, selector, ipv4Header, (UDPHeader)transportHeader);
            }
            if (protocol == IPv4Header.Protocol.TCP)
            {
                return new TCPConnection(id, client, selector, ipv4Header, (TCPHeader)transportHeader);
            }
            throw new NotSupportedException($"Unsupported protocol: {protocol}");
        }

        private Connection Find(ConnectionId id)
        {
            foreach (Connection connection in connections)
            {
                if (id.Equals(connection.GetId()))
                {
                    return connection;
                }
            }
            return null;
        }

        public void Clear()
        {
            foreach (Connection connection in connections)
            {
                connection.Disconnect();
            }
            connections.Clear();
        }

        public void Remove(Connection connection)
        {
            if (!connections.Remove(connection))
            {
                throw new InvalidOperationException("Removed a connection unknown from the router");
            }
        }

        public void CleanExpiredConnections()
        {
            for (int i = connections.Count - 1; i >= 0; --i)
            {
                Connection connection = connections[i];
                if (connection.IsExpired())
                {
                    Log.D(TAG, $"Remove expired connection: {connection.GetId()}");
                    connection.Disconnect();
                    connections.RemoveAt(i);
                }
            }
        }
    }
}
