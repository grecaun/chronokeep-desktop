using Chronokeep.Interfaces.Timing;
using System.Collections.Generic;
using System.Threading;

namespace Chronokeep.Timing.Remote
{
    internal class RemoteReadersNotifier : IRemoteReadersChangeNotifier
    {
        private readonly List<IRemoteReadersChangeSubscriber> subscribed = [];
        private readonly Lock rrLock = new();

        private static readonly RemoteReadersNotifier Instance = new();

        public static RemoteReadersNotifier GetRemoteReadersNotifier()
        {
            return Instance;
        }

        public bool Subscribe(IRemoteReadersChangeSubscriber sub)
        {
            bool output = false;
            if (!rrLock.TryEnter(3000)) return output;
            try
            {
                subscribed.Add(sub);
                output = true;
            }
            finally
            {
                rrLock.Exit();
            }
            return output;
        }

        public bool Unsubscribe(IRemoteReadersChangeSubscriber sub)
        {
            bool output = false;
            if (!rrLock.TryEnter(3000)) return output;
            try
            {
                output = subscribed.Remove(sub);
            }
            finally
            {
                rrLock.Exit();
            }
            return output;
        }

        public void Notify()
        {
            if (!rrLock.TryEnter(3000)) return;
            try
            {
                foreach (IRemoteReadersChangeSubscriber subscriber in subscribed)
                {
                    subscriber.NotifyRemoteReadersChange();
                }
            }
            finally
            {
                rrLock.Exit();
            }
        }
    }
}
