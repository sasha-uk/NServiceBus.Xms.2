using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Transactions;
using IBM.XMS;
using NServiceBus.Serialization;
using NServiceBus.Unicast.Transport;
using NServiceBus.Unicast.Transport.Msmq;
using NServiceBus.Utils;
using log4net;

namespace NServiceBus.Xms.Transport
{
    public class XmsTransport : ITransport
    {
        #region config info

        /// <summary>
        ///   The path to the queue the transport will read from.
        ///   Only specify the name of the queue - msmq specific address not required.
        ///   When using MSMQ v3, only local queues are supported.
        /// </summary>
        public string InputQueue { get; set; }

        /// <summary>
        ///   Sets the path to the queue the transport will transfer
        ///   errors to.
        /// </summary>
        public string ErrorQueue { get; set; }

        /// <summary>
        ///   Sets whether or not the transport is transactional.
        /// </summary>
        public bool IsTransactional { get; set; }

        /// <summary>
        ///   Sets whether or not the transport should deserialize
        ///   the body of the message placed on the queue.
        /// </summary>
        public bool SkipDeserialization { get; set; }

        /// <summary>
        ///   Sets whether or not the transport should purge the input
        ///   queue when it is started.
        /// </summary>
        public bool PurgeOnStartup { get; set; }

        private int maxRetries = 5;

        /// <summary>
        ///   Sets the maximum number of times a message will be retried
        ///   when an exception is thrown as a result of handling the message.
        ///   This value is only relevant when <see cref = "IsTransactional" /> is true.
        /// </summary>
        /// <remarks>
        ///   Default value is 5.
        /// </remarks>
        public int MaxRetries
        {
            get { return maxRetries; }
            set { maxRetries = value; }
        }

        private int millisecondsToWaitForMessage = 10000;

        /// <summary>
        ///   Sets the maximum interval of time for when a thread thinks there is a message in the queue
        ///   that it tries to receive, until it gives up.
        /// 
        ///   Default value is 10.
        /// </summary>
        public int SecondsToWaitForMessage
        {
            get { return millisecondsToWaitForMessage*1000; }
            set { millisecondsToWaitForMessage = value/1000; }
        }

        /// <summary>
        ///   Property for getting/setting the period of time when the transaction times out.
        ///   Only relevant when <see cref = "IsTransactional" /> is set to true.
        /// </summary>
        public TimeSpan TransactionTimeout { get; set; }

        /// <summary>
        ///   Property for getting/setting the isolation level of the transaction scope.
        ///   Only relevant when <see cref = "IsTransactional" /> is set to true.
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; }


        /// <summary>
        ///   Sets the object which will be used to serialize and deserialize messages.
        /// </summary>
        public IMessageSerializer MessageSerializer { get; set; }

        /// <summary>
        ///   Gets/sets the address to which all received messages will be forwarded.
        /// </summary>
        public string ForwardReceivedMessagesTo { get; set; }

        #endregion

        #region ITransport Members

        /// <summary>
        ///   Event which indicates that message processing has started.
        /// </summary>
        public event EventHandler StartedMessageProcessing;

        /// <summary>
        ///   Event which indicates that message processing has completed.
        /// </summary>
        public event EventHandler FinishedMessageProcessing;

        /// <summary>
        ///   Event which indicates that message processing failed for some reason.
        /// </summary>
        public event EventHandler FailedMessageProcessing;

        /// <summary>
        ///   Gets/sets the number of concurrent threads that should be
        ///   created for processing the queue.
        /// 
        ///   Get returns the actual number of running worker threads, which may
        ///   be different than the originally configured value.
        /// 
        ///   When used as a setter, this value will be used by the <see cref = "Start" />
        ///   method only and will have no effect if called afterwards.
        /// 
        ///   To change the number of worker threads at runtime, call <see cref = "ChangeNumberOfWorkerThreads" />.
        /// </summary>
        public int NumberOfWorkerThreads
        {
            get
            {
                lock (workerThreads)
                    return workerThreads.Count;
            }
            set { numberOfWorkerThreads = value; }
        }

        private int numberOfWorkerThreads;


        /// <summary>
        ///   Event raised when a message has been received in the input queue.
        /// </summary>
        public event EventHandler<TransportMessageReceivedEventArgs> TransportMessageReceived;

        /// <summary>
        ///   Gets the address of the input queue.
        /// </summary>
        public string Address
        {
            get { return InputQueue; }
        }

