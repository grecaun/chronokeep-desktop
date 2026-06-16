using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Chronokeep.Network.Util
{
    internal static class Helpers
    {
        internal static HttpClient GetHttpClient()
        {
            HttpClient client = new();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
