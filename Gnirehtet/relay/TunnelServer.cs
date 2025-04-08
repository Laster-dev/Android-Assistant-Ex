using Gnirehtet.Relay;
using java.nio.channels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Genymobile.Gnirehtet.Relay
{
    public class TunnelServer
    {
        private static readonly string Tag = nameof(TunnelServer);

        private readonly List<Client> clients = new List<Client>();

        public TunnelServer(int port, Selector selector)
        {
            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            serverSocket.Listen(100);
            serverSocket.Blocking = false;

            // Handle incoming connections
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    while (true)
                    {
                        var socket = serverSocket.Accept();
                        socket.Blocking = false;

                        // Create new client and register it
                        var client = new Client(selector, socket, RemoveClient);
                        clients.Add(client);
                        Console.WriteLine($"{Tag}: Client #{client.Id} connected");
                    }
                }
                catch (IOException e)
                {
                    Console.Error.WriteLine($"{Tag}: Cannot accept client, {e.Message}");
                }
            });
        }

        private void RemoveClient(Client client)
        {
            clients.Remove(client);
            Console.WriteLine($"{Tag}: Client #{client.Id} disconnected");
        }

        public void CleanUp()
        {
            foreach (var client in clients)
            {
                client.CleanExpiredConnections();
            }
        }
    }
}
