using System;
using System.IO;
using System.Text;

namespace Genymobile.Gnirehtet.Relay
{
    public class StreamBuffer
    {
        private readonly byte[] data;
        private readonly byte[] wrapper;
        private int head;
        private int tail;

        public StreamBuffer(int capacity)
        {
            data = new byte[capacity + 1];
            wrapper = new byte[capacity + 1];
        }

        public bool IsEmpty() => head == tail;

        public bool IsFull() => (head + 1) % data.Length == tail;

        public int Size()
        {
            if (head < tail)
            {
                return head + data.Length - tail;
            }
            return head - tail;
        }

        public int Capacity() => data.Length - 1;

        public int Remaining() => Capacity() - Size();

        public int WriteTo(Stream stream)
        {
            int written = 0;

            if (head > tail)
            {
                int length = head - tail;
                Array.Copy(data, tail, wrapper, 0, length);
                stream.Write(wrapper, 0, length); // Fix: Removed assignment to 'written' as 'stream.Write' returns void.  
                written = length; // Assign the actual length written.  
                tail = (tail + written) % data.Length;
                Optimize();
            }
            else if (head < tail)
            {
                int length1 = data.Length - tail;
                int length2 = head;
                Array.Copy(data, tail, wrapper, 0, length1);
                Array.Copy(data, 0, wrapper, length1, length2);
                stream.Write(wrapper, 0, length1 + length2); // Fix: Removed assignment to 'written' as 'stream.Write' returns void.  
                written = length1 + length2; // Assign the actual length written.  
                tail = written % data.Length;
                Optimize();
            }

            return written;
        }

        public void ReadFrom(byte[] buffer)
        {
            int requested = Math.Min(buffer.Length, Remaining());
            if (requested <= data.Length - head)
            {
                Array.Copy(data, head, buffer, 0, requested);
            }
            else
            {
                int part1 = data.Length - head;
                int part2 = requested - part1;
                Array.Copy(data, head, buffer, 0, part1);
                Array.Copy(data, 0, buffer, part1, part2);
            }
            head = (head + requested) % data.Length;
        }

        // To minimize the occurrence of suboptimal writing of data at the "end" of the buffer.
        private void Optimize()
        {
            if (IsEmpty())
            {
                head = 0;
                tail = 0;
            }
        }
    }
}
