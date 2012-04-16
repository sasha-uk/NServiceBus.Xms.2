using System;
using System.Transactions;
using Autofac;
using Messages;
using NServiceBus;
using NServiceBus.Xms.Transport.Config;
using log4net.Config;

namespace Subscriber1
{
    public class EndpointConfig : IConfigureThisEndpoint, IWantCustomInitialization
    {
        public void Init()
        {
            SetLoggingLibrary.Log4Net(XmlConfigurator.Configure);

            var builder = new ContainerBuilder();
            IContainer container = builder.Build();

            Configure.With()
                .Autofac2Builder(container)
                .XmlSerializer(Namespaces.Default)
                /*.MsmqTransport()
                    .IsTransactional(true)
                    .PurgeOnStartup(false)*/
                .XmsTransport()
                    .IsTransactional(true)
                    .PurgeOnStartup(false)
                .UnicastBus()
                    .LoadMessageHandlers()
                .CreateBus();
        }
    }
}