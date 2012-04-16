using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IBM.WMQ;
using IBM.XMS;
using NServiceBus.Serialization;
using NServiceBus.Unicast.Transport;
using MQC = IBM.XMS.MQC;

namespace NServiceBus.Xms.Transport
{
    public static class XmsUtilities
    {
        /// <summary>
        ///   Create a WMQ connection factory and set relevant properties.
        /// </summary>
        /// <returns>A connection factory</returns>
        public static IConnectionFactory CreateConnectionFactory(XmsDestination destination)
        {
            // Create the connection factories factory
            XMSFactoryFactory factoryFactory = XMSFactoryFactory.GetInstance(XMSC.CT_WMQ);

            // Use the connection factories factory to create a connection factory
            IConnectionFactory cf = factoryFactory.CreateConnectionFactory();

            // Set the properties
            cf.SetStringProperty(XMSC.WMQ_HOST_NAME, destination.HostName);
            cf.SetIntProperty(XMSC.WMQ_PORT, destination.Port);
            cf.SetStringProperty(XMSC.WMQ_CHANNEL, destination.Channel);
            cf.SetIntProperty(XMSC.WMQ_CONNECTION_MODE, XMSC.WMQ_CM_CLIENT);
            // since null is not permited for queue manager, pass that as empty string
            // so that, while connecting to queue manager, it finds the default queue manager.
            cf.SetStringProperty(XMSC.WMQ_QUEUE_MANAGER, destination.Manager);

            //cf.SetIntProperty(XMSC.WMQ_BROKER_VERSION, Options.BrokerVersion.ValueAsNumber);

            // Integrator
            //if (Options.BrokerVersion.ValueAsNumber == XMSC.WMQ_BROKER_V2)
            //    cf.SetStringProperty(XMSC.WMQ_BROKER_PUBQ, Options.BrokerPublishQueue.Value);

            return (cf);
        }

        public static void Convert(TransportMessage message, IBytesMessage toSend, IMessageSerializer serializer)
        {
            byte[] body;
            if (message.Body == null && message.BodyStream != null)
            {
                body = message.BodyStream.ToBytes();
            }
            else
            {
                var stream = new MemoryStream();
                serializer.Serialize(message.Body, stream);
                body = stream.ToBytes();
            }

            toSend.WriteBytes(body);

            // TODO: clarify usage of JMSCorrelationID
            toSend.JMSCorrelationID = message.CorrelationId;
            toSend.JMSDeliveryMode = message.Recoverable ? DeliveryMode.Persistent : DeliveryMode.NonPersistent;
            toSend.SetStringProperty(HEADER_RETURNADDRESS, message.ReturnAddress);
            toSend.SetStringProperty(HEADER_IDFORCORRELATION, message.IdForCorrelation);
            toSend.SetStringProperty(HEADER_WINDOWSIDENTITYNAME, message.WindowsIdentityName);
            toSend.SetIntProperty(HEADER_MESSAGEINTENT, (int) message.MessageIntent);

            //TODO: set message expiration
            //toSend.JMSReplyTo = new Destination message.ReplyToAddress;
            //if (message.TimeToBeReceived < MessageQueue.InfiniteTimeout)
            //toSend.JMSExpiration = (long) UTCNow.message.TimeToBeReceived.TotalMilliseconds;

            if (message.Headers == null)
                message.Headers = new List<HeaderInfo>();

            var nsbHeaderKeys = new List<string>();
            foreach (var keyValue in message.Headers)
            {
                toSend.SetStringProperty(keyValue.Key.ToXmsFriendly(), keyValue.Value);
                nsbHeaderKeys.Add(keyValue.Key.ToXmsFriendly());
            }
            toSend.SetStringProperty(HEADER_NBSKEYS, WrapKeys(nsbHeaderKeys));
        }

        private static string WrapKeys(IEnumerable<string> keys)
        {
            return string.Join("|", keys);
        }

        private static string[] UnwrapKeys(string keys)
        {
            return keys.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);
        }

        private static readonly string HEADER_RETURNADDRESS = "ReturnAddress";
        private static readonly string HEADER_IDFORCORRELATION = "CorrId";
        private static readonly string HEADER_WINDOWSIDENTITYNAME = "WinIdName";
        private static readonly string HEADER_MESSAGEINTENT = "MessageIntent";
        private static readonly string HEADER_NBSKEYS = "NSBKeys";
        private static readonly string HEADER_FAILEDQUEUE = "FailedQ";
        private static readonly string HEADER_ORIGINALID = "OriginalId";


