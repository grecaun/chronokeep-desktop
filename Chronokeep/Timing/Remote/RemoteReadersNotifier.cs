/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

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

