namespace NServiceBus.Xms.Transport.Config
{
    /// <summary>
    /// Contains extension methods to NServiceBus.Configure.
    /// </summary>
    public static class ConfigureXmsExtensions
    {
        /// <summary>
        /// Returns MsmqTransport specific configuration settings.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static XmsTransportConfigure XmsTransport(this Configure config)
        {
            var cfg = new XmsTransportConfigure();
            cfg.Configure(config);

            return cfg;
        }

        /// <summary>
        /// Forwards all received messages to a given endpoint (queue@machine).
        /// This is useful as an auditing/debugging mechanism.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Configure ForwardReceivedMessagesTo(this Configure config, string value)
        {
            config.Configurer.ConfigureProperty<XmsTransport>(t => t.ForwardReceivedMessagesTo, value);
            return config;
        }
    }
}