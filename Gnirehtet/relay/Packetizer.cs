using System;
using System.IO;
using System.Net.Sockets;
using System.Numerics;
using System.Text;

namespace Genymobile.Gnirehtet.Relay
{
    public class Packetizer
    {
        private readonly byte[] buffer = new byte[IPv4Packet.MAX_PACKET_LENGTH];
        private readonly MemoryStream payloadBuffer;

        private readonly IPv4Header responseIPv4Header;
        private readonly TransportHeader responseTransportHeader;

        public Packetizer(IPv4Header ipv4Header, TransportHeader transportHeader)
        {
            responseIPv4Header = ipv4Header.CopyTo(buffer);
            responseTransportHeader = transportHeader.CopyTo(buffer);
            payloadBuffer = new MemoryStream(buffer, responseIPv4Header.GetHeaderLength() + responseTransportHeader.GetHeaderLength(), buffer.Length - responseIPv4Header.GetHeaderLength() - responseTransportHeader.GetHeaderLength());
        }

        public IPv4Header GetResponseIPv4Header()
        {
            return responseIPv4Header;
        }

        public TransportHeader GetResponseTransportHeader()
        {
            return responseTransportHeader;
        }

        public IPv4Packet PacketizeEmptyPayload()
        {
            payloadBuffer.SetLength(0);
            return Inflate();
        }

        public IPv4Packet Packetize(NetworkStream stream, int maxChunkSize)
        {
            byte[] chunk = new byte[maxChunkSize];
            int payloadLength = stream.Read(chunk, 0, chunk.Length);
            if (payloadLength == 0)
            {
                return null;
            }
            payloadBuffer.Write(chunk, 0, payloadLength);
            return Inflate();
        }

        public IPv4Packet Packetize(NetworkStream stream)
        {
            return Packetize(stream, payloadBuffer.Capacity);
        }

        // Fix for CS1503: Convert MemoryStream to byte[] before passing to IPv4Packet constructor
        private IPv4Packet Inflate()
        {
            int payloadLength = (int)payloadBuffer.Length;
            int ipv4HeaderLength = responseIPv4Header.GetHeaderLength();
            int transportHeaderLength = responseTransportHeader.GetHeaderLength();
            int totalLength = ipv4HeaderLength + transportHeaderLength + payloadLength;

            responseIPv4Header.SetTotalLength(totalLength);
            responseTransportHeader.SetPayloadLength(payloadLength);

            // Convert the buffer to a byte array for the IPv4Packet constructor
            byte[] packetData = new byte[totalLength];
            Array.Copy(buffer, 0, packetData, 0, totalLength);

            IPv4Packet packet = new IPv4Packet(packetData);
            packet.ComputeChecksums();
            return packet;
        }
    }
}
