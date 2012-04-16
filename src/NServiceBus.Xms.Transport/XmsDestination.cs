using System.Collections.Concurrent;

namespace NServiceBus.Xms.Transport
{
    public class XmsDestination
    {
        public XmsDestination(string queue, string manager, string hostName, int port, string channel)
        {
            Queue = queue;
            HostName = hostName;
            Port = port;
            Channel = channel;
            Manager = manager;
        }

        public string Queue { get; private set; }
        public string HostName { get; private set; }
        public int Port { get; private set; }
        public string Channel { get; private set; }
        public string Manager { get; private set; }

        public string ConnectionName
        {
            get { return string.Format("{0}({1})", HostName, Port); }
        }

        public override string ToString()
        {
            return "{0}@{1}/{2}/{3}/{4}".FormatWith(Queue, Manager, HostName, Port, Channel);
        }

        public bool Equals(XmsDestination other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Queue, Queue) && Equals(other.HostName, HostName) && other.Port == Port &&
                   Equals(other.Channel, Channel) && Equals(other.Manager, Manager);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (XmsDestination)) return false;
            return Equals((XmsDestination) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Queue != null ? Queue.GetHashCode() : 0);
                result = (result*397) ^ (HostName != null ? HostName.GetHashCode() : 0);
                result = (result*397) ^ Port;
                result = (result*397) ^ (Channel != null ? Channel.GetHashCode() : 0);
                result = (result*397) ^ (Manager != null ? Manager.GetHashCode() : 0);
                return result;
            }
        }
    }

    public static class XmsDestinationExtensions
    {
        public static XmsDestination ToXmsDestination(this string destination)
        {
            var parts = destination.ToUpper().Split('@');

            var queueName = parts[0];
            var args = parts[1].Split('/');
            var queueManager = args[0];
            var hostName = args[1];
            var port = int.Parse(args[2]);
            var channel = args.Length > 3 ? args[3] : string.Empty;

            return new XmsDestination(queueName, queueManager, hostName, port, channel);
        }
    }
}