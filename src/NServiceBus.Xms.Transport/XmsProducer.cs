using System;
using IBM.XMS;
using log4net;

namespace NServiceBus.Xms.Transport
{
    public class XmsProducer : IXmsProducer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(XmsProducer));
        private readonly XmsDestination destination;
        private readonly bool transactional;
        private IConnectionFactory factory;
        private IConnection connection;
        private ISession session;
        private IDestination queue;
        private IMessageProducer producer;

        public XmsProducer(XmsDestination destination, bool transactional)
        {
            this.destination = destination;
            this.transactional = transactional;
            log.Debug("New physical producer created. About to connect.");
            Connect();
            log.Debug("New physical producer successfully connected.");
        }

        public void Send(IBM.XMS.IMessage message)
        {
            producer.Send(message);
        }

        public IBytesMessage CreateBytesMessage()
        {
            return session.CreateBytesMessage();
        }

        public ITextMessage CreateTextMessage()
        {
            return session.CreateTextMessage();
        }

        public void Dispose()
        {
            Disconnect();
        }

        public ISession Session { get { return session; } }

        private void Disconnect()
        {
            log.Debug("Physical producer about to be disconnected.");

            if (producer != null) producer.Dispose();
            if (queue != null) queue.Dispose();
            if (session != null) session.Dispose();
            if (connection != null) connection.Dispose();

            producer = null;
            queue = null;
            session = null;
            connection = null;

            log.Debug("Physical producer successfully disconnected.");
        }

        private void Connect()
        {
            factory = XmsUtilities.CreateConnectionFactory(destination);
            connection = factory.CreateConnection();
            connection.ExceptionListener += OnError;
            session = connection.CreateSession(transactional, AcknowledgeMode.AutoAcknowledge);
            queue = session.CreateQueue(destination.Queue);
            queue.SetIntProperty(XMSC.DELIVERY_MODE, XMSC.DELIVERY_PERSISTENT);
            producer = session.CreateProducer(queue);
        }

        private void OnError(Exception ex)
        {
            log.Error(ex);
        }

    }
}