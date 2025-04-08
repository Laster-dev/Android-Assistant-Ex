using Genymobile.Gnirehtet.Relay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Gnirehtet.Relay
{
    public class Client
    {
        private static readonly string TAG = nameof(Client);
        private static int nextId = 0;

        private readonly int id;
        private readonly TcpClient clientChannel;
        private readonly CloseListener<Client> closeListener;
        private readonly IPv4PacketBuffer clientToNetwork = new IPv4PacketBuffer();
        private readonly StreamBuffer networkToClient = new StreamBuffer(16 * IPv4Packet.MAX_PACKET_LENGTH);
        private readonly Router router;
        private readonly List<PacketSource> pendingPacketSources = new List<PacketSource>();
        private byte[] pendingIdBuffer;
        private int pendingIdOffset;
        private volatile bool isClosed;

        public Client(TcpClient clientChannel, CloseListener<Client> closeListener)
        {
            id = Interlocked.Increment(ref nextId);
            this.clientChannel = clientChannel;
            this.closeListener = closeListener;
            router = new Router(this);
            pendingIdBuffer = CreateIntBuffer(id);
            pendingIdOffset = 0;

            // 开始异步读写操作
            BeginReceive();
            BeginSend();
        }

        private static byte[] CreateIntBuffer(int value)
        {
            byte[] buffer = new byte[4];
            buffer[0] = (byte)(value >> 24);
            buffer[1] = (byte)(value >> 16);
            buffer[2] = (byte)(value >> 8);
            buffer[3] = (byte)value;
            return buffer;
        }

        public int Id => id;

        public Router Router => router;

        private void BeginReceive()
        {
            if (isClosed) return;

            try
            {
                byte[] buffer = new byte[IPv4Packet.MAX_PACKET_LENGTH];
                clientChannel.GetStream().BeginRead(buffer, 0, buffer.Length, ReceiveCallback, buffer);
            }
            catch (Exception e)
            {
                Log.E(TAG, "Cannot start receiving", e);
                Close();
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            if (isClosed) return;

            try
            {
                int bytesRead = clientChannel.GetStream().EndRead(ar);
                if (bytesRead <= 0)
                {
                    Close();
                    return;
                }
                byte[] buffer = (byte[])ar.AsyncState;

                // Fix for the CS1501 error: Adjusting the method call to match the signature of `ReadFrom` in `IPv4PacketBuffer`  
                clientToNetwork.ReadFrom(new MemoryStream(buffer, 0, bytesRead));


                PushToNetwork();
                BeginReceive(); // 继续接收
            }
            catch (IOException e)
            {
                Log.E(TAG, "Cannot read", e);
                Close();
            }
        }

        private void BeginSend()
        {
            if (isClosed) return;

            if (MustSendId())
            {
                SendId();
            }
            else if (!networkToClient.IsEmpty())
            {
                Write();
            }
            else if (pendingPacketSources.Count > 0)
            {
                ProcessPending();
            }
        }

        private bool MustSendId()
        {
            return pendingIdBuffer != null && pendingIdOffset < pendingIdBuffer.Length;
        }

        private void SendId()
        {
            if (!MustSendId()) return;

            try
            {
                int remaining = pendingIdBuffer.Length - pendingIdOffset;
                clientChannel.GetStream().BeginWrite(pendingIdBuffer, pendingIdOffset, remaining, SendIdCallback, null);
            }
            catch (IOException e)
            {
                Log.E(TAG, $"Cannot write client id #{id}", e);
                Close();
            }
        }

        private void SendIdCallback(IAsyncResult ar)
        {
            if (isClosed) return;

            try
            {
                int bytesWritten = clientChannel.GetStream().EndWrite(ar);
                if (bytesWritten <= 0)
                {
                    Log.W(TAG, $"Cannot write client id #{id} (EOF)");
                    Close();
                    return;
                }

                pendingIdOffset += bytesWritten;
                if (pendingIdOffset >= pendingIdBuffer.Length)
                {
                    Log.D(TAG, $"Client id #{id} sent to client");
                    pendingIdBuffer = null; // 释放缓冲区
                }
                BeginSend(); // 继续发送
            }
            catch (IOException e)
            {
                Log.E(TAG, $"Cannot write client id #{id}", e);
                Close();
            }
        }

        private void Write()
        {
            try
            {
                byte[] data = networkToClient.ToArray();
                clientChannel.GetStream().BeginWrite(data, 0, data.Length, WriteCallback, null);
            }
            catch (IOException e)
            {
                Log.E(TAG, "Cannot write", e);
                Close();
            }
        }

        private void WriteCallback(IAsyncResult ar)
        {
            if (isClosed) return;

            try
            {
                clientChannel.GetStream().EndWrite(ar);
                networkToClient.Clear();
                ProcessPending(); // 处理待发送的数据
                BeginSend(); // 继续发送
            }
            catch (IOException e)
            {
                Log.E   (TAG, "Cannot write", e);
                Close();
            }
        }

        private void PushToNetwork()
        {
            IPv4Packet packet;
            while ((packet = clientToNetwork.AsIPv4Packet()) != null)
            {
                router.SendToNetwork(packet);
                clientToNetwork.Next();
            }
        }

        private void Close()
        {
            if (isClosed) return;
            isClosed = true;

            try
            {
                clientChannel.Close();
            }
            catch (IOException e)
            {
                Log.E(TAG, "Cannot close client connection", e);
            }

            router.Clear();
            closeListener.OnClosed(this);
        }

        public bool SendToClient(IPv4Packet packet)
        {
            if (networkToClient.Remaining < packet.RawLength)
            {
                Log.W(TAG, "Client buffer full");
                return false;
            }

            networkToClient.ReadFrom(packet.Raw);
            BeginSend();
            return true;
        }

        public void Consume(PacketSource source)
        {
            IPv4Packet packet = source.Get();
            if (SendToClient(packet))
            {
                source.Next();
                return;
            }

            if (!pendingPacketSources.Contains(source))
            {
                pendingPacketSources.Add(source);
            }
        }

        private void ProcessPending()
        {
            for (int i = pendingPacketSources.Count - 1; i >= 0; i--)
            {
                PacketSource packetSource = pendingPacketSources[i];
                IPv4Packet packet = packetSource.Get();
                if (SendToClient(packet))
                {
                    packetSource.Next();
                    Log.D(TAG, $"Pending packet sent to client ({packet.RawLength})");
                    pendingPacketSources.RemoveAt(i);
                }
                else
                {
                    Log.W(TAG, $"Pending packet not sent to client ({packet.RawLength}), client buffer full again");
                    break;
                }
            }
        }

        public void CleanExpiredConnections()
        {
            router.CleanExpiredConnections();
        }
    }
}