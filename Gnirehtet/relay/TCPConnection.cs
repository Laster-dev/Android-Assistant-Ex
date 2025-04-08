using Gnirehtet.Relay;
using java.nio.channels;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Genymobile.Gnirehtet.Relay
{
    public class TCPConnection : AbstractConnection, PacketSource
    {
        private const int MTU = 0x4000;
        private const int MAX_PAYLOAD_SIZE = MTU - 20 - 20;  // 20 bytes for IP headers, 20 bytes for TCP headers
        private static readonly Random RANDOM = new Random();

        public enum State
        {
            SYN_SENT,
            SYN_RECEIVED,
            ESTABLISHED,
            CLOSE_WAIT,
            LAST_ACK,
            CLOSING,
            FIN_WAIT_1,
            FIN_WAIT_2
        }

        private StreamBuffer clientToNetwork = new StreamBuffer(4 * IPv4Packet.MAX_PACKET_LENGTH);
        private Packetizer networkToClient;
        private IPv4Packet packetForClient;
        private Socket socket;
        private SelectionKey selectionKey;
        private int interests;

        private State state;
        private int synSequenceNumber;
        private int sequenceNumber;
        private int acknowledgementNumber;
        private int theirAcknowledgementNumber;
        private int clientWindow;
        private int? finSequenceNumber; // null means "no FIN sent yet"
        private bool finReceived;

        public TCPConnection(ConnectionId id, Client client, Selector selector, IPv4Header ipv4Header, TCPHeader tcpHeader)
            : base(id, client)
        {
            TCPHeader shrinkedTcpHeader = tcpHeader.Copy();
            shrinkedTcpHeader.ShrinkOptions();  // no TCP options

            networkToClient = new Packetizer(ipv4Header, shrinkedTcpHeader);
            networkToClient.GetResponseIPv4Header().SwapSourceAndDestination();
            networkToClient.GetResponseTransportHeader().SwapSourceAndDestination();

            var selectionHandler = new SelectionHandler((selectionKey) =>
            {
                if (selectionKey.IsValid && selectionKey.IsConnectable)
                {
                    ProcessConnect();
                }
                if (selectionKey.IsValid && selectionKey.IsReadable)
                {
                    ProcessReceive();
                }
                if (selectionKey.IsValid && selectionKey.IsWritable)
                {
                    ProcessSend();
                }
                UpdateInterests();
            });

            socket = CreateSocket();
            interests = SelectionKey.OP_CONNECT;
            selectionKey = socket.Register(selector, interests, selectionHandler);
        }

        public override void Disconnect()
        {
            Logi("TCPConnection", "Close");
            selectionKey.Cancel();
            try
            {
                socket.Close();
            }
            catch (IOException e)
            {
                Loge("TCPConnection", "Cannot close connection channel", e);
            }
        }

        private void ProcessReceive()
        {
            try
            {
                if (packetForClient != null)
                    throw new InvalidOperationException("The IPv4Packet shares the networkToClient buffer, it must not be corrupted");

                int remainingClientWindow = GetRemainingClientWindow();
                if (remainingClientWindow <= 0)
                    throw new InvalidOperationException("If remainingClientWindow is 0, then processReceive() should not have been called");

                int maxPayloadSize = Math.Min(remainingClientWindow, MAX_PAYLOAD_SIZE);
                UpdateHeaders(TCPHeader.FLAG_ACK | TCPHeader.FLAG_PSH);
                packetForClient = networkToClient.Packetize(socket, maxPayloadSize);

                if (packetForClient == null)
                {
                    Eof();
                    return;
                }

                Consume(this);
            }
            catch (IOException e)
            {
                Loge("TCPConnection", "Cannot read", e);
                ResetConnection();
            }
        }

        private void ProcessSend()
        {
            try
            {
                int w = clientToNetwork.WriteTo(socket);
                if (w > 0)
                {
                    acknowledgementNumber += w;

                    Logd("TCPConnection", $"{w} bytes written to the network socket");

                    if (finReceived && clientToNetwork.IsEmpty())
                    {
                        Logd("TCPConnection", "No more pending data, process the pending FIN");
                        DoHandleFin();
                    }
                    else
                    {
                        Logd("TCPConnection", $"Sending ACK {Numbers()} to client");
                        SendEmptyPacketToClient(TCPHeader.FLAG_ACK);
                    }
                }
                else
                {
                    Close();
                }
            }
            catch (IOException e)
            {
                Loge("TCPConnection", "Cannot write", e);
                ResetConnection();
            }
        }

        private void Eof()
        {
            SendEmptyPacketToClient(TCPHeader.FLAG_FIN | TCPHeader.FLAG_ACK);

            finSequenceNumber = sequenceNumber;
            ++sequenceNumber; // FIN counts for 1 byte
            state = state == State.CLOSE_WAIT ? State.LAST_ACK : State.FIN_WAIT_1;
            Logd("TCPConnection", $"State = {state}");
        }

        private int GetRemainingClientWindow()
        {
            int remaining = theirAcknowledgementNumber + clientWindow - sequenceNumber;
            return remaining < 0 || remaining > clientWindow ? 0 : remaining;
        }

        public override bool IsExpired()
        {
            return false;  // No external timeout expiration
        }

        private void UpdateHeaders(int flags)
        {
            TCPHeader tcpHeader = (TCPHeader)networkToClient.GetResponseTransportHeader();
            tcpHeader.SetFlags(flags);
            tcpHeader.SetSequenceNumber(sequenceNumber);
            tcpHeader.SetAcknowledgementNumber(acknowledgementNumber);
        }

        private Socket CreateSocket()
        {
            Logi("TCPConnection", "Open");
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.Blocking = false;
            sock.Connect(GetRewrittenDestination());
            return sock;
        }

        public void SendToNetwork(IPv4Packet packet)
        {
            HandlePacket(packet);
            Logd("TCPConnection", $"current ack={acknowledgementNumber}");
            UpdateInterests();
        }

        private void HandlePacket(IPv4Packet packet)
        {
            TCPHeader tcpHeader = (TCPHeader)packet.GetTransportHeader();
            if (state == null)
            {
                HandleFirstPacket(packet);
                return;
            }

            if (tcpHeader.IsSyn())
            {
                HandleDuplicateSyn(packet);
                return;
            }

            int packetSequenceNumber = tcpHeader.GetSequenceNumber();
            int expectedPacket = acknowledgementNumber + clientToNetwork.Size();
            if (packetSequenceNumber != expectedPacket)
            {
                Logw("TCPConnection", $"Ignoring packet {packetSequenceNumber} (acking {tcpHeader.GetAcknowledgementNumber()}); expecting {expectedPacket}; flags={tcpHeader.GetFlags()}");
                return;
            }

            clientWindow = tcpHeader.GetWindow();
            theirAcknowledgementNumber = tcpHeader.GetAcknowledgementNumber();

            Logd("TCPConnection", $"Receiving expected packet {packetSequenceNumber} (flags = {tcpHeader.GetFlags()})");

            if (tcpHeader.IsRst())
            {
                Logd("TCPConnection", "Reset requested, closing");
                Close();
                return;
            }

            if (tcpHeader.IsAck())
            {
                Logd("TCPConnection", $"Client acked {tcpHeader.GetAcknowledgementNumber()}");
                HandleAck(packet);
            }

            if (tcpHeader.IsFin())
            {
                HandleFin();
            }

            if (finSequenceNumber.HasValue && tcpHeader.GetAcknowledgementNumber() == finSequenceNumber + 1)
            {
                Logd("TCPConnection", "Received ACK of FIN");
                HandleFinAck();
            }
        }

        private void HandleFirstPacket(IPv4Packet packet)
        {
            Logd("TCPConnection", "handleFirstPacket()");
            TCPHeader tcpHeader = (TCPHeader)packet.GetTransportHeader();
            if (!tcpHeader.IsSyn())
            {
                Logw("TCPConnection", $"Unexpected first packet {tcpHeader.GetSequenceNumber()}; acking {tcpHeader.GetAcknowledgementNumber()}; flags={tcpHeader.GetFlags()}");
                sequenceNumber = tcpHeader.GetAcknowledgementNumber();  // Make a RST in the window client
                ResetConnection();
                return;
            }

            int theirSequenceNumber = tcpHeader.GetSequenceNumber();
            acknowledgementNumber = theirSequenceNumber + 1;
            synSequenceNumber = theirSequenceNumber;

            sequenceNumber = RANDOM.Next();
            Logd("TCPConnection", $"initialized seqNum={sequenceNumber}; ackNum={acknowledgementNumber}");
            clientWindow = tcpHeader.GetWindow();
            state = State.SYN_SENT;
            Logd("TCPConnection", $"State = {state}");
        }

        private void HandleDuplicateSyn(IPv4Packet packet)
        {
            TCPHeader tcpHeader = (TCPHeader)packet.GetTransportHeader();
            int theirSequenceNumber = tcpHeader.GetSequenceNumber();
            if (state == State.SYN_SENT)
            {
                synSequenceNumber = theirSequenceNumber;
                acknowledgementNumber = theirSequenceNumber + 1;
            }
            else if (theirSequenceNumber != synSequenceNumber)
            {
                ResetConnection();
            }
        }

        private void HandleFin()
        {
            Logd("TCPConnection", $"Received a FIN from the client {Numbers()}");
            finReceived = true;
            if (clientToNetwork.IsEmpty())
            {
                Logd("TCPConnection", "No pending data, process the FIN immediately");
                DoHandleFin();
            }
        }

        private void DoHandleFin()
        {
            ++acknowledgementNumber;  // Received FIN counts for 1 byte

            switch (state)
            {
                case State.ESTABLISHED:
                    SendEmptyPacketToClient(TCPHeader.FLAG_FIN | TCPHeader.FLAG_ACK);
                    finSequenceNumber = sequenceNumber;
                    ++sequenceNumber;  // FIN counts for 1 byte
                    state = State.LAST_ACK;
                    Logd("TCPConnection", $"State = {state}");
                    break;
                case State.FIN_WAIT_1:
                    SendEmptyPacketToClient(TCPHeader.FLAG_ACK);
                    state = State.FIN_WAIT_2;
                    Logd("TCPConnection", $"State = {state}");
                    break;
                case State.CLOSE_WAIT:
                    state = State.LAST_ACK;
                    Logd("TCPConnection", $"State = {state}");
                    break;
                case State.LAST_ACK:
                    Close();
                    break;
            }
        }

        private void HandleAck(IPv4Packet packet)
        {
            Logd("TCPConnection", $"Received a valid ACK from client {Numbers()}");

            // Move the client's data from buffer to our transmit buffer.
            clientToNetwork.Shrink(packet.GetPayload());

            // Ensure the remaining data fits in window.
            if (clientToNetwork.IsFull() && state == State.ESTABLISHED)
            {
                SendEmptyPacketToClient(TCPHeader.FLAG_ACK);
            }
        }

        private void SendEmptyPacketToClient(int flags)
        {
            packetForClient = networkToClient.PacketizeEmpty(socket, flags);
            if (packetForClient != null)
                Consume(this);
        }

        private void ResetConnection()
        {
            Logd("TCPConnection", "reset connection");
            state = State.CLOSED;
            selectionKey.Cancel();
            try
            {
                socket.Close();
            }
            catch (IOException e)
            {
                Loge("TCPConnection", "Cannot close connection", e);
            }
        }

        private void UpdateInterests()
        {
            // Update the selection interests based on state
            if (state == State.SYN_SENT)
                interests = SelectionKey.OP_CONNECT;
            else if (state == State.ESTABLISHED)
                interests = SelectionKey.OP_READ | SelectionKey.OP_WRITE;
            else if (state == State.FIN_WAIT_1 || state == State.FIN_WAIT_2)
                interests = SelectionKey.OP_READ;
            else
                interests = 0;
            selectionKey.InterestOps(interests);
        }

        public IPv4Packet Get()
        {
            throw new NotImplementedException();
        }

        public void Next()
        {
            throw new NotImplementedException();
        }
    }
}
