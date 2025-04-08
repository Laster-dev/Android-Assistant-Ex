using Gnirehtet.Relay;
using java.nio.channels;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Genymobile.Gnirehtet.Relay
{
    public class UDPConnection : AbstractConnection
    {
        public static readonly long IDLE_TIMEOUT = 2 * 60 * 1000;

        private static readonly string TAG = nameof(UDPConnection);

        private readonly DatagramBuffer clientToNetwork = new DatagramBuffer(4 * IPv4Packet.MAX_PACKET_LENGTH);
        private readonly Packetizer networkToClient;

        private readonly UdpClient channel;
        private readonly SelectionKey selectionKey;
        private int interests;

        private long idleSince;

        public UDPConnection(ConnectionId id, Client client, Selector selector, IPv4Header ipv4Header, UDPHeader udpHeader)
            : base(id, client)
        {
            networkToClient = new Packetizer(ipv4Header, udpHeader);
            networkToClient.ResponseIPv4Header.SwapSourceAndDestination();
            networkToClient.ResponseTransportHeader.SwapSourceAndDestination();

            Touch();

            // Selection handler
            SelectionHandler selectionHandler = selectionKey =>
            {
                Touch();
                if (selectionKey.IsValid && selectionKey.IsReadable)
                {
                    ProcessReceive();
                }
                if (selectionKey.IsValid && selectionKey.IsWritable)
                {
                    ProcessSend();
                }
                UpdateInterests();
            };

            channel = CreateChannel();
            interests = SelectionKey.OP_READ;
            selectionKey = channel.Register(selector, interests, selectionHandler);
        }

        public override void SendToNetwork(IPv4Packet packet)
        {
            if (!clientToNetwork.ReadFrom(packet.Payload))
            {
                LogW(TAG, "Cannot send to network, dropping packet");
                return;
            }
            UpdateInterests();
        }

        public override void Disconnect()
        {
            LogI(TAG, "Close");
            selectionKey.Cancel();
            try
            {
                channel.Close();
            }
            catch (IOException e)
            {
                LogE(TAG, "Cannot close connection channel", e);
            }
        }

        public override bool IsExpired()
        {
            return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond >= idleSince + IDLE_TIMEOUT;
        }

        private UdpClient CreateChannel()
        {
            LogI(TAG, "Open");
            var udpClient = new UdpClient
            {
                Client = { Blocking = false }
            };
            udpClient.Connect(RewrittenDestination);
            return udpClient;
        }

        private void Touch()
        {
            idleSince = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }

        private void ProcessReceive()
        {
            var packet = Read();
            if (packet == null)
            {
                Close();
                return;
            }
            PushToClient(packet);
        }

        private void ProcessSend()
        {
            if (!Write())
            {
                Close();
            }
        }

        private IPv4Packet Read()
        {
            try
            {
                return networkToClient.Packetize(channel);
            }
            catch (IOException e)
            {
                LogE(TAG, "Cannot read", e);
                return null;
            }
        }

        private bool Write()
        {
            try
            {
                return clientToNetwork.WriteTo(channel);
            }
            catch (IOException e)
            {
                LogE(TAG, "Cannot write", e);
                return false;
            }
        }

        private void PushToClient(IPv4Packet packet)
        {
            if (!SendToClient(packet))
            {
                LogW(TAG, "Cannot send to client, dropping packet");
                return;
            }
            LogD(TAG, $"Packet ({packet.PayloadLength} bytes) sent to client");
            if (Log.IsVerboseEnabled())
            {
                LogV(TAG, Binary.BuildPacketString(packet.Raw));
            }
        }

        protected void UpdateInterests()
        {
            if (!selectionKey.IsValid)
            {
                return;
            }

            int interestOps = SelectionKey.OP_READ;
            if (MayWrite())
            {
                interestOps |= SelectionKey.OP_WRITE;
            }

            if (interests != interestOps)
            {
                interests = interestOps;
                selectionKey.InterestOps(interestOps);
            }
        }

        private bool MayWrite()
        {
            return !clientToNetwork.IsEmpty;
        }
    }
}
