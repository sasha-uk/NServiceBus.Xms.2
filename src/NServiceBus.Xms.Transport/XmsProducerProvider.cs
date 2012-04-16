using System;
using System.Collections.Concurrent;
using System.Transactions;
using NServiceBus.Xms.Transport.Pooling;
using log4net;

namespace NServiceBus.Xms.Transport
{
    public class XmsProducerProvider : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(XmsProducerProvider));
        private readonly bool transactional;
        private readonly ConcurrentDictionary<string, XmsCommitPerScopeProducer> scopedProducers = new ConcurrentDictionary<string, XmsCommitPerScopeProducer>();
        private readonly ConcurrentDictionary<XmsDestination, Pool<XmsPooledProducer>> poolsByDestination = new ConcurrentDictionary<XmsDestination, Pool<XmsPooledProducer>>();
        private readonly object locker = new object();

        public XmsProducerProvider(bool transactional)
        {
            this.transactional = transactional;
        }

        public IXmsProducer GetProducer(XmsDestination destination)
        {
            Pool<XmsPooledProducer> pool;
            if (!poolsByDestination.TryGetValue(destination, out pool))
            {
                lock (locker)
                {
                    pool = poolsByDestination.GetOrAdd(destination, PoolFactory);
                }
            }

            if (IsInTransaction && transactional)
            {
                log.Debug("Detected transaction scope on transactional provider. Using per scope producer.");

                // here we need to keep track of current instances within the current transaction scope
                // so we can return the same producers for given current thread, transaction and destination 
                var key = "{0}|{1}".FormatWith(Transaction.Current.TransactionInformation.LocalIdentifier, destination);

                if (!scopedProducers.ContainsKey(key))
                {
                    log.Debug("Detected that there is no pending scoped producer for {0}. Acquiring a new instance.".FormatWith(key));
                    var pooled = pool.Acquire();
                    var producer = (XmsProducer)pooled.Producer;

                    return scopedProducers.GetOrAdd(key, k => new XmsCommitPerScopeProducer(pooled, producer.Session,
                        () =>
                        {
                            log.Debug("Removing pending scoped producer for {0}.".FormatWith(key));
                            XmsCommitPerScopeProducer _;
                            scopedProducers.TryRemove(key, out _);
                        }));
                }
                log.Debug("Detected pending scoped producer for {0}. Reusing the existing instance.".FormatWith(key));
                return scopedProducers[key];
            }
            if (transactional)
            {
                var pooled = pool.Acquire();
                log.Debug("No transaction scope on transactional provider. Using per call producer.");
                var producer = (XmsProducer)pooled.Producer;
                return new XmsCommitPerCallProducer(pooled, producer.Session);
            }
            return pool.Acquire();
        }

        private Pool<XmsPooledProducer> PoolFactory(XmsDestination destination)
        {
            log.Info("Going to create new producer pool for destination {0}".FormatWith(destination));
            //TODO: make this configurable
            var store = new StackStore<XmsPooledProducer>(60.Seconds(), 30.Seconds());
            var pool = new Pool<XmsPooledProducer>(10, p => PlainProducerFactory(p, destination), store);
            return pool;
        }

        private XmsPooledProducer PlainProducerFactory(Pool<XmsPooledProducer> pool, XmsDestination destination)
        {
            log.Info("Going to create new plain producer for destination {0}".FormatWith(destination));
            var producer = new XmsProducer(destination, transactional);
            var pooled = new XmsPooledProducer(pool, producer);
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