namespace NServiceBus.Xms.Transport.Pooling
{
    public interface IStore<T>
    {
        T Fetch();
        void Store(T item);
        int Count { get; }
    }
}
