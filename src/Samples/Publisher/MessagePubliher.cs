using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Messages;
using NServiceBus;
using NServiceBus.Xms.Transport;
using log4net;

namespace Publisher
{
    public class MessagePubliher : IWantToRunAtStartup
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof (MessagePubliher));

        private readonly IBus bus;

        public MessagePubliher(IBus bus)
        {
            this.bus = bus;
        }

        public void Run()
        {
            string input = Console.ReadLine();
            while (input != "e")
            {
                if(input.Contains('/'))
                {
                    var args = input.Split('/');
                    int threads = Convert.ToInt32(args[0]);
                    int perThread = Convert.ToInt32(args[1]);
                    var stop = Stopwatch.StartNew();
                    PublishUsingMultipleThreads(threads,perThread);
                    Console.WriteLine("Published {0} messages in {1} using {2} threads.".FormatWith(perThread * threads, stop.Elapsed, threads));
                }
                else
                {
                    int count;
                    if (int.TryParse(input, out count))
                    {
                        var stop = Stopwatch.StartNew();
                        PublishUsingSingleThread(count);
                        Console.WriteLine("Published {0} messages in {1}".FormatWith(count, stop.Elapsed));
                    }
                }

                input = Console.ReadLine();
            }
        }

        private void PublishUsingMultipleThreads(int threads, int perThread)
        {
            var factory = new TaskFactory();
            var options = TaskCreationOptions.LongRunning;
            var tasks = (from i in Enumerable.Range(0, threads)
                         select factory.StartNew(() => PublishUsingSingleThread(perThread), options));
            Task.WaitAll(tasks.ToArray());
        }

        private void PublishUsingSingleThread(int count)
        {
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var options = new TransactionOptions
                    {
                        IsolationLevel = IsolationLevel.ReadCommitted,
                        Timeout = TimeSpan.FromSeconds(10)
                    };
                    using (var scope = new TransactionScope(TransactionScopeOption.Required, options))
                    {
                        bus.Publish<HelloWorld>(m => { m.Date = DateTime.Now; });
                        scope.Complete();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex);
                }
            }
        }

        public void Stop()
        {
        }
    }
}