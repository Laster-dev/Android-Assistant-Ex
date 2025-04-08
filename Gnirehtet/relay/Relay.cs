using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Genymobile.Gnirehtet.Relay
{
    public class Relay
    {
        private static readonly string TAG = typeof(Relay).Name;

        private const int CLEANING_INTERVAL = 60 * 1000;

        private readonly int port;

        public Relay(int port)
        {
            this.port = port;
        }

        public void Run()
        {
            Selector selector = new Selector();

            // Will register the socket on the selector
            TunnelServer tunnelServer = new TunnelServer(port, selector);

            Log.Info(TAG, "Relay server started");

            long nextCleaningDeadline = DateTimeOffset.Now.ToUnixTimeMilliseconds() + UDPConnection.IDLE_TIMEOUT;

            while (true)
            {
                long timeout = Math.Max(0, nextCleaningDeadline - DateTimeOffset.Now.ToUnixTimeMilliseconds());
                selector.Select(timeout);
                HashSet<SelectionKey> selectedKeys = selector.SelectedKeys;

                long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (now >= nextCleaningDeadline || selectedKeys.Count == 0)
                {
                    tunnelServer.CleanUp();
                    nextCleaningDeadline = now + CLEANING_INTERVAL;
                }

                foreach (SelectionKey selectedKey in selectedKeys)
                {
                    SelectionHandler selectionHandler = selectedKey.Attachment as SelectionHandler;
                    selectionHandler?.OnReady(selectedKey);
                }

                // By design, we handled everything
                selectedKeys.Clear();
            }
        }
    }
}
