using NUnit.Framework;
using log4net.Config;

namespace NServiceBus.Xms.Transport.Tests
{
    [SetUpFixture]
    public class CleanUp
    {
        [SetUp]
        public void Setup()
        {
            log4net.Config.XmlConfigurator.Configure();
        }

        [TearDown]
        public void TearDown()
        {
            //XmsUtilities.Purge(Target.Input);
        }
    }
}