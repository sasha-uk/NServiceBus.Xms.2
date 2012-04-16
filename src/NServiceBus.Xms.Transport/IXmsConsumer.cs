using System;

namespace NServiceBus.Xms.Transport
{
    public interface IXmsConsumer : IDisposable
    {
        IBM.XMS.IMessage ReceiveNoWait();
        IBM.XMS.IMessage Receive(int milisecondsToWaitForMessage);
    }
}