namespace Gnirehtet.Relay
{
    public interface CloseListener<T>
    {
        void OnClosed(T obj);
    }
}