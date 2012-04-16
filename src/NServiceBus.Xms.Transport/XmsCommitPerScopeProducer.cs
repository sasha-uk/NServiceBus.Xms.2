using System;
using System.Transactions;
using IBM.XMS;
using log4net;

namespace NServiceBus.Xms.Transport
{
    public class XmsCommitPerScopeProducer : IXmsProducer, IEnlistmentNotification
    {
        private static readonly ILog log = LogManager.GetLogger(typeof (XmsCommitPerScopeProducer));
        private readonly IXmsProducer producer;
        private readonly ISession session;
        private readonly Action onTransactionCompleted;

        public XmsCommitPerScopeProducer(IXmsProducer producer, ISession session, Action onTransactionCompleted)
        {
            this.producer = producer;
            this.session = session;
            this.onTransactionCompleted = onTransactionCompleted;
            if (Transaction.Current == null)
            {
                throw new Exception("You cannot use XmsCommitPerScopeProducer when there is no transaction.");
            }
            log.Debug("Enlisting producer in global transaction scope.");
            Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
            Transaction.Current.TransactionCompleted += OnTransactionCompleted;
        }

        private void OnTransactionCompleted(object sender, TransactionEventArgs e)
        {
            onTransactionCompleted();
            e.Transaction.TransactionCompleted -= OnTransactionCompleted;
            producer.Dispose();
        }

        void IEnlistmentNotification.Commit(Enlistment enlistment)
        {
            session.Commit();
            enlistment.Done();
        }

        void IEnlistmentNotification.InDoubt(Enlistment enlistment)
        {
            session.Rollback();
            enlistment.Done();
        }

        void IEnlistmentNotification.Prepare(PreparingEnlistment preparingEnlistment)
        {
            preparingEnlistment.Prepared();
        }

        void IEnlistmentNotification.Rollback(Enlistment enlistment)
        {
            session.Rollback();
            enlistment.Done();
        }

        public void Dispose()
        {
            // do nothing, the transaction is going to do this.
        }

        public void Send(IBM.XMS.IMessage message)
        {
            producer.Send(message);
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