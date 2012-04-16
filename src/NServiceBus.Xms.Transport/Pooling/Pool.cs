using System;
using System.Threading;
using log4net;

namespace NServiceBus.Xms.Transport.Pooling
{
    public class Pool<T> : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof (Pool<T>));
        private bool isDisposed;
        private readonly Func<Pool<T>, T> factory;
        private readonly IStore<T> store;
        private readonly Semaphore sync;    

        public Pool(int size, Func<Pool<T>, T> factory, IStore<T> store)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException("size", size, "Argument 'size' must be greater than zero.");
            if (factory == null)
                throw new ArgumentNullException("factory");

            this.factory = factory;
            sync = new Semaphore(size, size);
            this.store = store;
        }

        public T Acquire()
        {
            sync.WaitOne();
            lock (store)
            {
                if (store.Count > 0)
                {
                    return store.Fetch();
                }
            }
            try
            {
                var instance = factory(this);
                return instance;
            }
            catch (Exception)
            {
                log.Warn("Factory method failed to produce a required instance.");
                sync.Release();
                throw;
            }
        }

        public void Release(T item)
        {
            if (!ReferenceEquals(item, null))
            {
                lock (store)
                {
                    log.Debug("Returning item back to the pool.");
                    store.Store(item);
                }
            }
            else
            {
                log.Debug("Null item returned back to the pool.");
            }
            sync.Release();
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            if (typeof (IDisposable).IsAssignableFrom(typeof (T)))
            {
                lock (store)
                {
                    while (store.Count > 0)
                    {
                        var disposable = (IDisposable) store.Fetch();
                        disposable.Dispose();
                    }
                }
            }
            sync.Close();
        }

        public bool IsDisposed
        {
            get { return isDisposed; }
        }
    }
}