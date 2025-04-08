using Genymobile.Gnirehtet.Relay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Gnirehtet
{
    public class AdbMonitor
    {
        public interface IAdbDevicesCallback
        {
            void OnNewDeviceConnected(string serial);
        }

        private static readonly string TAG = nameof(AdbMonitor);
        private const int ADBD_PORT = 5037;

        private const string TRACK_DEVICES_REQUEST = "0012host:track-devices";
        private const int BUFFER_SIZE = 1024;
        private const int LENGTH_FIELD_SIZE = 4;
        private const int OKAY_SIZE = 4;
        private const long RETRY_DELAY_ADB_DAEMON_OK = 1000;
        private const long RETRY_DELAY_ADB_DAEMON_KO = 5000;

        private List<string> connectedDevices = new List<string>();
        private readonly IAdbDevicesCallback callback;
        private static readonly byte[] BUFFER = new byte[BUFFER_SIZE]; // 静态缓冲区，用于避免频繁分配
        private readonly byte[] socketBuffer = new byte[BUFFER_SIZE];

        public AdbMonitor(IAdbDevicesCallback callback)
        {
            this.callback = callback;
        }

        public void Monitor()
        {
            while (true)
            {
                try
                {
                    TrackDevices();
                }
                catch (Exception e)
                {
                    Log.E(TAG, "Failed to monitor adb devices", e);
                    RepairAdbDaemon();
                }
            }
        }

        private void TrackDevices()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Connect(new IPEndPoint(IPAddress.Loopback, ADBD_PORT));
                TrackDevicesOnChannel(socket);
            }
        }

        private void TrackDevicesOnChannel(Socket channel)
        {
            Array.Clear(socketBuffer, 0, socketBuffer.Length);
            WriteRequest(channel, TRACK_DEVICES_REQUEST);

            // 检查 daemon 是否返回 "OKAY"
            if (!ConsumeOkay(channel))
            {
                return;
            }

            while (true)
            {
                string packet = NextPacket(channel);
                HandlePacket(packet);
            }
        }

        private static void WriteRequest(Socket channel, string request)
        {
            byte[] requestBytes = Encoding.ASCII.GetBytes(request);
            channel.Send(requestBytes);
        }

        private bool ConsumeOkay(Socket channel)
        {
            int bytesRead;
            int totalRead = 0;

            while ((bytesRead = channel.Receive(socketBuffer, totalRead, socketBuffer.Length - totalRead, SocketFlags.None)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead < OKAY_SIZE)
                {
                    continue; // 数据不足
                }

                string text = Encoding.ASCII.GetString(socketBuffer, 0, OKAY_SIZE);
                Array.Copy(socketBuffer, OKAY_SIZE, socketBuffer, 0, totalRead - OKAY_SIZE);
                Array.Clear(socketBuffer, totalRead - OKAY_SIZE, socketBuffer.Length - (totalRead - OKAY_SIZE));
                return text == "OKAY";
            }
            return false;
        }

        private string NextPacket(Socket channel)
        {
            string packet;
            int position = 0;

            while ((packet = ReadPacket(socketBuffer, ref position)) == null)
            {
                FillBufferFrom(channel, ref position);
            }
            Array.Clear(socketBuffer, 0, position); // 清空已处理的数据
            return packet;
        }

        private void FillBufferFrom(Socket channel, ref int position)
        {
            int bytesRead = channel.Receive(socketBuffer, position, socketBuffer.Length - position, SocketFlags.None);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("ADB daemon closed the track-devices connection");
            }
            position += bytesRead;
        }

        private static string ReadPacket(byte[] input, ref int position)
        {
            if (position < LENGTH_FIELD_SIZE)
            {
                return null;
            }

            int length = ParseLength(input);
            if (length > BUFFER.Length)
            {
                throw new ArgumentException($"Packet size should not be that big: {length}");
            }

            if (position < LENGTH_FIELD_SIZE + length)
            {
                return null; // 数据不足
            }

            string packet = Encoding.UTF8.GetString(input, LENGTH_FIELD_SIZE, length);
            position -= (LENGTH_FIELD_SIZE + length);
            Array.Copy(input, LENGTH_FIELD_SIZE + length, input, 0, position);
            return packet;
        }

        private void HandlePacket(string packet)
        {
            List<string> currentConnectedDevices = ParseConnectedDevices(packet);
            foreach (string serial in currentConnectedDevices)
            {
                if (!connectedDevices.Contains(serial))
                {
                    callback.OnNewDeviceConnected(serial);
                }
            }
            connectedDevices = currentConnectedDevices;
        }

        private static List<string> ParseConnectedDevices(string packet)
        {
            var list = new List<string>();
            foreach (string line in packet.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] tokens = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 2 && tokens[1] == "device")
                {
                    list.Add(tokens[0]);
                }
            }
            return list;
        }

        private static int ParseLength(byte[] data)
        {
            if (data.Length < LENGTH_FIELD_SIZE)
            {
                throw new ArgumentException("Length field must be at least 4 bytes length");
            }

            int result = 0;
            for (int i = 0; i < LENGTH_FIELD_SIZE; i++)
            {
                char c = (char)data[i];
                result = (result << 4) + Convert.ToInt32(c.ToString(), 16);
            }
            return result;
        }

        private static void RepairAdbDaemon()
        {
            if (StartAdbDaemon())
            {
                Sleep(RETRY_DELAY_ADB_DAEMON_OK);
            }
            else
            {
                Sleep(RETRY_DELAY_ADB_DAEMON_KO);
            }
        }

        private static bool StartAdbDaemon()
        {
            Log.I(TAG, "Restarting adb daemon");
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "adb",
                        Arguments = "start-server",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Log.E(TAG, "Could not restart adb daemon (exited on error)");
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Log.E(TAG, "Could not restart adb daemon", e);
                return false;
            }
        }

        private static void Sleep(long delay)
        {
            try
            {
                Thread.Sleep((int)delay);
            }
            catch (ThreadInterruptedException)
            {
                // 忽略中断
            }
        }
    }
}