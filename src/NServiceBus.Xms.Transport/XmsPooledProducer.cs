using System;
using IBM.XMS;
using NServiceBus.Xms.Transport.Pooling;
using log4net;

namespace NServiceBus.Xms.Transport
{
    public class XmsPooledProducer : IXmsProducer, IExpirable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof (XmsPooledProducer));
        private readonly Pool<XmsPooledProducer> pool;
        private readonly IXmsProducer producer;
        private bool faulted;

        public XmsPooledProducer(Pool<XmsPooledProducer> pool, IXmsProducer producer)
        {
            this.pool = pool;
            this.producer = producer;
        }

        public IXmsProducer Producer
        {
            get { return producer; }
        }

        public void Send(IBM.XMS.IMessage message)
        {
            WithErrorTracking(
                () =>
                    {
                        producer.Send(message);
                        return true;
                    });
        }

        public IBytesMessage CreateBytesMessage()
        {
            return WithErrorTracking(() => producer.CreateBytesMessage());
        }

        public ITextMessage CreateTextMessage()
        {
            return WithErrorTracking(() => producer.CreateTextMessage());
        }


        private T WithErrorTracking<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch
            {
                faulted = true;
                log.Warn("Detected an error with this MQ connection. It will be disposed of and replaced with new one at the nearest oportunity.");
                throw;
            }
        }

        public void Dispose()
        {
            if (pool.IsDisposed)
            {
                producer.Dispose();
                return;
            }

            if (faulted)
            {
                pool.Release(null);
                return;
            }
            
            pool.Release(this);
        }

        public void Expire()
        {
            producer.Dispose();
        }
    }
}