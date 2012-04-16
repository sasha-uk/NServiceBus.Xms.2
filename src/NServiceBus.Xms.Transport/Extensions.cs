using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using IBM.XMS;

namespace NServiceBus.Xms.Transport
{
    public static class Extensions
    {
        public static byte[] ToBytes(this Stream stream)
        {
            stream.Position = 0;
            var content = new byte[stream.Length];
            stream.Read(content, 0, content.Length);
            return content;
        }

        public static byte[] BodyToBytes(this IBytesMessage message)
        {
            var bytes = new byte[message.BodyLength];
            if (bytes.Length > 0)
            {
                message.ReadBytes(bytes, bytes.Length);
            }
            return bytes;
        }

        public static Stream BodyToStream(this IBytesMessage message)
        {
            return new MemoryStream(message.BodyToBytes());
        }

        public static string ToXmsFriendly(this string value)
        {
            return value.Replace(".", "_");
        }

        public static string FromXmsFriendly(this string value)
        {
            return value.Replace("_", ".");
        }

        public static string FormatWith(this string format, params object[] args)
        {
            return String.Format(CultureInfo.InvariantCulture, format, args);
        }

        public static void CopyStringProperty(this IBytesMessage to, string propertyName, IBytesMessage from)
        {
            var value = @from.GetStringProperty(propertyName);
            to.SetStringProperty(propertyName, value);
        }

        public static void CopyIntProperty(this IBytesMessage to, string propertyName, IBytesMessage from)
        {
            var value = @from.GetIntProperty(propertyName);
            to.SetIntProperty(propertyName, value);
        }

        public static TimeSpan Seconds(this int seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        public static TimeSpan Milliseconds(this int milliseconds)
        {
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        public static string GetIfDefined(this Dictionary<string,string> aliases, string address)
        {
            string result;
            if(aliases.TryGetValue(address, out result))
            {
                return result;
            }
            return address;
        }
    }
}