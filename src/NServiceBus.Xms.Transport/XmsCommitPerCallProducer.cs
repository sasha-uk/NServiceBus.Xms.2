using System;
using IBM.XMS;
using log4net;

namespace NServiceBus.Xms.Transport
{
    public class XmsCommitPerCallProducer : IXmsProducer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(XmsCommitPerCallProducer));
        private readonly IXmsProducer producer;
        private readonly ISession session;

        public XmsCommitPerCallProducer(IXmsProducer producer, ISession session)
        {
            this.producer = producer;
            this.session = session;
        }

        public void Dispose()
        {
            producer.Dispose();
        }

        public void Send(IBM.XMS.IMessage message)
        {
            try
            {
                producer.Send(message);
                log.Debug("Commiting per call transaction.");
                session.Commit();
            }
            catch (Exception)
            {
                log.Debug("Rolling back per call transaction.");
                session.Rollback();
                throw;
            }
        }

        public IBytesMessage CreateBytesMessage()
        {
            return producer.CreateBytesMessage();
        }

        public ITextMessage CreateTextMessage()
        {
            return producer.CreateTextMessage();
        }
    }
}