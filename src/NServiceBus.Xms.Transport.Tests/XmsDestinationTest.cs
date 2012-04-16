using NUnit.Framework;

namespace NServiceBus.Xms.Transport.Tests
{
    [TestFixture]
    public class XmsDestinationTest
    {
        [Test]
        public void ShouldParseStringToXmsDestination()
        {
            var actual = "queueName@qm/host/12/channel".ToXmsDestination();
            Assert.That(actual.Queue, Is.EqualTo("QUEUENAME"));
            Assert.That(actual.Manager, Is.EqualTo("QM"));
            Assert.That(actual.HostName, Is.EqualTo("HOST"));
            Assert.That(actual.Port, Is.EqualTo(12));
            Assert.That(actual.Channel, Is.EqualTo("CHANNEL"));
        }

        [Test]
        public void ShouldOverrideToString()
        {
            var actual = "queueName@qm/host/12/channel".ToXmsDestination().ToString();
            Assert.That(actual, Is.EqualTo("QUEUENAME@QM/HOST/12/CHANNEL"));
        }


        [Test]
        public void ShouldOverrideEquals()
        {
            var a = "queueName@qm/host/12/channel".ToXmsDestination();
            var b = "queueName@qm/host/12/channel".ToXmsDestination();

            Assert.That(a, Is.EqualTo(b));
        }
    }
}