using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using IBM.XMS;
using NUnit.Framework;

namespace NServiceBus.Xms.Transport.Tests.Manual
{
    [Ignore]
    [TestFixture]
    public class PerformanceTest
    {
        private XmsDestination destination;

        [SetUp]
        public void SetUp()
        {
            destination = Target.Input;
            XmsUtilities.Purge(destination);
        }

        [Test]
        public void Performance()
        {
            int count = 200;
            CreateTestMessagesUsingSameFactory(count);
            CreateTestMessagesUsingSameConnection(count);
            CreateTestMessagesUsingSameSession(count);
            CreateTestMessagesUsingSameQueue(count);
            CreateTestMessagesUsingSameProducer(count);
            CreateTestMessagesUsingConcurency(count);
            CreateTestMessagesUsingConcurencyWithDtc(count);
        }

        private void CreateTestMessagesUsingSameFactory(int count)
        {
            var stop = Stopwatch.StartNew();
            var factory = XmsUtilities.CreateConnectionFactory(destination);
            foreach (var i in Enumerable.Range(0, count))
            {
                using (var connection = factory.CreateConnection())
                {
                    using (ISession session = connection.CreateSession(false, AcknowledgeMode.AutoAcknowledge))
                    {
                        IDestination queue = session.CreateQueue(destination.Queue);
                        queue.SetIntProperty(XMSC.DELIVERY_MODE, XMSC.DELIVERY_NOT_PERSISTENT);

                        using (var producer = session.CreateProducer(queue))
                        {
                            ITextMessage message = session.CreateTextMessage("Hello XMS message." + DateTime.Now);
                            producer.Send(message);
                        }
                    }
                }
            }
            Console.WriteLine("CreateTestMessagesUsingSameFactory {0} messages in {1}", count, stop.Elapsed);
        }

        private void CreateTestMessagesUsingSameConnection(int count)
        {
            var stop = Stopwatch.StartNew();
            var factory = XmsUtilities.CreateConnectionFactory(destination);
            using (var connection = factory.CreateConnection())
            {
                foreach (var i in Enumerable.Range(0, count))
                {
                    using (ISession session = connection.CreateSession(false, AcknowledgeMode.AutoAcknowledge))
                    {
                        IDestination queue = session.CreateQueue(destination.Queue);
                        queue.SetIntProperty(XMSC.DELIVERY_MODE, XMSC.DELIVERY_NOT_PERSISTENT);

                        using (var producer = session.CreateProducer(queue))
                        {
                            ITextMessage message = session.CreateTextMessage("Hello XMS message." + DateTime.Now);
                            producer.Send(message);
                        }
                    }
                }
            }
            Console.WriteLine("CreateTestMessagesUsingSameConnection {0} messages in {1}", count, stop.Elapsed);
        }

        private void CreateTestMessagesUsingConcurencyWithDtc(int count)
        {
            var stop = Stopwatch.StartNew();
            using (var provider = new XmsProducerProvider(true))
            {
                var taskFactory = new TaskFactory();

                var tasks = new List<Task>();
                foreach (var i in Enumerable.Range(0, count))
                {
                    var task = taskFactory.StartNew(
                        () =>
                            {
                                using(var scope = new TransactionScope(TransactionScopeOption.Required))
                                {
                                    var producer = provider.GetProducer(destination);
                                    var msg = producer.CreateTextMessage();
                                    msg.Text = "Hello XMS message." + DateTime.Now;
                                    producer.Send(msg);
                                    scope.Complete();
                                }
                            });
                    tasks.Add(task);
                }
                Task.WaitAll(tasks.ToArray());

                var faulted = tasks.Where(x => x.IsFaulted).ToList();

                Assert.That(faulted.Count, Is.EqualTo(0));
            }
            Console.WriteLine("CreateTestMessagesUsingConcurencyWithDtc {0} messages in {1}", count, stop.Elapsed);
        }

