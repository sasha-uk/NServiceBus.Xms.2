using System;

namespace NServiceBus.Xms.Transport
{
    public static class Clock
    {
        private static readonly Func<DateTime> DefaultProvider = () => DateTime.UtcNow;
        private static Func<DateTime> provider = DefaultProvider;

        public static DateTime UtcNow()
        {
            return provider();
        }

        public static void Freeze()
        {
            var value = provider();
            provider = () => value;
        }

        public static void Freeze(DateTime dateTime)
        {
            provider = () => dateTime;
        }

        public static void AdvancedBy(TimeSpan timeSpan)
        {
            Freeze(UtcNow().Add(timeSpan));
        }

        public static void Reset()
        {
            provider = DefaultProvider;
        }
    }
}