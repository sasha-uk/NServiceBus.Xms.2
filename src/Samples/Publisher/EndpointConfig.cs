using Autofac;
using Messages;
using NServiceBus;
using NServiceBus.Host.Internal;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Xms.Transport.Config;
using log4net.Config;

namespace Publisher
{
    public class EndpointConfig : IConfigureThisEndpoint, IWantCustomInitialization
    {
        public void Init()
        {
            SetLoggingLibrary.Log4Net(XmlConfigurator.Configure);
            var builder = new ContainerBuilder();
            builder.RegisterType<InMemorySubscriptionStorage>().As<ISubscriptionStorage>();
            IContainer container = builder.Build();

            Configure
                .With()
                .Autofac2Builder(container)
                .XmlSerializer(Namespaces.Default)
                .XmsTransport()
                    .IsTransactional(true)
                    .PurgeOnStartup(false)
                .DBSubcriptionStorage()
              /*  .MsmqTransport()
                    .IsTransactional(true)
                    .PurgeOnStartup(false)*/
                .UnicastBus()
                    .ImpersonateSender(false)
                .CreateBus();
        }
    }
}