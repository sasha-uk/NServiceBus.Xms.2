using System;
using System.Threading;

namespace NServiceBus.Xms.Transport
{
    public class Delay
    {
        private readonly TimeSpan[] delays;
        private int nextDelay = -1;

        public Delay(params TimeSpan[] delays)
        {
            this.delays = delays;
        }

        public void Wait()
        {
            Thread.Sleep(CalculateNextDelay());
        }

        public void Reset()
        {
            nextDelay = -1;
        }

        private TimeSpan CalculateNextDelay()
        {
            if(nextDelay < delays.Length - 1)
            {
                nextDelay++;
            }
            return delays[nextDelay];
        }
    }
}