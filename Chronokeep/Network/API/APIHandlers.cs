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
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Chronokeep.Network.Util.Helpers;

namespace Chronokeep.Network.API
{
    public static class ApiHandlers
    {
        public static async Task<bool> IsHealthy(ApiObject api)
        {
            string content;
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{api.Url}health"),
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.NoContent)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    return true;
                }
                content = "Unable to contact API.";
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown checking health: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<GetEventsResponse> GetEvents(ApiObject api)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Getting events.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{api.Url}event/my")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetEventsResponse result = JsonSerializer.Deserialize<GetEventsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting events: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<GetEventResponse> GetEvent(ApiObject api, string slug)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Getting specific event.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}event"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new GetEventRequest
                        {
                            Slug = slug
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetEventResponse result = JsonSerializer.Deserialize<GetEventResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting events: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<GetEventYearsResponse> GetEventYears(ApiObject api, string slug)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Getting event years.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}event-year/event"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new GetEventRequest
                        {
                            Slug = slug
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetEventYearsResponse result = JsonSerializer.Deserialize<GetEventYearsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting event years: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<EventYearResponse> GetEventYear(ApiObject api, string slug, string year)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Getting specific event year.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}event-year"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new GetEventYearRequest
                        {
                            Slug = slug,
                            Year = year
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    EventYearResponse result = JsonSerializer.Deserialize<EventYearResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting event years: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<ModifyEventResponse> AddEvent(ApiObject api, ApiEvent ev)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Adding event.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}event/add"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new ModifyEventRequest
                        {
                            Event = ev
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    ModifyEventResponse result = JsonSerializer.Deserialize<ModifyEventResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown adding event: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<ModifyEventResponse> UpdateEvent(ApiObject api, ApiEvent ev)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Updating event.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Put,
                    RequestUri = new Uri($"{api.Url}event/update"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new ModifyEventRequest
                        {
                            Event = ev
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    ModifyEventResponse result = JsonSerializer.Deserialize<ModifyEventResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown adding event: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<EventYearResponse> AddEventYear(ApiObject api, string slug, ApiEventYear year)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Adding event year.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}event-year/add"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new ModifyEventYearRequest
                        {
                            Slug = slug,
                            Year = year
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    EventYearResponse result = JsonSerializer.Deserialize<EventYearResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown adding event year: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task UpdateEventYear(ApiObject api, string slug, ApiEventYear year)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Updating event year.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Put,
                    RequestUri = new Uri($"{api.Url}event-year/update"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new ModifyEventYearRequest
                        {
                            Slug = slug,
                            Year = year
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    EventYearResponse result = JsonSerializer.Deserialize<EventYearResponse>(json)!;
                    return;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown adding event year: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<AddResultsResponse> UploadResults(ApiObject api, string slug, string year, List<ApiResult> results)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Uploading results.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}results/add"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new AddResultsRequest
                        {
                            Slug = slug,
                            Year = year,
                            Results = results
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    AddResultsResponse result = JsonSerializer.Deserialize<AddResultsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown uploading results: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<AddResultsResponse> DeleteResults(ApiObject api, string slug, string year)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Deleting results.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"{api.Url}results/delete"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new GetResultsRequest
                        {
                            Slug = slug,
                            Year = year
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    AddResultsResponse result = JsonSerializer.Deserialize<AddResultsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown deleting results: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<AddResultsResponse> DeleteDistanceResults(ApiObject api, string slug, string year, string distance)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Deleting distance results.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"{api.Url}results/delete"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new GetResultsDistanceRequest
                        {
                            Slug = slug,
                            Year = year,
                            Distance = distance,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    AddResultsResponse result = JsonSerializer.Deserialize<AddResultsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown deleting results: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<AddResultsResponse> UploadBibChips(ApiObject api, string slug, string year, List<BibChip> bibChips)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Uploading bibchips.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}bibchips/add"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new AddBibChipsRequest
                        {
                            Slug = slug,
                            Year = year,
                            BibChips = bibChips,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    AddResultsResponse result = JsonSerializer.Deserialize<AddResultsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown adding bibchips: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<AddResultsResponse> DeleteBibChips(ApiObject api, string slug, string year)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Deleting bibchips.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"{api.Url}bibchips/delete"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new GetBibChipsRequest
                        {
                            Slug = slug,
                            Year = year,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    AddResultsResponse result = JsonSerializer.Deserialize<AddResultsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown deleting bibchips: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<GetBibChipsResponse> GetBibChips(ApiObject api, string slug, string year)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Getting bibchips.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}bibchips"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new GetBibChipsRequest
                        {
                            Slug = slug,
                            Year = year,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetBibChipsResponse result = JsonSerializer.Deserialize<GetBibChipsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting bibchips: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<AddResultsResponse> UploadParticipants(ApiObject api, string slug, string year, List<ApiPerson> people)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Uploading participants.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}participants/add"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new AddParticipantsRequest
                        {
                            Slug = slug,
                            Year = year,
                            Participants = people,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    AddResultsResponse result = JsonSerializer.Deserialize<AddResultsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown uploading participants: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<AddResultsResponse> DeleteParticipants(ApiObject api, string slug, string year)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Deleting participants.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"{api.Url}participants/delete"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new DeleteParticipantsRequest
                        {
                            Slug = slug,
                            Year = year
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    AddResultsResponse result = JsonSerializer.Deserialize<AddResultsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown deleting participants: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<GetParticipantsResponse> GetParticipants(ApiObject api, string slug, string year, int limit, int page)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Getting participants.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}participants"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new GetParticipantsRequest
                        {
                            Slug = slug,
                            Year = year,
                            Limit = limit,
                            Page = page,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetParticipantsResponse result = JsonSerializer.Deserialize<GetParticipantsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting participants: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<GetBannedPhonesResponse> GetBannedPhones()
        {
            string content;
            Log.D("Network.API.APIHandlers", "Getting banned phone numbers.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{Constants.ApiConstants.API_URL[Constants.ApiConstants.CHRONOKEEP_RESULTS]}blocked/phones/get"),
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetBannedPhonesResponse result = JsonSerializer.Deserialize<GetBannedPhonesResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting banned phone numbers: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<int> AddBannedPhone(string phone)
        {
            string validPhone = Constants.GlobalVars.GetValidPhone(phone);
            if (string.IsNullOrEmpty(validPhone))
            {
                throw new ApiException("Invalid phone number.");
            }
            string content;
            Log.D("Network.API.APIHandlers", "Blocking phone number.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{Constants.ApiConstants.API_URL[Constants.ApiConstants.CHRONOKEEP_RESULTS]}blocked/phones/add"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new ModifyBannedPhoneRequest
                        {
                            Phone = validPhone,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    return 200;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown blocking phone number: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task UnblockBannedPhone(string phone)
        {
            string validPhone = Constants.GlobalVars.GetValidPhone(phone);
            if (string.IsNullOrEmpty(validPhone))
            {
                throw new ApiException("Invalid phone number.");
            }
            string content;
            Log.D("Network.API.APIHandlers", "Unblocking phone number.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{Constants.ApiConstants.API_URL[Constants.ApiConstants.CHRONOKEEP_RESULTS]}blocked/phones/unblock"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new ModifyBannedPhoneRequest
                        {
                            Phone = validPhone,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    return;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown unblocking phone number: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<GetBannedEmailsResponse> GetBannedEmails()
        {
            string content;
            Log.D("Network.API.APIHandlers", "Getting banned emails.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{Constants.ApiConstants.API_URL[Constants.ApiConstants.CHRONOKEEP_RESULTS]}blocked/emails/get"),
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetBannedEmailsResponse result = JsonSerializer.Deserialize<GetBannedEmailsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting banned emails: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task AddBannedEmail(string email)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Blocking email.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{Constants.ApiConstants.API_URL[Constants.ApiConstants.CHRONOKEEP_RESULTS]}blocked/emails/add"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new ModifyBannedEmailRequest
                        {
                            Email = email,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    return;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown blocking email: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task UnblockBannedEmail(string email)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Unblocking email.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{Constants.ApiConstants.API_URL[Constants.ApiConstants.CHRONOKEEP_RESULTS]}blocked/emails/unblock"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new ModifyBannedEmailRequest
                        {
                            Email = email,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    return;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown unblocking email: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<AddSegmentsResponse> AddSegments(ApiObject api, string slug, string year, List<ApiSegment> segments)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Adding Segments.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}segments/add"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new AddSegmentsRequest
                        {
                            Slug = slug,
                            Year = year,
                            Segments = segments,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    AddSegmentsResponse result = JsonSerializer.Deserialize<AddSegmentsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown adding segments: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<DeleteSegmentsResponse> DeleteSegments(ApiObject api, string slug, string year)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Deleting Segments.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"{api.Url}segments/delete"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new DeleteSegmentsRequest
                        {
                            Slug = slug,
                            Year = year,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    DeleteSegmentsResponse result = JsonSerializer.Deserialize<DeleteSegmentsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown deleting segments: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<GetDistancesResponse> AddDistances(ApiObject api, string slug, string year, List<ApiDistance> distances)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Adding Distances.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}distances/add"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new AddDistancesRequest
                        {
                            Slug = slug,
                            Year = year,
                            Distances = distances,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetDistancesResponse result = JsonSerializer.Deserialize<GetDistancesResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown adding distances: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<DeleteDistancesResponse> DeleteDistances(ApiObject api, string slug, string year)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Deleting Distances.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"{api.Url}distances/delete"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new DeleteDistancesRequest
                        {
                            Slug = slug,
                            Year = year,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    DeleteDistancesResponse result = JsonSerializer.Deserialize<DeleteDistancesResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown deleting distances: {ex.Message}");
            }
            throw new ApiException(content);
        }

        public static async Task<GetSmsSubscriptionsResponse> GetSmsSubscriptions(ApiObject api, string slug, string year)
        {
            string content;
            Log.D("Network.API.APIHandlers", "Adding Segments.");
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{api.Url}sms"),
                    Content = new StringContent(
                        JsonSerializer.Serialize(new GetSmsSubscriptionsRequest
                        {
                            Slug = slug,
                            Year = year,
                        }),
                        Encoding.UTF8,
                        "application/json"
                        )
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AuthToken);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Network.API.APIHandlers", "Status code ok.");
                    string json = await response.Content.ReadAsStringAsync();
                    GetSmsSubscriptionsResponse result = JsonSerializer.Deserialize<GetSmsSubscriptionsResponse>(json)!;
                    return result;
                }
                Log.D("Network.API.APIHandlers", "Status code not ok.");
                string errjson = await response.Content.ReadAsStringAsync();
                ErrorResponse errresult = JsonSerializer.Deserialize<ErrorResponse>(errjson)!;
                content = errresult.Message;
            }
            catch (Exception ex)
            {
                Log.D("Network.API.APIHandlers", "Exception thrown.");
                throw new ApiException($"Exception thrown getting sms subscriptions: {ex.Message}");
            }
            throw new ApiException(content);
        }
    }
}

