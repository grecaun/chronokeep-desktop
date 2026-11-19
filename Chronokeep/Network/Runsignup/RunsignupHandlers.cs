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
using Chronokeep.Objects.Runsignup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chronokeep.Network.Runsignup
{
    /// <summary>
    /// Calls the Runsignup REST API with v2 credentials: the key goes in the rsu_api_key query
    /// parameter and the secret in the X-RSU-API-SECRET header, so the secret never appears in a URL.
    /// Calls run one at a time, well under Runsignup's limit of two at once.
    /// </summary>
    public static class RunsignupHandlers
    {
        public const int ParticipantsPerPage = 250;
        public const int RemovalsPerPage = 250;
        public const int PurchasesPerPage = 500;
        private const string Tag = "Network.Runsignup.RunsignupHandlers";

        /// <summary>Where requests go. Tests point this at a local server.</summary>
        internal static string BaseUrl { get; set; } = Constants.ApiConstants.RUNSIGNUP_BASE_URL;

        private static readonly SemaphoreSlim OneAtATime = new(1, 1);

        /// <summary>Who the key belongs to. Used by the Test button to check the key and secret work.</summary>
        public static async Task<RsuEntityInfo> GetEntityInfo(ApiObject api) =>
            RsuJson.EntityInfo(await Get(api, "rest/v2/auth-info/entity-info.json", [], "checking the Runsignup key"));

        /// <summary>
        /// The races the key's account times, with their events, soonest first. Runsignup only lists
        /// races held on or after <paramref name="from"/> (today when not given).
        /// </summary>
        public static async Task<List<RsuRace>> GetRaces(ApiObject api, DateTime? from = null)
        {
            List<RsuRace> output = [];
            for (int page = 1; ; page++)
            {
                Dictionary<string, string> query = new()
                {
                    ["events"] = "T",
                    ["only_partner_races"] = "T",
                    ["sort"] = "date ASC",
                    ["page"] = page.ToString(),
                    ["results_per_page"] = "1000",
                };
                if (from is DateTime start)
                {
                    query["start_date"] = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
                List<RsuRace> races = RsuJson.Races(await Get(api, "rest/races", query, "getting races"));
                output.AddRange(races);
                if (races.Count < 1000) break;
            }
            return output;
        }

        public static async Task<RsuRace> GetRace(ApiObject api, string raceId) =>
            RsuJson.Race(await Get(api, $"rest/race/{Uri.EscapeDataString(raceId)}", [], "getting the race"));

        /// <summary>Every participant in the given events, with add-ons, question answers and the anonymous flag.</summary>
        public static async Task<List<RsuParticipant>> GetParticipants(ApiObject api, string raceId, IEnumerable<string> eventIds, Action<int>? onPage = null)
        {
            List<RsuParticipant> output = [];
            List<string> events = [.. eventIds];
            for (int page = 1; ; page++)
            {
                onPage?.Invoke(page);
                List<RsuParticipant> participants = await GetParticipantsPage(api, raceId, events, page, ParticipantsPerPage);
                output.AddRange(participants);
                if (participants.Count < ParticipantsPerPage) break;
            }
            return output;
        }

        /// <summary>One page of participants, such as a sample for listing race questions.</summary>
        public static async Task<List<RsuParticipant>> GetParticipantsPage(ApiObject api, string raceId, IEnumerable<string> eventIds, int page, int perPage) =>
            RsuJson.Participants(await Get(api, $"rest/race/{Uri.EscapeDataString(raceId)}/participants", new()
            {
                ["event_id"] = string.Join(",", eventIds),
                ["page"] = page.ToString(),
                ["results_per_page"] = perPage.ToString(),
                ["sort"] = "registration_id ASC",
                ["include_registration_addons"] = "T",
                ["include_questions"] = "T",
                ["include_user_anonymous_flag"] = "T",
                ["supports_nb"] = "T",
            }, "getting participants"));

        /// <summary>Registrations removed from the given events, and why.</summary>
        public static async Task<List<RsuRemoval>> GetRemovedParticipants(ApiObject api, string raceId, IEnumerable<string> eventIds)
        {
            List<RsuRemoval> output = [];
            string events = string.Join(",", eventIds);
            for (int page = 1; ; page++)
            {
                List<RsuRemoval> removals = RsuJson.Removals(await Get(api, $"rest/race/{Uri.EscapeDataString(raceId)}/removed-participants", new()
                {
                    ["event_id"] = events,
                    ["page"] = page.ToString(),
                    ["results_per_page"] = RemovalsPerPage.ToString(),
                    ["sort"] = "registration_id ASC",
                }, "getting removed participants"));
                output.AddRange(removals);
                if (removals.Count < RemovalsPerPage) break;
            }
            return output;
        }

        /// <summary>Everything bought in the race store, following next_url until the last page.</summary>
        public static async Task<List<RsuPurchase>> GetStorePurchases(ApiObject api, string raceId)
        {
            List<RsuPurchase> output = [];
            string token = "";
            HashSet<string> seen = [];
            do
            {
                Dictionary<string, string> query = new()
                {
                    ["race_id"] = raceId,
                    ["num"] = PurchasesPerPage.ToString(),
                };
                if (token.Length > 0) query["next_token"] = token;
                RsuPurchasePage page = RsuJson.Purchases(await Get(api, "rest/v2/race-store/get-race-store-purchases.json", query, "getting store purchases"));
                output.AddRange(page.Purchases);
                token = page.NextToken;
            } while (token.Length > 0 && seen.Add(token));
            return output;
        }

        private static async Task<JsonElement> Get(ApiObject api, string path, Dictionary<string, string> query, string doing)
        {
            query["rsu_api_key"] = api.AuthToken;
            query["format"] = "json";
            string url = $"{BaseUrl}{path}?{string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"))}";
            // ConfigureAwait(false): replies are read and parsed off the UI thread.
            await OneAtATime.WaitAsync().ConfigureAwait(false);
            try
            {
                using HttpClient client = Chronokeep.Network.Util.Helpers.NewHttpClient(TimeSpan.FromSeconds(60));
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                request.Headers.Add("X-RSU-API-SECRET", api.Secret);
                using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JsonElement root;
                try
                {
                    root = JsonDocument.Parse(json).RootElement.Clone();
                }
                catch (JsonException)
                {
                    Log.D(Tag, $"Unreadable response {doing}: {(int)response.StatusCode}");
                    throw new ApiException($"Runsignup sent a reply that couldn't be read while {doing} ({(int)response.StatusCode} {response.ReasonPhrase}).");
                }
                if (RsuJson.Error(root) is string error)
                {
                    throw new ApiException($"Runsignup error while {doing}: {error}");
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new ApiException($"Runsignup error while {doing}: {(int)response.StatusCode} {response.ReasonPhrase}");
                }
                return root;
            }
            catch (ApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.D(Tag, $"Exception {doing}: {ex.Message}");
                throw new ApiException($"Couldn't reach Runsignup while {doing}: {ex.Message}");
            }
            finally
            {
                OneAtATime.Release();
            }
        }
    }
}
