using NServiceBus.Xms.Transport.Pooling;
using NUnit.Framework;
using Rhino.Mocks;

namespace NServiceBus.Xms.Transport.Tests
{
    [TestFixture]
    public class XmsPooledProducerTests
    {
        private XmsDestination destination;
        private StackStore<XmsPooledProducer> store;

        [SetUp]
        public void SetUp()
        {
            destination = Target.Input;
            XmsUtilities.Purge(destination);
            store = new StackStore<XmsPooledProducer>();
        }

        [Test]
        public void ShouldReturnInstanceIntoThePoolAndNextCallerShouldGetIt()
        {
            var pool = new Pool<XmsPooledProducer>(1, p => CreateFailingStub(p), store);
            Assert.That(store.Count, Is.EqualTo(0));

            IXmsProducer expected;
            IXmsProducer actual;
            using (var producer = pool.Acquire())
            {
                expected = producer;
            }
            using (var producer = pool.Acquire())
            {
                actual = producer;
            }

            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(actual, Is.SameAs(expected));
        }

        [Test]
        public void ShouldNotReturnFaultyInstanceIntoThePoolAndWeCanGetNewOne()
        {
            var pool = new Pool<XmsPooledProducer>(1, p => CreateFailingStub(p), store);
            Assert.That(store.Count, Is.EqualTo(0));

            IXmsProducer notExpected;
            IXmsProducer actual;

            using (var producer = pool.Acquire())
            {
                notExpected = producer;
                Assert.Throws<TestException>(() => producer.Send(null));
            }
            using (var producer = pool.Acquire())
            {
                actual = producer;
            }

            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(actual, Is.Not.SameAs(notExpected));
        }

        [Test]
        public void SchouldSendMessageNonTransactional()
        {
            using (var pool = CreateNewPool(false))
            using (var producer = pool.Acquire())
            {
                producer.SendTestMessage(destination);
            }
            destination.AssertMessageCount(1);
        }

        private Pool<XmsPooledProducer> CreateNewPool(bool trasactional)
        {
            return new Pool<XmsPooledProducer>(2,
                p => new XmsPooledProducer(p, new XmsProducer(destination, trasactional)), store);
        }

        private XmsPooledProducer CreateFailingStub(Pool<XmsPooledProducer> pool)
        {
            var producer = MockRepository.GenerateStub<IXmsProducer>();
            producer.Stub(x => x.Send(Arg<IBM.XMS.IMessage>.Is.Anything)).Throw(new TestException());
            var pooled = new XmsPooledProducer(pool, producer);
            return pooled;
        }
    }
}