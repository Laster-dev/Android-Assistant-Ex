using Genymobile.Gnirehtet.Relay;
using System;
using System.Net;

namespace Gnirehtet.Relay
{
    public abstract class AbstractConnection : Connection
    {
        private const int LOCALHOST_FORWARD = 0x0a000202; // 10.0.2.2 must be forwarded to localhost

        private readonly ConnectionId id;
        private readonly Client client;

        protected AbstractConnection(ConnectionId id, Client client)
        {
            this.id = id;
            this.client = client;
        }

        public ConnectionId Id => id;

        ConnectionId Connection.Id => throw new NotImplementedException();

        protected void Close()
        {
            Disconnect();
            client.Router.Remove(this);
        }

        protected void Consume(PacketSource source)
        {
            // Ensure the PacketSource type matches the expected type in the Client.Consume method
            client.Consume(source);
        }

        protected bool SendToClient(IPv4Packet packet)
        {
            return client.SendToClient(packet);
        }

        private static IPAddress GetRewrittenAddress(int ip)
        {
            return ip == LOCALHOST_FORWARD ? IPAddress.Loopback : Net.ToInetAddress(ip);
        }

        /// <summary>
        /// Get destination, rewritten to localhost if it was 10.0.2.2.
        /// </summary>
        /// <returns>Destination to connect to.</returns>
        protected IPEndPoint GetRewrittenDestination()
        {
            int destIp = id.DestinationIp;
            int port = id.DestinationPort;
            return new IPEndPoint(GetRewrittenAddress(destIp), port);
        }

        public void LogV(string tag, string message, Exception e = null)
        {
            Log.V(tag, $"{id} {message}");
        }

        public void LogV(string tag, string message)
        {
            LogV(tag, message, null);
        }

        public void LogD(string tag, string message, Exception e = null)
        {
            Log.D(tag, $"{id} {message}");
        }

        public void LogD(string tag, string message)
        {
            LogD(tag, message, null);
        }

        public void LogI(string tag, string message, Exception e = null)
        {
            Log.I(tag, $"{id} {message}");
        }

        public void LogI(string tag, string message)
        {
            LogI(tag, message, null);
        }

        public void LogW(string tag, string message, Exception e = null)
        {
            Log.W(tag, $"{id} {message}");
        }

        public void LogW(string tag, string message)
        {
            LogW(tag, message, null);
        }

        public void LogE(string tag, string message, Exception e = null)
        {
            Log.E(tag, $"{id} {message}");
        }

        public void LogE(string tag, string message)
        {
            LogE(tag, message, null);
        }

        // 抽象方法，子类需要实现
        public abstract void Disconnect();

        public void SendToNetwork(IPv4Packet packet)
        {
            throw new NotImplementedException();
        }

        public bool IsExpired()
        {
            throw new NotImplementedException();
        }
    }
}