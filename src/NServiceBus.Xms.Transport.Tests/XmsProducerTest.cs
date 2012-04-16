using System;
using System.Diagnostics;
using System.Linq;
using IBM.XMS;
using NUnit.Framework;

namespace NServiceBus.Xms.Transport.Tests
{
    [TestFixture]
    public class XmsProducerTest
    {
        private XmsDestination destination;

        [SetUp]
        public void SetUp()
        {
            destination = Target.Input;
            XmsUtilities.Purge(destination);
        }

        [Test]
        public void ShouldSendOneMessage()
        {
            using (var producer = new XmsProducer(destination, false))
            {
                var msg = producer.CreateTextMessage();
                msg.Text = "message";
                producer.Send(msg);
            }
            var after = XmsUtilities.GetCurrentQueueDebth(destination);

            Assert.That(after, Is.EqualTo(1));
        }

        [Test]
        public void ShouldSend100MessagesSingleThreaded()
        {
            int count = 100;
            var stop = Stopwatch.StartNew();
            using (var producer = new XmsProducer(destination, false))
            {
                foreach (var i in Enumerable.Range(0, count))
                {
                    var msg = producer.CreateTextMessage();
                    msg.Text = "message";
                    producer.Send(msg);
                }
                stop.Stop();
            }
            var after = XmsUtilities.GetCurrentQueueDebth(destination);
            Console.WriteLine("Sent {0} messages single-threaded in {1}", count, stop.Elapsed);
            Assert.That(after, Is.EqualTo(count));
        }

        [Test]
        public void CopyExistingMessageOntoDifferentQueue_ThisProofsThatMessageDoesNotHaveSessionAfinity()
        {
            using (var producer = new XmsProducerProvider(false))
            {
                producer.SendTestMessage(destination);
            }
            IBM.XMS.IMessage message;
            using (var consumer = new XmsConsumerProvider(false))
            {
                message = consumer.GetConsumer(destination).ReceiveNoWait();
            }
            using (var producer = new XmsProducerProvider(false))
            {
                producer.GetProducer(Target.Error).Send(message);
            }
        }
    }
}