        public Dictionary<string, string> Aliases { get; set; }

        /// <summary>
        ///   Changes the number of worker threads to the given target,
        ///   stopping or starting worker threads as needed.
        /// </summary>
        /// <param name = "targetNumberOfWorkerThreads"></param>
        public void ChangeNumberOfWorkerThreads(int targetNumberOfWorkerThreads)
        {
            lock (workerThreads)
            {
                var current = workerThreads.Count;

                if (targetNumberOfWorkerThreads == current)
                    return;

                if (targetNumberOfWorkerThreads < current)
                {
                    for (var i = targetNumberOfWorkerThreads; i < current; i++)
                        workerThreads[i].Stop();

                    return;
                }

                if (targetNumberOfWorkerThreads > current)
                {
                    for (var i = current; i < targetNumberOfWorkerThreads; i++)
                        AddWorkerThread().Start();

                    return;
                }
            }
        }

        /// <summary>
        ///   Starts the transport.
        /// </summary>
        public void Start()
        {
            producerProvider = new XmsProducerProvider(IsTransactional);
            consumerProvider = new XmsConsumerProvider(IsTransactional);

            CheckConfiguration();

            inputDestination = Aliases.GetIfDefined(InputQueue).ToXmsDestination();
            errorDestination = Aliases.GetIfDefined(ErrorQueue).ToXmsDestination();
            if (ForwardReceivedMessagesTo != null)
            {
                forwardDestination = Aliases.GetIfDefined(ForwardReceivedMessagesTo).ToXmsDestination();
            }
            if (PurgeOnStartup)
            {
                XmsUtilities.Purge(inputDestination);
            }

            for (int i = 0; i < numberOfWorkerThreads; i++)
                AddWorkerThread().Start();
        }

        private void CheckConfiguration()
        {
            if (string.IsNullOrEmpty(InputQueue))
                return;

            if (MessageSerializer == null && !SkipDeserialization)
                throw new InvalidOperationException("No message serializer has been configured.");
        }

        /// <summary>
        ///   Re-queues a message for processing at another time.
        /// </summary>
        /// <param name = "m">The message to process later.</param>
        /// <remarks>
        ///   This method will place the message onto the back of the queue
        ///   which may break message ordering.
        /// </remarks>
        public void ReceiveMessageLater(TransportMessage m)
        {
            if (!string.IsNullOrEmpty(InputQueue))
                Send(m, InputQueue);
        }


        private XmsProducerProvider producerProvider;
        private XmsConsumerProvider consumerProvider;

        /// <summary>
        ///   Sends a message to the specified destination.
        /// </summary>
        /// <param name = "m">The message to send.</param>
        /// <param name = "destination">The address of the destination to send the message to.</param>
        public void Send(TransportMessage m, string destination)
        {
            var xmsDestination = Aliases.GetIfDefined(destination).ToXmsDestination();
            if (producerProvider == null)
            {
                producerProvider = new XmsProducerProvider(IsTransactional);
            }
            using(var producer = producerProvider.GetProducer(xmsDestination))
            {
                var toSend = producer.CreateBytesMessage();
                XmsUtilities.Convert(m, toSend, MessageSerializer);
                producer.Send(toSend);
                m.Id = toSend.JMSMessageID;
            }

            /* 
            catch (Exception)
                //TODO: sasha - exception 
                if (ex.MessageQueueErrorCode == MessageQueueErrorCode.QueueNotFound)
                    throw new ConfigurationErrorsException("The destination queue '" + destination +
                    "' could not be found. You may have misconfigured the destination for this kind of message (" +
                    m.Body[0].GetType().FullName +
                    ") in the MessageEndpointMappings of the UnicastBusConfig section in your configuration file." +
                    "It may also be the case that the given queue just hasn't been created yet, or has been deleted."
                , ex);
                throw;
            */
        }

        /// <summary>
        ///   Returns the number of messages in the queue.
        /// </summary>
        /// <returns></returns>
        public int GetNumberOfPendingMessages()
        {
            return XmsUtilities.GetCurrentQueueDebth(inputDestination);
        }

        #endregion

        #region helper methods

        private WorkerThread AddWorkerThread()
        {
            lock (workerThreads)
            {
                var result = new WorkerThread(Process);

                workerThreads.Add(result);

                result.Stopped += delegate(object sender, EventArgs e)
                                      {
                                          var wt = sender as WorkerThread;
                                          lock (workerThreads)
                                              workerThreads.Remove(wt);
                                      };

                return result;
            }
        }

