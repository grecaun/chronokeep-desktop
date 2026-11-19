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

using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Chronokeep.Network.Util
{
    internal static class Helpers
    {
        /// <summary>A client for the APIs, Chronoclock and remote reads: JSON replies, 10 second timeout.</summary>
        internal static HttpClient GetHttpClient() => NewHttpClient(TimeSpan.FromSeconds(10));

        /// <summary>
        /// A client for a single request, to be disposed once it's done. It asks the server to close the connection
        /// once it has replied, since nothing else will be sent on it. Otherwise the server keeps the connection open
        /// waiting for another request until its own idle timeout, which it may never hear the end of on a spotty
        /// connection. Disposing the client closes Chronokeep's side.
        /// </summary>
        internal static HttpClient NewHttpClient(TimeSpan timeout)
        {
            HttpClient client = new() { Timeout = timeout };
            client.DefaultRequestHeaders.ConnectionClose = true;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}

