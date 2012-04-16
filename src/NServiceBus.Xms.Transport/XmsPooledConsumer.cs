using System;
using NServiceBus.Xms.Transport.Pooling;
using log4net;

namespace NServiceBus.Xms.Transport
{
    public class XmsPooledConsumer : IXmsConsumer, IExpirable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(XmsPooledConsumer));
        private readonly Pool<XmsPooledConsumer> pool;
        private readonly IXmsConsumer consumer;
        private bool faulted;

        public XmsPooledConsumer(Pool<XmsPooledConsumer> pool, IXmsConsumer consumer)
        {
            this.pool = pool;
            this.consumer = consumer;
        }

        public IXmsConsumer Consumer
        {
            get { return consumer; }
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
                consumer.Dispose();
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
            consumer.Dispose();
        }

        public IBM.XMS.IMessage ReceiveNoWait()
        {
            return WithErrorTracking(() => consumer.ReceiveNoWait());
        }

        public IBM.XMS.IMessage Receive(int milisecondsToWaitForMessage)
        {
            return WithErrorTracking(() => consumer.Receive(milisecondsToWaitForMessage));
        }
    }
}