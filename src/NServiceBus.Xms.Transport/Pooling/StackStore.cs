using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Timers;
using log4net;
using Timer = System.Timers.Timer;

namespace NServiceBus.Xms.Transport.Pooling
{
    public class StackStore<T> : IStore<T>, IDisposable
        where T:IExpirable
    {
        private readonly TimeSpan timeToLive;
        private ConcurrentStack<ExpirableItem<T>> stack = new ConcurrentStack<ExpirableItem<T>>();
        private readonly Timer timer;
        private readonly ManualResetEvent manualResetEvent = new ManualResetEvent(true);
        private static readonly ILog log = LogManager.GetLogger(typeof (StackStore<T>));

        public StackStore(bool start = true)
            : this(30.Seconds(), 60.Seconds(), start)
        {
        }

        public StackStore(TimeSpan timeToLive, TimeSpan cleanUpInterval, bool start = true)
        {
            this.timeToLive = timeToLive;
            timer = new Timer(cleanUpInterval.TotalMilliseconds);
            timer.AutoReset = true;
            timer.Elapsed += CleanUp;
            if (start) timer.Start();
        }

        public void CleanUp(object sender = null, ElapsedEventArgs elapsedEventArgs = null)
        {
            manualResetEvent.Reset();

            var now = Clock.UtcNow();
            var items = stack.OrderBy(x => x.Expiration).ToList();
            stack = new ConcurrentStack<ExpirableItem<T>>();
            int removed = 0;
            foreach (var expirable in items)
            {
                if (expirable.Expiration < now)
                {
                    log.Debug("Identfied item that has not been used for {0}. The item will be disposed.".FormatWith(timeToLive));
                    expirable.Expire();
                    ++removed;
                }
                else
                {
                    stack.Push(expirable);
                }
            }
            manualResetEvent.Set();
        }

        public T Fetch()
        {
            manualResetEvent.WaitOne();

            ExpirableItem<T> item;

            if (stack.TryPop(out item))
            {
                return item.Item;
            }
            return default(T);
        }

        public void Store(T item)
        {
            manualResetEvent.WaitOne();

            stack.Push(new ExpirableItem<T>(item, Clock.UtcNow().Add(timeToLive)));
        }

        public int Count
        {
            get
            {
                manualResetEvent.WaitOne();
                return stack.Count;
            }
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}