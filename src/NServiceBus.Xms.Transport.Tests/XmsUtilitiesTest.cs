using System;
using System.Collections.Generic;
using System.Diagnostics;
using NServiceBus.Serializers.XML;
using NServiceBus.Unicast.Transport;
using NUnit.Framework;

namespace NServiceBus.Xms.Transport.Tests
{
    [TestFixture]
    public class XmsUtilitiesTest
    {
        private XmsDestination destination;
        private TransportMessage sent;
        private TransportMessage received;

        [SetUp]
        public void SetUp()
        {
            destination = Target.Input;
            XmsUtilities.Purge(destination);
            received = null;
            sent = new TransportMessage().WithBody();;
        }

        [Test]
        public void CreateConnectionFactory()
        {
            var factory = XmsUtilities.CreateConnectionFactory(destination);
            Assert.That(factory, Is.Not.Null);
        }

        [Test]
        public void CanPurgeQueue()
        {
            destination.FillWith(100);
            var stop = Stopwatch.StartNew();
            int count = XmsUtilities.Purge(destination);
            Console.WriteLine("Deleted {0} messages in {1}", count, stop.Elapsed);
            Assert.That(count, Is.EqualTo(100));
        }

        [Test]
        public void CanCountMessage()
        {
            destination.FillWith(7);
            var actual = XmsUtilities.GetCurrentQueueDebth(destination);
            Assert.That(actual, Is.EqualTo(7));
        }


        [Test]
        public void ConvertPreservesMessageId()
        {
            SendAndReceive();
            Assert.That(received.Id, Is.EqualTo(sent.Id));
        }

        private void SendAndReceive()
        {
            var transport = new XmsTransport();
            transport.Aliases = new Dictionary<string, string>();
            transport.NumberOfWorkerThreads = 0;
            transport.SkipDeserialization = true;
            transport.InputQueue = Target.Input.ToString();
            transport.ErrorQueue = Target.Error.ToString();
            transport.MessageSerializer = new MessageSerializer();
            transport.Start();
            transport.Send(sent, Target.Input.ToString());
            transport.TransportMessageReceived += (sender, args) =>
            {
                received = args.Message;
            };
            transport.ReceiveFromQueue();
        }
    }
}