using System;
using NServiceBus.Xms.Transport.Pooling;
using NUnit.Framework;

namespace NServiceBus.Xms.Transport.Tests.Pooling
{
    [TestFixture]
    public class StackStoreTest
    {
        private TimeSpan cleanUpInterval;
        private TimeSpan expiration;
        private StackStore<Foo> store;
        private Foo foo1;
        private Foo foo2;
        private Foo foo3;

        [SetUp]
        public void Setup()
        {
            foo1 = new Foo();
            foo2 = new Foo();
            foo3 = new Foo();
            expiration = 1.Seconds();
            cleanUpInterval = 10.Milliseconds();
            store = new StackStore<Foo>(expiration, cleanUpInterval, false);
            Clock.Freeze();
        }

        [TearDown]
        public void TearDown()
        {
            Clock.Reset();
        }

        [Test]
        public void ShouldRemoveExpiredItem()
        {
            store.Store(new Foo());
            Clock.AdvancedBy(expiration);
            Clock.AdvancedBy(expiration);
            store.CleanUp();
            Assert.That(store.Count, Is.EqualTo(0));
        }

        [Test]
        public void ShouldKeepNonExpiredItem()
        {
            store.Store(new Foo());
            store.CleanUp();
            Assert.That(store.Count, Is.EqualTo(1));
        }

        [Test]
        public void ShouldDisconnectExpiredItem()
        {
            store.Store(foo1);
            Clock.AdvancedBy(expiration);
            Clock.AdvancedBy(expiration);
            store.CleanUp();
            Assert.That(foo1.IsExpired, Is.True);
        }

        [Test]
        public void ShouldPreserveTheOrderOfItems()
        {
            store.Store(foo1);
            Clock.AdvancedBy(1.Milliseconds());
            store.Store(foo2);
            Clock.AdvancedBy(1.Milliseconds());
            store.Store(foo3);
            Clock.AdvancedBy(1.Milliseconds());
            store.CleanUp();

            Assert.That(store.Count, Is.EqualTo(3));
            Assert.That(store.Fetch(), Is.SameAs(foo3));
            Assert.That(store.Fetch(), Is.SameAs(foo2));
            Assert.That(store.Fetch(), Is.SameAs(foo1));
        }

        [Test]
        public void ShouldPreserveTheOrderOfItemsOnCleanUp()
        {
            store.Store(foo1);
            Clock.AdvancedBy(expiration);
            store.Store(foo2);
            Clock.AdvancedBy(1.Milliseconds());
            store.Store(foo3);
            Clock.AdvancedBy(1.Milliseconds());
            store.CleanUp();

            Assert.That(store.Count, Is.EqualTo(2));
            Assert.That(store.Fetch(), Is.SameAs(foo3));
            Assert.That(store.Fetch(), Is.SameAs(foo2));
        }


        private class Foo : IExpirable
        {
            private bool expired;

            public void Expire()
            {
                expired = true;
            }

            public object IsExpired
            {
                get { return expired; }
            }
        }
    }
}