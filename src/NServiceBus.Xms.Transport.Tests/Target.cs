using System.Configuration;

namespace NServiceBus.Xms.Transport.Tests
{
    public static class Target
    {
        public static XmsDestination Input
        {
            get { return ConfigurationManager.AppSettings["target"].ToXmsDestination(); }
        }

        public static XmsDestination Error
        {
            get { return ConfigurationManager.AppSettings["error"].ToXmsDestination(); }
        }
    }
}