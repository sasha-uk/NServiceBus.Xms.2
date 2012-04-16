using System.Collections.Generic;
using System.Configuration;

namespace NServiceBus.Xms.Transport.Config
{
    /// <summary>
    ///   Contains the properties representing the MsmqTransport configuration section.
    /// </summary>
    public class XmsTransportConfig : ConfigurationSection
    {
        /// <summary>
        ///   The queue to receive messages from in the format
        ///   "queuename@queuemanager/hostname/port/channel".
        /// </summary>
        [ConfigurationProperty("InputQueue", IsRequired = true)]
        public string InputQueue
        {
            get { return this["InputQueue"] as string; }
            set { this["InputQueue"] = value; }
        }

        /// <summary>
        ///   The queue to which to forward messages that could not be processed
        ///   in the format "queuename@queuemanager/hostname/port/channel".
        /// </summary>
        [ConfigurationProperty("ErrorQueue", IsRequired = true)]
        public string ErrorQueue
        {
            get { return this["ErrorQueue"] as string; }
            set { this["ErrorQueue"] = value; }
        }

        /// <summary>
        ///   The number of worker threads that can process messages in parallel.
        /// </summary>
        [ConfigurationProperty("NumberOfWorkerThreads", IsRequired = true)]
        public int NumberOfWorkerThreads
        {
            get { return (int) this["NumberOfWorkerThreads"]; }
            set { this["NumberOfWorkerThreads"] = value; }
        }

        /// <summary>
        ///   The maximum number of times to retry processing a message
        ///   when it fails before moving it to the error queue.
        /// </summary>
        [ConfigurationProperty("MaxRetries", IsRequired = true)]
        public int MaxRetries
        {
            get { return (int) this["MaxRetries"]; }
            set { this["MaxRetries"] = value; }
        }

        [ConfigurationProperty("Aliases", IsRequired = false)]
        [ConfigurationCollection(typeof (AliasConfigurationElement), AddItemName = "add",
            ClearItemsName = "clear", RemoveItemName = "remove")]
        public GenericConfigurationElementCollection<AliasConfigurationElement> Aliases
        {
            get
            {
                return
                    (GenericConfigurationElementCollection<AliasConfigurationElement>) this["Aliases"];
            }
        }
    }

    public class AliasConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return (string) this["name"]; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("value", IsRequired = true)]
        public string Value
        {
            get { return (string) this["value"]; }
            set { this["value"] = value; }
        }
    }

    public class GenericConfigurationElementCollection<T> : ConfigurationElementCollection, IEnumerable<T>
        where T : ConfigurationElement, new()
    {
        private readonly List<T> elements = new List<T>();

        protected override ConfigurationElement CreateNewElement()
        {
            T newElement = new T();
            elements.Add(newElement);
            return newElement;
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return elements.Find(e => e.Equals(element));
        }

        public new IEnumerator<T> GetEnumerator()
        {
            return elements.GetEnumerator();
        }
    }
}