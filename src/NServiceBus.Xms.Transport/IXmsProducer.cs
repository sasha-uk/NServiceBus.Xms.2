using System;
using IBM.XMS;

namespace NServiceBus.Xms.Transport
{
    public interface IXmsProducer : IDisposable
    {
        void Send(IBM.XMS.IMessage message);
        IBytesMessage CreateBytesMessage();
        ITextMessage CreateTextMessage();
    }
}