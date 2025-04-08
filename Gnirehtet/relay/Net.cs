using System;
using System.Net;

namespace Genymobile.Gnirehtet.Relay
{
    public static class Net
    {

        public static IPAddress[] ToInetAddresses(params string[] addresses)
        {
            IPAddress[] result = new IPAddress[addresses.Length];
            for (int i = 0; i < result.Length; ++i)
            {
                result[i] = ToInetAddress(addresses[i]);
            }
            return result;
        }

        public static IPAddress ToInetAddress(string address)
        {
            if (IPAddress.TryParse(address, out IPAddress ipAddress))
            {
                return ipAddress;
            }
            throw new ArgumentException("Invalid address", nameof(address));
        }

        public static IPAddress ToInetAddress(byte[] raw)
        {
            try
            {
                return new IPAddress(raw);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Invalid byte array", nameof(raw), e);
            }
        }

        public static IPAddress ToInetAddress(int ipAddr)
        {
            byte[] ip = {
                (byte)(ipAddr >> 24),
                (byte)((ipAddr >> 16) & 0xff),
                (byte)((ipAddr >> 8) & 0xff),
                (byte)(ipAddr & 0xff)
            };
            return ToInetAddress(ip);
        }

        public static string ToString(IPEndPoint address)
        {
            return $"{address.Address}:{address.Port}";
        }

        public static string ToString(int ip, ushort port)
        {
            return ToString(new IPEndPoint(ToInetAddress(ip), port));
        }
    }
}
