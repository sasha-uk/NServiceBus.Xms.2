using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NServiceBus.Xms.Transport.Tests
{
    [TestFixture]
    public class XmsConsumerProviderTest
    {
        private XmsDestination destination;

        [SetUp]
        public void SetUp()
        {
            destination = Target.Input;
            XmsUtilities.Purge(destination);
        }

        private static int actual;

        [Test]
        public void ShouldRead100MessagesMultiThreaded()
        {
            int expected = 100;
            destination.FillWith(expected);
            
            var stop = Stopwatch.StartNew();
            using (var provider = new XmsConsumerProvider(false))
            {
                var taskFactory = new TaskFactory();
                var tasks = new Task[expected];

                for (int i = 0; i < expected; i++)
                {
                    tasks[i] = taskFactory.StartNew(
                        () =>
                            {
                                using(var consumer = provider.GetConsumer(destination))
                                {
                                    var message = consumer.ReceiveNoWait();
                                    if (message != null) Interlocked.Increment(ref actual);
                                }
                            });
                }
                Task.WaitAll(tasks.ToArray());
                stop.Stop();
            }
            Console.WriteLine("Received {0} messages multi-threaded in {1}", expected, stop.Elapsed);
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}