using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using NUnit.Framework;

namespace NServiceBus.Xms.Transport.Tests
{
    [TestFixture]
    public class XmsProducerProviderTest
    {
        private XmsDestination destination;

        [SetUp]
        public void SetUp()
        {
            destination = Target.Input;
            XmsUtilities.Purge(destination);
        }

        [Test]
        public void ShouldSend100MessagesSingleThreaded()
        {
            int count = 100;
            var stop = Stopwatch.StartNew();
            using (var provider = new XmsProducerProvider(false))
            {
                for (int i = 0; i < count; i++)
                {
                    provider.SendTestMessage(destination);
                }
            }
            Console.WriteLine("Sent {0} messages single-threaded in {1}", count, stop.Elapsed);
            destination.AssertMessageCount(count);
        }

        [Test]
        public void ShouldSend100MessagesMultiThreaded()
        {
            int count = 100;
            var stop = Stopwatch.StartNew();
            using (var provider = new XmsProducerProvider(false))
            {
                var taskFactory = new TaskFactory();
                var tasks = new Task[count];

                for (int i = 0; i < count; i++)
                {
                    tasks[i] = taskFactory.StartNew(() => provider.SendTestMessage(destination));
                }
                Task.WaitAll(tasks);
                stop.Stop();
            }
            Console.WriteLine("Sent {0} messages multi-threaded in {1}", count, stop.Elapsed);
            destination.AssertMessageCount(count);
        }

        [Test]
        public void ShouldSend100MessagesMultiThreadedWithTransactions()
        {
            int count = 100;
            var stop = Stopwatch.StartNew();
            using (var provider = new XmsProducerProvider(true))
            {
                var taskFactory = new TaskFactory();
                var tasks = new Task[count];

                for (int i = 0; i < count; i++)
                {
                    tasks[i] = taskFactory.StartNew(
                        () =>
                        {
                            using(var scope = new TransactionScope(TransactionScopeOption.Required))
                            {
                                provider.SendTestMessage(destination);
                                scope.Complete();
                            }
                        });
                }
                Task.WaitAll(tasks.ToArray());
                stop.Stop();
            }
            Console.WriteLine("Sent {0} messages multi-threaded in {1}", count, stop.Elapsed);
            destination.AssertMessageCount(count);
        }

        [Test]
        public void ShouldRollbackOnlyMesagesSinceLastCommit()
        {
            using (var provider = new XmsProducerProvider(true))
            {
                using (var scope = new TransactionScope(TransactionScopeOption.Required))
                {
                    provider.SendTestMessage(destination);
                    // commit
                    scope.Complete();
                }
                using (var scope = new TransactionScope(TransactionScopeOption.Required))
                {
                    provider.SendTestMessage(destination);
                    // roll back
                }
            }

            destination.AssertMessageCount(1);
        }


        [Test]
        public void ShouldCommitMessageFromTransactionProviderEventWhenThereIsNoTransactionalScope()
        {
            using (var provider = new XmsProducerProvider(true))
            {
                provider.SendTestMessage(destination);
            }

            destination.AssertMessageCount(1);
        }

        [Test]
        public void ShouldCommitMessageFromNonTransactionProvider()
        {
            using (var provider = new XmsProducerProvider(false))
            {
                provider.SendTestMessage(destination);
            }

            destination.AssertMessageCount(1);
        }

        [Test]
        public void ShouldCommitMessageFromNonTransactionProviderEventWhenThereIsTransactionScope()
        {
            using (var provider = new XmsProducerProvider(false))
            {
                using (var scope = new TransactionScope(TransactionScopeOption.Required))
                {
                    provider.SendTestMessage(destination);
                }
            }

            destination.AssertMessageCount(1);
        }

        /// <summary>
        /// https://github.com/sasha-uk/NServieBus.Xms/issues/5
        /// </summary>
        [Test]
        public void Bug_AsPerIssue5()
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Required))
            {
                using (var provider = new XmsProducerProvider(true))
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Console.Write("Cycle {0}".FormatWith(i));

                        {
                            provider.SendTestMessage(destination);
                        }
                    }
                }
                scope.Complete();
            }
            destination.AssertMessageCount(20);
        }
    }
}