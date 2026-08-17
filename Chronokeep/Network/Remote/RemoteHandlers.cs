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

using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.Objects.ChronokeepRemote;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Chronokeep.Network.Util.Helpers;

namespace Chronokeep.Network.Remote
{
    public static class RemoteHandlers
    {
        public static async Task<GetReadersResponse> GetReaders(ApiObject api)
        {
            string content;
            Log.D("Network.Remote.RemoteHandlers", "Getting remote readers.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{api.Url}readers")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.Remote.RemoteHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetReadersResponse result = JsonSerializer.Deserialize<GetReadersResponse>(json)!;
                    return result;
                }
                Log.D("Network.Remote.RemoteHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.Remote.RemoteHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting events: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<GetReadsResponse> GetReads(ApiObject api, string reader, long start, long end)
        {
            string content;
            Log.D("Network.Remote.RemoteHandlers", "Getting reads.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{api.Url}reads"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new GetReadsRequest
                        {
                            ReaderName = reader,
                            Start = start,
                            End = end
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.Remote.RemoteHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetReadsResponse result = JsonSerializer.Deserialize<GetReadsResponse>(json)!;
                    return result;
                }
                Log.D("Network.Remote.RemoteHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.Remote.RemoteHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting events: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<DeleteReadsResponse> DeleteReads(ApiObject api, string reader, long start, long end)
        {
            string content;
            Log.D("Network.Remote.RemoteHandlers", "Deleting reads.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"{api.Url}reads/delete"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new DeleteReadsRequest
                        {
                            ReaderName = reader,
                            Start = start,
                            End = end
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.Remote.RemoteHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    DeleteReadsResponse result = JsonSerializer.Deserialize<DeleteReadsResponse>(json)!;
                    return result;
                }
                Log.D("Network.Remote.RemoteHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.Remote.RemoteHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting events: {ex.Message}");
            }
            throw new ApiException(content);
        }
    }
}

