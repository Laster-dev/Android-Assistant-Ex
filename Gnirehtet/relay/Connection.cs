using Genymobile.Gnirehtet.Relay;

namespace Gnirehtet.Relay
{
    public interface Connection
    {
        ConnectionId Id { get; }
        void SendToNetwork(IPv4Packet packet);
        void Disconnect();
        bool IsExpired();
    }
}