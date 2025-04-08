using System;

namespace Genymobile.Gnirehtet.Relay
{
    /// <summary>
    /// Source that may produce packets.
    /// <para>
    /// When a {@link TCPConnection} sends a packet to the {@link Client} while its buffers are full,
    /// then it fails. To recover, once some space becomes available, the {@link Client} must pull the
    /// available packets.
    /// </para>
    /// <para>
    /// This interface provides the abstraction of a packet source from which it can pull packets.
    /// </para>
    /// <para>
    /// It is implemented by {@link TCPConnection}.
    /// </para>
    /// </summary>
    public interface PacketSource
    {
        IPv4Packet Get();

        void Next();
    }
}
