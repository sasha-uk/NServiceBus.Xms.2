using System.Transactions;
using IBM.XMS;
using NUnit.Framework;

namespace NServiceBus.Xms.Transport.Tests
{
    [TestFixture]
    public class XmsConsumerTest
    {
        private XmsDestination destination;
        private const string expected = "foo";

        [SetUp]
        public void SetUp()
        {
            destination = Target.Input;
            XmsUtilities.Purge(destination);

            using (var producer = new XmsProducer(destination, false))
            {
                var msg = producer.CreateBytesMessage();
                msg.WriteUTF(expected);
                producer.Send(msg);
            }
        }

        [Test]
        public void ShouldReceiveMessageWithNoWait()
        {
            string actual = null;
            using (var consumer = new XmsConsumer(destination, false))
            {
                var msg = (IBytesMessage) consumer.ReceiveNoWait();
                actual = msg.ReadUTF();
            }
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(XmsUtilities.GetCurrentQueueDebth(destination), Is.EqualTo(0), "Queue should be empty");
        }

        [Test]
        public void ShouldReceiveMessage()
        {
            string actual = null;
            using (var consumer = new XmsConsumer(destination, false))
            {
                var msg = (IBytesMessage) consumer.Receive(10);
                actual = msg.ReadUTF();
            }
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(XmsUtilities.GetCurrentQueueDebth(destination), Is.EqualTo(0), "Queue should be empty");
        }


        [Test]
        public void ShouldRemoveMessageFromQueueWhenGlobalTrasactionScopeCompleteted()
        {
            string actual = null;

            using (var scope = new TransactionScope(TransactionScopeOption.Required))
            {
                using (var consumer = new XmsTransactedConsumer(new XmsConsumer(destination, true)))
                {
                    var msg = (IBytesMessage) consumer.ReceiveNoWait();
                    actual = msg.ReadUTF();
                    Assert.That(msg, Is.Not.Null);
                    scope.Complete();
                }
            }

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(XmsUtilities.GetCurrentQueueDebth(destination), Is.EqualTo(0), "Queue should be empty");
        }

        [Test]
        public void ShouldReturnMessageBackOnTheQueueWhenGlobalTrasactionScopeNotCompleted()
        {
            string actual = null;
            using (var scope = new TransactionScope(TransactionScopeOption.Required))
            {
                using (var consumer = new XmsTransactedConsumer(new XmsConsumer(destination, true)))
                {
                    var msg = (IBytesMessage) consumer.ReceiveNoWait();
                    actual = msg.ReadUTF();
                    Assert.That(msg, Is.Not.Null);
                }
            }

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(XmsUtilities.GetCurrentQueueDebth(destination), Is.EqualTo(1),
                        "Message should be returned back to the queue.");
        }
    }
}