using System;
using System.Collections.Generic;
using System.IO;
using NServiceBus.Serializers.XML;
using NServiceBus.Unicast.Transport;
using NUnit.Framework;

namespace NServiceBus.Xms.Transport.Tests
{
    [TestFixture]
    public class XmsTransportTest
    {
        private XmsDestination destination;
        private XmsTransport transport;
        private TransportMessage sent;
        private TransportMessage received;

        [SetUp]
        public void SetUp()
        {
            destination = Target.Input;
            XmsUtilities.Purge(destination);
            transport = new XmsTransport(){Aliases = new Dictionary<string, string>()};
            received = null;
            sent = new TransportMessage().WithBody();
            ;
        }

        [Test]
        public void ShouldSendMessage()
        {
            int before = XmsUtilities.GetCurrentQueueDebth(destination);
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.WriteLine("foo message");
            writer.Flush();

            var message = new TransportMessage {BodyStream = stream};

            transport.Send(message, Target.Input.ToString());

            int after = XmsUtilities.GetCurrentQueueDebth(destination);

            Assert.That(after, Is.EqualTo(++before));
        }


        [Test]
        public void SendPopulatesMessageId()
        {
            var message = new TransportMessage().WithBody();
            Assert.That(message.Id, Is.Null);
            transport.Send(message, Target.Input.ToString());
            Assert.That(message.Id, Is.Not.Null);
            Console.WriteLine(message.Id);
        }

        [Test]
        public void ShouldPreservesMessageProperties()
        {
            sent.CorrelationId = "CorrelationId";
            sent.Headers = new List<HeaderInfo>();
            sent.IdForCorrelation = "IdForCorrelation";
            sent.MessageIntent = MessageIntentEnum.Publish;
            sent.Recoverable = true;
            sent.ReturnAddress = "ReturnAddress";
            sent.WindowsIdentityName = "WindowsIdentityName";
            var timeSent = DateTime.UtcNow;

            SendAndReceive();
            Assert.That(received.Id, Is.EqualTo(sent.Id), "Id");
            Assert.That(received.BodyStream.Length, Is.EqualTo(sent.BodyStream.Length), "BodyStream");
            Assert.That(received.CorrelationId, Is.EqualTo(sent.CorrelationId), "CorrelationId");
            // Assert.That(received.Headers.Count, Is.EqualTo(sent.Headers.Count), "Headers.Count");
            Assert.That(received.IdForCorrelation, Is.EqualTo(sent.IdForCorrelation), "IdForCorrelation");
            Assert.That(received.MessageIntent, Is.EqualTo(sent.MessageIntent), "IdForCorrelation");
            Assert.That(received.Recoverable, Is.EqualTo(sent.Recoverable), "Recoverable");
            Assert.That(received.ReturnAddress, Is.EqualTo(sent.ReturnAddress), "ReturnAddress");
            Assert.That(received.TimeSent, Is.GreaterThan(timeSent), "TimeSent");
            //Assert.That(received.TimeToBeReceived, Is.EqualTo(sent.TimeToBeReceived), "TimeToBeReceived");
            Assert.That(received.WindowsIdentityName, Is.EqualTo(sent.WindowsIdentityName), "WindowsIdentityName");
        }

        [Test]
        public void ShouldPreservesCustomMessageHeaders()
        {
            var header1 = new HeaderInfo {Key = "foo", Value = "foo-value"};
            var header2 = new HeaderInfo {Key = "bar", Value = "bar-value"};
            sent.Headers = new List<HeaderInfo> {header1, header2};

            SendAndReceive();

            Assert.That(received.Headers.Count, Is.EqualTo(2));
            Assert.That(received.Headers[0].Key, Is.EqualTo("foo"));
            Assert.That(received.Headers[0].Value, Is.EqualTo("foo-value"));
            Assert.That(received.Headers[1].Key, Is.EqualTo("bar"));
            Assert.That(received.Headers[1].Value, Is.EqualTo("bar-value"));
        }

        private void SendAndReceive()
        {
            var trans = new XmsTransport();
            trans.Aliases = new Dictionary<string, string>();
            trans.NumberOfWorkerThreads = 0;
            trans.SkipDeserialization = true;
            trans.InputQueue = Target.Input.ToString();
            trans.ErrorQueue = Target.Error.ToString();
            trans.MessageSerializer = new MessageSerializer();
            trans.Start();
            trans.Send(sent, Target.Input.ToString());
            trans.TransportMessageReceived += (sender, args) => { received = args.Message; };
            trans.ReceiveFromQueue();
        }
    }
}