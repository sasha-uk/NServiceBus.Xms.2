using System;
using IBM.XMS;
using log4net;

namespace NServiceBus.Xms.Transport
{
    public class XmsConsumer : IXmsConsumer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(XmsConsumer));
        private readonly XmsDestination destination;
        private readonly bool transactional;
        private IConnectionFactory factory;
        private IConnection connection;
        private ISession session;
        private IDestination queue;
        private IMessageConsumer consumer;

        public XmsConsumer(XmsDestination destination, bool transactional)
        {
            this.destination = destination;
            this.transactional = transactional;
            log.Debug("New physical consumer created. About to connect.");
            Connect();
            log.Debug("New physical consumer successfully connected.");
        }

        public IBM.XMS.IMessage ReceiveNoWait()
        {
            var message = consumer.ReceiveNoWait();
            return message;
        }

        public IBM.XMS.IMessage Receive(int milisecondsToWaitForMessage)
        {
            var message = consumer.Receive(milisecondsToWaitForMessage);
            return message;
        }

        public ISession Session { get { return session; } }

        public void Dispose()
        {
            Disconnect();
        }

        private void Disconnect()
        {
            log.Debug("Physical consumer about to be disconnected.");

            if (connection != null) connection.Stop();
            if (consumer != null) consumer.Dispose();
            if (queue != null) queue.Dispose();
            if (session != null) session.Dispose();
            if (connection != null) connection.Dispose();

            consumer = null;
            queue = null;
            session = null;
            connection = null;

            log.Debug("Physical consumer successfully disconnected.");
        }

        private void Connect()
        {
            factory = XmsUtilities.CreateConnectionFactory(destination);
            connection = factory.CreateConnection();
            connection.ExceptionListener += OnError;
            session = connection.CreateSession(transactional, AcknowledgeMode.AutoAcknowledge);
            queue = session.CreateQueue(destination.Queue);
            queue.SetIntProperty(XMSC.DELIVERY_MODE,
                                 transactional ? XMSC.DELIVERY_PERSISTENT : XMSC.DELIVERY_NOT_PERSISTENT);
            consumer = session.CreateConsumer(queue);
            connection.Start();
        }

        private void OnError(Exception ex)
        {
            log.Error(ex);
        }
    }
}