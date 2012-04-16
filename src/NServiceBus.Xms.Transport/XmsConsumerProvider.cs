using System;
using System.Collections.Concurrent;
using System.Transactions;
using NServiceBus.Xms.Transport.Pooling;
using log4net;

namespace NServiceBus.Xms.Transport
{
    public class XmsConsumerProvider : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(XmsProducerProvider));
        private readonly bool transactional;
        private readonly ConcurrentDictionary<XmsDestination, Pool<XmsPooledConsumer>> poolsByDestination =
            new ConcurrentDictionary<XmsDestination, Pool<XmsPooledConsumer>>();
        private readonly object locker = new object();

        public XmsConsumerProvider(bool transactional)
        {
            this.transactional = transactional;
        }

        public IXmsConsumer GetConsumer(XmsDestination destination)
        {
            Pool<XmsPooledConsumer> pool;
            if (!poolsByDestination.TryGetValue(destination, out pool))
            {
                lock (locker)
                {
                    pool = poolsByDestination.GetOrAdd(destination, PoolFactory);
                }
            }
            var pooled = pool.Acquire();
            if (IsInTransaction)
            {
                log.Debug("Detected transaction scope. Wrapping the consumer in transacted consumer.");

                // not very nice, we need ot get the session somehow
                var producer = (XmsConsumer)pooled.Consumer;
                return new XmsTransactedConsumer(pooled, producer.Session);
            }
            return pooled;
        }

        private Pool<XmsPooledConsumer> PoolFactory(XmsDestination destination)
        {
            log.Info("Going to create new consumer pool for destination {0}".FormatWith(destination));
            //TODO: make this configurable
            var store = new StackStore<XmsPooledConsumer>(60.Seconds(), 30.Seconds());
            var pool = new Pool<XmsPooledConsumer>(10, p => PlainProducerFactory(p, destination), store);
            return pool;
        }

        private XmsPooledConsumer PlainProducerFactory(Pool<XmsPooledConsumer> pool, XmsDestination destination)
        {
            log.Info("Going to create new plain consumer for destination {0}".FormatWith(destination));
            var producer = new XmsConsumer(destination, transactional);
            var pooled = new XmsPooledConsumer(pool, producer);
            return pooled;
        }

        private bool IsInTransaction
        {
            get { return Transaction.Current != null; }
        }

        public void Dispose()
        {
            foreach (var pair in poolsByDestination)
            {
                pair.Value.Dispose();
            }
            poolsByDestination.Clear();
        }
    }
}