        /// <summary>
        ///   Waits for a message to become available on the input queue
        ///   and then receives it.
        /// </summary>
        /// <remarks>
        ///   If the queue is transactional the receive operation will be wrapped in a 
        ///   transaction.
        /// </remarks>
        private void Process()
        {
            needToAbort = false;
            messageId = string.Empty;

            try
            {
                if (IsTransactional)
                    new TransactionWrapper().RunInTransaction(ReceiveFromQueue, IsolationLevel, TransactionTimeout);
                else
                    ReceiveFromQueue();

                ClearFailuresForMessage(messageId);
            }
            catch (AbortHandlingCurrentMessageException)
            {
                //in case AbortHandlingCurrentMessage was called
                return; //don't increment failures, we want this message kept around.
            }
            catch
            {
                if (IsTransactional)
                    IncrementFailuresForMessage(messageId);

                OnFailedMessageProcessing();
            }
        }

        /// <summary>
        ///   Receives a message from the input queue.
        /// </summary>
        /// <remarks>
        ///   If a message is received the <see cref = "TransportMessageReceived" /> event will be raised.
        /// </remarks>
        public void ReceiveFromQueue()
        {
            var m = ReceiveMessageFromQueueAfterPeekWasSuccessful();
            if (m == null)
                return;

            messageId = m.JMSMessageID;

            if (IsTransactional)
            {
                if (HandledMaxRetries(m.JMSMessageID))
                {
                    string id = XmsUtilities.GetRealMessageId(m);

                    log.Error(string.Format("Message has failed the maximum number of times allowed, ID={0}.", id));

                    MoveToErrorQueue(m);
                    OnFinishedMessageProcessing();

                    return;
                }
            }

            //exceptions here will cause a rollback - which is what we want.
            if (StartedMessageProcessing != null)
                StartedMessageProcessing(this, null);

            if (ForwardReceivedMessagesTo != null)
                ForwardMessage(m);


            var result = XmsUtilities.Convert(m);

            var bytesMessage = (IBytesMessage)m;

            if (SkipDeserialization)
            {
                result.BodyStream = bytesMessage.BodyToStream();
            }
            else
            {
                try
                {
                    result.Body = MessageSerializer.Deserialize(bytesMessage.BodyToStream());
                }
                catch (Exception e)
                {
                    log.Error("Could not extract message data.", e);

                    MoveToErrorQueue(m);

                    OnFinishedMessageProcessing(); // don't care about failures here
                    return; // deserialization failed - no reason to try again, so don't throw
                }
            }

            //care about failures here
            var exceptionNotThrown = OnTransportMessageReceived(result);
            //and here
            var otherExNotThrown = OnFinishedMessageProcessing();

            //but need to abort takes precedence - failures aren't counted here,
            //so messages aren't moved to the error queue.
            if (needToAbort)
                throw new AbortHandlingCurrentMessageException();

            if (!(exceptionNotThrown && otherExNotThrown)) //cause rollback
                throw new ApplicationException("Exception occured while processing message.");
        }

        private void ForwardMessage(IBM.XMS.IMessage m)
        {
            try
            {
                using(var producer = producerProvider.GetProducer(forwardDestination))
                {
                    producer.Send(m);
                }
            }
            catch (Exception ex)
            {
                log.Error("Unexpected error while forwarding message to {0}.".FormatWith(forwardDestination),ex);
                throw;
            }
        }

        private bool HandledMaxRetries(string id)
        {
            failuresPerMessageLocker.EnterReadLock();

            if (failuresPerMessage.ContainsKey(id) &&
                (failuresPerMessage[id] >= maxRetries))
            {
                failuresPerMessageLocker.ExitReadLock();
                failuresPerMessageLocker.EnterWriteLock();
                failuresPerMessage.Remove(id);
                failuresPerMessageLocker.ExitWriteLock();

                return true;
            }

            failuresPerMessageLocker.ExitReadLock();
            return false;
        }

        private void ClearFailuresForMessage(string id)
        {
            failuresPerMessageLocker.EnterReadLock();
            if (failuresPerMessage.ContainsKey(id))
            {
                failuresPerMessageLocker.ExitReadLock();
                failuresPerMessageLocker.EnterWriteLock();
                failuresPerMessage.Remove(id);
                failuresPerMessageLocker.ExitWriteLock();
            }
            else
                failuresPerMessageLocker.ExitReadLock();
        }

