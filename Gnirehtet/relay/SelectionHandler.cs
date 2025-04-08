using System.Net.Sockets;

namespace Genymobile.Gnirehtet.Relay
{
    public interface ISelectionHandler
    {
        void OnReady(SelectionKey selectionKey);
    }
}
