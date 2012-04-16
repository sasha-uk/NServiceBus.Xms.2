using System.IO;
using System.Threading.Tasks;
using NServiceBus.Unicast.Transport;
using NUnit.Framework;

namespace NServiceBus.Xms.Transport.Tests
{
    public static class TestExtensions
    {
        public static string SendTestMessage(this XmsProducerProvider provider, XmsDestination destination)
        {
            using(var producer = provider.GetProducer(destination))
            {
                return producer.SendTestMessage(destination);
            }
        }

        public static string SendTestMessage(this IXmsProducer producer, XmsDestination destination)
        {
            var msg = producer.CreateTextMessage();
            msg.Text = "message";
            producer.Send(msg);
            return msg.JMSMessageID;
        }

        public static void FillWith(this XmsDestination destination, int count)
        {
            using (var provider = new XmsProducerProvider(false))
            {
                var taskFactory = new TaskFactory();
                var tasks = new Task[count];

                for (int i = 0; i < count; i++)
                {
                    tasks[i] = taskFactory.StartNew(() => provider.SendTestMessage(destination));
                }
                Task.WaitAll(tasks);
            }
        }


        public static void AssertMessageCount(this XmsDestination destination, int expected)
        {
            var actual = XmsUtilities.GetCurrentQueueDebth(destination);

            Assert.That(actual, Is.EqualTo(expected),
                        "Unexpected number of messages in {0}. Expected {1} Actual {2}".FormatWith(destination, expected,
                                                                                                   actual));
        }

        public static TransportMessage WithBody(this TransportMessage message)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.WriteLine("foo message");
            writer.Flush();
            message.BodyStream = stream;
            return message;
        }
    }
}