        private void IncrementFailuresForMessage(string id)
        {
            try
            {
                failuresPerMessageLocker.EnterWriteLock();

                if (!failuresPerMessage.ContainsKey(id))
                    failuresPerMessage[id] = 1;
                else
                    failuresPerMessage[id] = failuresPerMessage[id] + 1;
            }
            catch
            {
            } //intentionally swallow exceptions here
            finally
            {
                failuresPerMessageLocker.ExitWriteLock();
            }
        }

        [DebuggerNonUserCode] // so that exceptions don't interfere with debugging.
        private IBM.XMS.IMessage ReceiveMessageFromQueueAfterPeekWasSuccessful()
        {
            try
            {
                var provider = consumerProvider.GetConsumer(inputDestination);
                var message = provider.Receive(millisecondsToWaitForMessage);
                delay.Value.Reset();
                return message;
            }
            catch (AccessViolationException e)
            {
                log.Error("Suspected issue with IBM.XMS driver. Unable to connect to {0}.".FormatWith(inputDestination), e);
                delay.Value.Wait();
                return null;
            }
            catch (Exception e)
            {
                log.Error("Problem in receiving message from queue: {0}".FormatWith(inputDestination), e);
                delay.Value.Wait();
                return null;
            }
        }

        /// <summary>
        ///   Moves the given message to the configured error queue.
        /// </summary>
        /// <param name = "m"></param>
        protected void MoveToErrorQueue(IBM.XMS.IMessage m)
        {
            using(var producer = producerProvider.GetProducer(errorDestination))
            {
                if(m is IBytesMessage)
                {
                    var toSend = producer.CreateBytesMessage();
                    XmsUtilities.PopulateErrorQueueMessage(toSend, (IBytesMessage)m, inputDestination);
                    producer.Send(m);
                }
                else
                {
                    log.Error("Received message of type {0} which is not supported. This message will be ignored.".FormatWith(m.GetType()));
                }
            }
        }

        /// <summary>
        ///   Causes the processing of the current message to be aborted.
        /// </summary>
        public void AbortHandlingCurrentMessage()
        {
            needToAbort = true;
        }

        private bool OnFinishedMessageProcessing()
        {
            try
            {
                if (FinishedMessageProcessing != null)
                    FinishedMessageProcessing(this, null);
            }
            catch (Exception e)
            {
                log.Error("Failed raising 'finished message processing' event.", e);
                return false;
            }

            return true;
        }

        private bool OnTransportMessageReceived(TransportMessage msg)
        {
            try
            {
                if (TransportMessageReceived != null)
                    TransportMessageReceived(this, new TransportMessageReceivedEventArgs(msg));
            }
            catch (Exception e)
            {
                log.Warn("Failed raising 'transport message received' event for message with ID=" + msg.Id, e);
                return false;
            }

            return true;
        }

        private bool OnFailedMessageProcessing()
        {
            try
            {
                if (FailedMessageProcessing != null)
                    FailedMessageProcessing(this, null);
            }
            catch (Exception e)
            {
                log.Warn("Failed raising 'failed message processing' event.", e);
                return false;
            }

            return true;
        }

        #endregion

        #region members

        private readonly IList<WorkerThread> workerThreads = new List<WorkerThread>();

        private readonly ReaderWriterLockSlim failuresPerMessageLocker = new ReaderWriterLockSlim();

        /// <summary>
        ///   Accessed by multiple threads - lock using failuresPerMessageLocker.
        /// </summary>
        private readonly IDictionary<string, int> failuresPerMessage = new Dictionary<string, int>();

        [ThreadStatic] private static volatile bool needToAbort;
        [ThreadStatic] private static volatile string messageId;

        private ThreadLocal<Delay> delay = new ThreadLocal<Delay>(() => new Delay(
            1.Seconds(), 5.Seconds(), 10.Seconds(), 30.Seconds(), 60.Seconds()));

        private static readonly ILog log = LogManager.GetLogger(typeof (XmsTransport));

        private XmsDestination inputDestination;
        private XmsDestination errorDestination;
        private XmsDestination forwardDestination;

        #endregion

        #region IDisposable Members

        /// <summary>
        ///   Stops all worker threads and disposes the MSMQ queue.
        /// </summary>
        public void Dispose()
        {
            lock (workerThreads)
                foreach (var t in workerThreads)
                    t.Stop();
        }

        #endregion
    }
}