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
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Chronokeep.Network.Util.Helpers;

namespace Chronokeep.Objects
{
    public class Chronoclock
    {
        public int Identifier { get; set; }
        public string Name { get; init; } = "";
        public string Url { get; init; } = "";
        public bool Enabled { get; init; }

        public async Task<CountUpDownTimestampResponse> StartCountUp()
        {
            Log.D("Chronokeep.Objects.Chronoclock", "StartCountUp");
            if (string.IsNullOrEmpty(Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{Url}/start"),
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    CountUpDownTimestampResponse result = JsonSerializer.Deserialize<CountUpDownTimestampResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown starting countup: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public async Task<CountUpDownTimestampResponse> StopCountUp()
        {
            Log.D("Chronokeep.Objects.Chronoclock", "StopCountUp");
            if (string.IsNullOrEmpty(this.Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{Url}/stop"),
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    CountUpDownTimestampResponse result = JsonSerializer.Deserialize<CountUpDownTimestampResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown stopping countup: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public async Task<CountUpDownTimestampResponse> AdjustTime(int seconds)
        {
            Log.D("Chronokeep.Objects.Chronoclock", "AdjustTime");
            if (string.IsNullOrEmpty(Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                using HttpClient client = GetHttpClient();
                string which = "/add_seconds";
                if (seconds < 0)
                {
                    seconds *= -1;
                    which = "/remove_seconds";
                }
                Dictionary<string, string> postContent = [];
                postContent["seconds"] = seconds.ToString();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"http://{Url}{which}"),
                    Content = new FormUrlEncodedContent(postContent)
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    CountUpDownTimestampResponse result = JsonSerializer.Deserialize<CountUpDownTimestampResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown adjusting time: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public async Task<GetTimeResponse> GetTime()
        {
            Log.D("Chronokeep.Objects.Chronoclock", "GetTime");
            if (string.IsNullOrEmpty(Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{Url}/get_time"),
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetTimeResponse result = JsonSerializer.Deserialize<GetTimeResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown getting time: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public async Task<GetConfigResponse> GetConfig()
        {
            Log.D("Chronokeep.Objects.Chronoclock", "GetConfig");
            if (string.IsNullOrEmpty(Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{Url}/config.json"),
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetConfigResponse result = JsonSerializer.Deserialize<GetConfigResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown getting config: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public async Task<GetTimeResponse> SetTime(DateTime date)
        {
            Log.D("Chronokeep.Objects.Chronoclock", "SetTime");
            if (string.IsNullOrEmpty(Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                Dictionary<string, string> postContent = [];
                postContent["DateTime"] = date.ToString("yyyy-MM-dd HH:mm:ss");
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"http://{Url}/set_time"),
                    Content = new FormUrlEncodedContent(postContent)
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetTimeResponse result = JsonSerializer.Deserialize<GetTimeResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown setting time: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public async Task<CountUpDownTimestampResponse> SetCountUpDownTime(DateTime date)
        {
            Log.D("Chronokeep.Objects.Chronoclock", "SetCountUpDownTime");
            if (string.IsNullOrEmpty(Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                Dictionary<string, string> postContent = [];
                postContent["DateTime"] = date.ToString("yyyy-MM-dd HH:mm:ss");
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"http://{Url}/set_countupdown"),
                    Content = new FormUrlEncodedContent(postContent)
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    CountUpDownTimestampResponse result = JsonSerializer.Deserialize<CountUpDownTimestampResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown setting time: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public async Task<CountUpDownTimestampResponse> SetFlipDisplay(bool flipDisplay)
        {
            Log.D("Chronokeep.Objects.Chronoclock", "SetFlipDisplay");
            if (string.IsNullOrEmpty(Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                Dictionary<string, string> postContent = [];
                postContent["value"] = flipDisplay ? "1" : "0";
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"http://{Url}/set_flip"),
                    Content = new FormUrlEncodedContent(postContent)
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    CountUpDownTimestampResponse result = JsonSerializer.Deserialize<CountUpDownTimestampResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown setting flipDisplay: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public async Task<CountUpDownTimestampResponse> SetTwelveHour(bool twelveHour)
        {
            Log.D("Chronokeep.Objects.Chronoclock", "SetTwelveHour");
            if (string.IsNullOrEmpty(Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                Dictionary<string, string> postContent = [];
                postContent["value"] = twelveHour ? "1" : "0";
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"http://{Url}/set_twelvehour"),
                    Content = new FormUrlEncodedContent(postContent)
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    CountUpDownTimestampResponse result = JsonSerializer.Deserialize<CountUpDownTimestampResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown setting twelveHour: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public async Task<CountUpDownTimestampResponse> SetLockCountUpDown(bool lockCountUpDown)
        {
            Log.D("Chronokeep.Objects.Chronoclock", "SetLockCountUpDown");
            if (string.IsNullOrEmpty(Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                Dictionary<string, string> postContent = [];
                postContent["value"] = lockCountUpDown ? "1" : "0";
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"http://{Url}/set_lock"),
                    Content = new FormUrlEncodedContent(postContent)
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    CountUpDownTimestampResponse result = JsonSerializer.Deserialize<CountUpDownTimestampResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown setting lockCountUpDown: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public async Task<CountUpDownTimestampResponse> SetBrightness(uint brightness)
        {
            Log.D("Chronokeep.Objects.Chronoclock", "SetBrightness");
            if (string.IsNullOrEmpty(Url))
            {
                throw new ApiException("url not set");
            }
            string content;
            try
            {
                Dictionary<string, string> postContent = [];
                postContent["value"] = brightness.ToString();
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"http://{Url}/set_brightness"),
                    Content = new FormUrlEncodedContent(postContent)
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.D("Chronokeep.Objects.Chronoclock", "Status code = ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    CountUpDownTimestampResponse result = JsonSerializer.Deserialize<CountUpDownTimestampResponse>(json)!;
                    return result;
                }
                Log.D("Chronokeep.Objects.Chronoclock", "Status code = conflict.");
                string eJson = await response.Content.ReadAsStringAsync();
                ChronoclockErrorResponse eResult = JsonSerializer.Deserialize<ChronoclockErrorResponse>(eJson)!;
                content = eResult.Error;
            }
            catch (Exception ex)
            {
                Log.D("Chronokeep.Objects.Chronoclock", "Exception thrown.");
                throw new ApiException($"Exception thrown setting brightness: {ex.Message}");
            }
            throw new ApiException(content);
        }
    }

    public class GetConfigResponse
    {
        [JsonPropertyName("mdns")]
        public string Mdns { get; init; } = "";
        [JsonPropertyName("apSsid")]
        public string ApSsid { get; init; } = "";
        [JsonPropertyName("apPassword")]
        public string ApPassword { get; init; } = "";
        [JsonPropertyName("ssids")]
        public List<string> Ssids { get; init; } = [];
        [JsonPropertyName("passwords")]
        public List<string> Passwords { get; init; } = [];
        [JsonPropertyName("timeZone")]
        public string TimeZone { get; init; } = "";
        [JsonPropertyName("brightness")]
        public uint Brightness { get; init; }
        [JsonPropertyName("flipDisplay")]
        public bool FlipDisplay { get; init; }
        [JsonPropertyName("twelveHour")]
        public bool TwelveHour { get; init; }
        [JsonPropertyName("lockCountUpDown")]
        public bool LockCountUpDown { get; init; }
        [JsonPropertyName("ntpServer1")]
        public string NtpServer1 { get; init; } = "";
        [JsonPropertyName("ntpServer2")]
        public string NtpServer2 { get; init; } = "";
        [JsonPropertyName("countupdownTimestamp")]
        public long CountUpDownTimestamp { get; init; }
    }

    public class GetTimeResponse
    {
        [JsonPropertyName("time")]
        public string Time { get; init; } = "";
    }

    public class CountUpDownTimestampResponse
    {
        [JsonPropertyName("brightness")]
        public uint Brightness { get; init; }
        [JsonPropertyName("flipDisplay")]
        public bool FlipDisplay { get; init; }
        [JsonPropertyName("twelveHour")]
        public bool TwelveHour { get; init; }
        [JsonPropertyName("lockCountUpDown")]
        public bool LockCountUpDown { get; init; }
        [JsonPropertyName("countupdownTimestamp")]
        public long CountUpDownTimestamp { get; init; }
    }

    public class ChronoclockErrorResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; init; } = "";
    }
}

