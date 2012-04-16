using System;

namespace NServiceBus.Xms.Transport.Pooling
{
    public class ExpirableItem<T> where T : IExpirable
    {
        public ExpirableItem(T item, DateTime expiration)
        {
            Item = item;
            Expiration = expiration;
        }

        public DateTime Expiration { get; private set; }
        public T Item { get; private set; }

        public void Expire()
        {
            Item.Expire();
        }
    }
}