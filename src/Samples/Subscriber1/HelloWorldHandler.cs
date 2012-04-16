using System;
using Messages;
using NServiceBus;

namespace Subscriber1
{
    public class HelloWorldHandler : IHandleMessages<HelloWorld>
    {
        private readonly IBus bus;

        public HelloWorldHandler(IBus bus)
        {
            this.bus = bus;
        }

        public void Handle(HelloWorld message)
        {
            Console.WriteLine("HelloWorld:" + DateTime.UtcNow);
        }
    }
}