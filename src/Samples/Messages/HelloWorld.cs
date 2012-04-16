using System;
using NServiceBus;

namespace Messages
{
    public class HelloWorld : IMessage
    {
        public DateTime Date { get; set; }
    }
}