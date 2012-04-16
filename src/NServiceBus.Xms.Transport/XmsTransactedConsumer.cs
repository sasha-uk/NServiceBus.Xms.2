using System;
using System.Transactions;
using IBM.XMS;
using log4net;

namespace NServiceBus.Xms.Transport
{
    public class XmsTransactedConsumer : IEnlistmentNotification, IXmsConsumer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(XmsTransactedConsumer));
        private readonly IXmsConsumer consumer;
        private readonly ISession session;

        public XmsTransactedConsumer(IXmsConsumer consumer, ISession session)
        {
            this.consumer = consumer;
            this.session = session;
            if (Transaction.Current == null)
            {
                throw new Exception("You cannot use XmsTransactedConsumer when there is no transaction.");
            }
            log.Debug("Enlisting consumer in global transaction scope.");
            Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
            Transaction.Current.TransactionCompleted += OnTransactionCompleted;
        }

        public XmsTransactedConsumer(XmsConsumer consumer)
            : this(consumer, consumer.Session)
        {
        }

        private void OnTransactionCompleted(object sender, TransactionEventArgs e)
        {
            e.Transaction.TransactionCompleted -= OnTransactionCompleted;
            consumer.Dispose();
        }

        public IBM.XMS.IMessage ReceiveNoWait()
        {
            return consumer.ReceiveNoWait();
        }

        public IBM.XMS.IMessage Receive(int milisecondsToWaitForMessage)
        {
            return consumer.Receive(milisecondsToWaitForMessage);
        }
     
        private void OnError(Exception ex)
        {
            Console.WriteLine(ex);
        }

        void IEnlistmentNotification.Commit(Enlistment enlistment)
        {
            session.Commit();
        }

        void IEnlistmentNotification.InDoubt(Enlistment enlistment)
        {
            session.Rollback();
        }

        void IEnlistmentNotification.Prepare(PreparingEnlistment preparingEnlistment)
        {
            preparingEnlistment.Prepared();
        }

        void IEnlistmentNotification.Rollback(Enlistment enlistment)
        {
            session.Rollback();
        }

        public void Dispose()
        {
            // do nothing, the transaction is going to do this.
        }
    }
}