        public static int Purge(XmsDestination destination)
        {
            int i = 0;
            var factory = CreateConnectionFactory(destination);
            using (var connection = factory.CreateConnection())
            {
                using (ISession session = connection.CreateSession(false, AcknowledgeMode.AutoAcknowledge))
                {
                    IDestination queue = session.CreateQueue(destination.Queue);
                    queue.SetIntProperty(XMSC.DELIVERY_MODE, XMSC.DELIVERY_NOT_PERSISTENT);

                    using (var consumer = session.CreateConsumer(queue))
                    {
                        connection.Start();
                        while (consumer.ReceiveNoWait() != null)
                        {
                            ++i;
                        }
                    }
                }
            }
            return i;
        }

        private static DateTime baseDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static TransportMessage Convert(IBM.XMS.IMessage m)
        {
            var result = new TransportMessage();
            result.Id = GetRealMessageId(m);
            result.CorrelationId = m.JMSCorrelationID;
            result.Recoverable = m.JMSDeliveryMode == DeliveryMode.Persistent;
            result.IdForCorrelation = m.GetStringProperty(HEADER_IDFORCORRELATION);
            result.ReturnAddress = m.GetStringProperty(HEADER_RETURNADDRESS);
            result.WindowsIdentityName = m.GetStringProperty(HEADER_WINDOWSIDENTITYNAME);
            result.MessageIntent = (MessageIntentEnum) m.GetIntProperty(HEADER_MESSAGEINTENT);
            result.TimeSent = baseDate.AddMilliseconds(m.JMSTimestamp);
            result.Headers = new List<HeaderInfo>();
            //TODO:
            //result.TimeToBeReceived = DateTime.UtcNow - baseDate.AddMilliseconds(m.JMSExpiration);
            if (m.GetStringProperty("NSBKeys") != null)
            {
                var keys = UnwrapKeys(m.GetStringProperty("NSBKeys"));

                result.Headers = (from k in keys
                                  select new HeaderInfo
                                             {
                                                 Key = k.FromXmsFriendly(),
                                                 Value = m.GetStringProperty(k)
                                             }).ToList();
            }

            //TODO:
            //TimeToBeReceived = baseDate.AddMilliseconds(m.JMSTimestamp),
            //ReplyToAddress = GetIndependentAddressForQueue(m.ResponseQueue),
            return result;
        }

        public static int GetCurrentQueueDebth(XmsDestination destination)
        {
            var manager = new MQQueueManager(destination.Manager, destination.Channel, destination.ConnectionName);
            var queue = manager.AccessQueue(destination.Queue, MQC.MQOO_INQUIRE);
            int depth = queue.CurrentDepth;
            manager.Disconnect();
            manager.Close();
            return depth;
        }

        public static string GetRealMessageId(IBM.XMS.IMessage message)
        {
            var id = message.GetStringProperty(HEADER_ORIGINALID);
            return string.IsNullOrEmpty(id) ? message.JMSMessageID : id;
        }

        // TODO: test this
        public static void PopulateErrorQueueMessage(IBytesMessage toSend, IBM.XMS.IBytesMessage failed, XmsDestination xmsDestination)
        {
            var body = new byte[failed.BodyLength];
            if (body.Length > 0)
            {
                failed.ReadBytes(body, body.Length);
                toSend.WriteBytes(body);
            }
            toSend.JMSCorrelationID = failed.JMSCorrelationID;
            toSend.JMSDeliveryMode = failed.JMSDeliveryMode;
            toSend.CopyStringProperty(HEADER_RETURNADDRESS, failed);
            toSend.CopyStringProperty(HEADER_IDFORCORRELATION, failed);
            toSend.CopyStringProperty(HEADER_WINDOWSIDENTITYNAME, failed);
            toSend.CopyStringProperty(HEADER_MESSAGEINTENT, failed);

            var keys = failed.GetStringProperty(HEADER_NBSKEYS);
            var unwrapedKeys = UnwrapKeys(keys);
            foreach (var unwrapedKey in unwrapedKeys)
            {
                toSend.CopyStringProperty(unwrapedKey, failed);
            }

            toSend.CopyStringProperty(HEADER_NBSKEYS, failed);
            
            // error queue specific
            toSend.SetStringProperty(HEADER_FAILEDQUEUE, xmsDestination.ToString());
            var id = failed.GetStringProperty(HEADER_ORIGINALID);
            toSend.SetStringProperty(HEADER_ORIGINALID, id);
        }
    }
}