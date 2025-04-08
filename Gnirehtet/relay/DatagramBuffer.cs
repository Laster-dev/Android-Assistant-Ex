using System;
using System.IO;

namespace Genymobile.Gnirehtet.Relay
{
    public class DatagramBuffer
    {
        private static readonly string TAG = typeof(DatagramBuffer).Name;

        // Every datagram is stored along with a header storing its length, on 16 bits
        private const int HEADER_LENGTH = 2;
        private const int MAX_DATAGRAM_LENGTH = 1 << 16;
        private const int MAX_BLOCK_LENGTH = HEADER_LENGTH + MAX_DATAGRAM_LENGTH;

        private readonly byte[] data;
        private readonly byte[] wrapper;
        private int head;
        private int tail;
        private readonly int circularBufferLength;

        public DatagramBuffer(int capacity)
        {
            data = new byte[capacity + MAX_BLOCK_LENGTH];
            wrapper = data;
            circularBufferLength = capacity + 1;
        }

        public bool IsEmpty()
        {
            return head == tail;
        }

        public bool HasEnoughSpaceFor(int datagramLength)
        {
            if (head >= tail)
            {
                // There is at least the extra space for storing 1 packet
                return true;
            }

            int remaining = tail - head - 1; // 1 extra byte to distinguish empty vs full
            return HEADER_LENGTH + datagramLength <= remaining;
        }

        public int Capacity()
        {
            return circularBufferLength - 1;
        }

        public bool WriteTo(Stream stream)
        {
            int length = ReadLength();
            ArraySegment<byte> segment = new ArraySegment<byte>(wrapper, tail, length);
            tail += length;
            if (tail >= circularBufferLength)
            {
                tail = 0;
            }

            try
            {
                stream.Write(segment.Array, segment.Offset, segment.Count);
                return true;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Cannot write the whole datagram to the stream (IOException: {ex.Message})");
                return false;
            }
        }

        public bool ReadFrom(ByteBuffer buffer)
        {
            int length = buffer.Remaining();
            if (length > MAX_DATAGRAM_LENGTH)
            {
                throw new ArgumentException($"Datagram length ({buffer.Remaining()}) may not be greater than {MAX_DATAGRAM_LENGTH} bytes");
            }

            if (!HasEnoughSpaceFor(length))
            {
                return false;
            }

            WriteLength(length);
            buffer.Get(data, head, length);
            head += length;
            if (head >= circularBufferLength)
            {
                head = 0;
            }

            return true;
        }

        private void WriteLength(int length)
        {
            if ((length & ~0xffff) != 0)
            {
                throw new InvalidOperationException("Length must be stored on 16 bits");
            }

            data[head++] = (byte)((length >> 8) & 0xff);
            data[head++] = (byte)(length & 0xff);
        }

        private int ReadLength()
        {
            int length = ((data[tail] & 0xff) << 8) | (data[tail + 1] & 0xff);
            tail += 2;
            return length;
        }
    }

    // Utility ByteBuffer class
    public class ByteBuffer
    {
        private readonly byte[] _buffer;
        private int _position;

        public ByteBuffer(byte[] buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public int Remaining()
        {
            return _buffer.Length - _position;
        }

        public void Get(byte[] target, int offset, int length)
        {
            Array.Copy(_buffer, _position, target, offset, length);
            _position += length;
        }
    }
}