        private void CreateTestMessagesUsingConcurency(int count)
        {
            var stop = Stopwatch.StartNew();
            using (var provider = new XmsProducerProvider(false))
            {
                var taskFactory = new TaskFactory();

                var tasks = new List<Task>();
                foreach (var i in Enumerable.Range(0, count))
                {
                    var task = taskFactory.StartNew(
                        () =>
                            {
                                var producer = provider.GetProducer(destination);
                                var msg = producer.CreateTextMessage();
                                msg.Text = "Hello XMS message." + DateTime.Now;
                                producer.Send(msg);
                            });
                    tasks.Add(task);
                }
                Task.WaitAll(tasks.ToArray());

                var faulted = tasks.Where(x => x.IsFaulted).ToList();

                Assert.That(faulted.Count, Is.EqualTo(0));
            }
            Console.WriteLine("CreateTestMessagesUsingConcurency {0} messages in {1}", count, stop.Elapsed);
        }


        private void CreateTestMessagesUsingSameSession(int count)
        {
            var stop = Stopwatch.StartNew();
            var factory = XmsUtilities.CreateConnectionFactory(destination);
            using (var connection = factory.CreateConnection())
            {
                using (ISession session = connection.CreateSession(false, AcknowledgeMode.AutoAcknowledge))
                {
                    foreach (var i in Enumerable.Range(0, count))
                    {
                        IDestination queue = session.CreateQueue(destination.Queue);
                        queue.SetIntProperty(XMSC.DELIVERY_MODE, XMSC.DELIVERY_NOT_PERSISTENT);

                        using (var producer = session.CreateProducer(queue))
                        {
                            ITextMessage message = session.CreateTextMessage("Hello XMS message." + DateTime.Now);
                            producer.Send(message);
                        }
                    }
                }
            }
            Console.WriteLine("CreateTestMessagesUsingSameSession {0} messages in {1}", count, stop.Elapsed);
        }

        private void CreateTestMessagesUsingSameQueue(int count)
        {
            var stop = Stopwatch.StartNew();
            var factory = XmsUtilities.CreateConnectionFactory(destination);
            using (var connection = factory.CreateConnection())
            {
                using (ISession session = connection.CreateSession(false, AcknowledgeMode.AutoAcknowledge))
                {
                    IDestination queue = session.CreateQueue(destination.Queue);
                    queue.SetIntProperty(XMSC.DELIVERY_MODE, XMSC.DELIVERY_NOT_PERSISTENT);
                    foreach (var i in Enumerable.Range(0, count))
                    {
                        using (var producer = session.CreateProducer(queue))
                        {
                            ITextMessage message = session.CreateTextMessage("Hello XMS message." + DateTime.Now);
                            producer.Send(message);
                        }
                    }
                }
            }

            Console.WriteLine("CreateTestMessagesUsingSameQueue {0} messages in {1}", count, stop.Elapsed);
        }

        private void CreateTestMessagesUsingSameProducer(int count)
        {
            var stop = Stopwatch.StartNew();
            var factory = XmsUtilities.CreateConnectionFactory(destination);
            using (var connection = factory.CreateConnection())
            {
                using (ISession session = connection.CreateSession(false, AcknowledgeMode.AutoAcknowledge))
                {
                    IDestination queue = session.CreateQueue(destination.Queue);
                    queue.SetIntProperty(XMSC.DELIVERY_MODE, XMSC.DELIVERY_NOT_PERSISTENT);

                    using (var producer = session.CreateProducer(queue))
                    {
                        foreach (var i in Enumerable.Range(0, count))
                        {
                            ITextMessage message = session.CreateTextMessage("Hello XMS message." + DateTime.Now);
                            producer.Send(message);
                        }
                    }
                }
            }

            Console.WriteLine("CreateTestMessagesUsingSameProducer {0} messages in {1}", count, stop.Elapsed);
        }
    }
}