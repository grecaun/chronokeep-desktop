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

using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.Runsignup;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using RegistrationState = Chronokeep.Constants.Registration;

namespace Chronokeep.Network.Runsignup
{
    /// <summary>Everything one Runsignup download fetched.</summary>
    public class RsuDownloadData
    {
        public List<RsuParticipant> Participants { get; set; } = [];
        public List<RsuRemoval> Removals { get; set; } = [];
        public List<RsuPurchase> Purchases { get; set; } = [];
        /// <summary>Every event in the race, to spot ones added since the link was set up.</summary>
        public List<RsuEvent> RaceEvents { get; set; } = [];
        /// <summary>Why store purchases couldn't be fetched, if they couldn't.</summary>
        public string StoreError { get; set; } = "";
    }

    /// <summary>What a Runsignup download did, for the summary shown afterwards.</summary>
    public class RsuDownloadSummary
    {
        /// <summary>How many registrations were downloaded from the mapped events.</summary>
        public int Downloaded { get; set; }
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Unchanged { get; set; }
        public int MarkedRemoved { get; set; }
        public int Reinstated { get; set; }
        public int TransfersDropped { get; set; }
        public int KeptReactivated { get; set; }
        public int SkippedEdited { get; set; }
        public int SkippedUnmapped { get; set; }
        public int UnmatchedPurchases { get; set; }
        public List<string> UnmappedEvents { get; } = [];
        public string StoreError { get; set; } = "";

        public string Describe()
        {
            List<string> lines =
            [
                $"Added {Added}, updated {Updated}, marked removed {MarkedRemoved}.",
            ];
            if (Reinstated > 0) lines.Add($"{Reinstated} back in the race after being removed.");
            if (TransfersDropped > 0) lines.Add($"{TransfersDropped} old entries dropped after a transfer.");
            if (KeptReactivated > 0) lines.Add($"{KeptReactivated} reactivated by hand and kept in the race, though Runsignup removed them.");
            if (SkippedEdited > 0) lines.Add($"{SkippedEdited} skipped because they were edited in Chronokeep.");
            if (SkippedUnmapped > 0) lines.Add($"{SkippedUnmapped} skipped because their Runsignup event isn't mapped to a distance.");
            if (UnmappedEvents.Count > 0) lines.Add($"Not mapped yet: {string.Join(", ", UnmappedEvents)}. Edit the Runsignup link to map them.");
            if (UnmatchedPurchases > 0) lines.Add($"{UnmatchedPurchases} store purchases couldn't be matched to a registration.");
            if (StoreError.Length > 0) lines.Add($"Store purchases weren't downloaded: {StoreError}");
            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// Downloads a linked Runsignup race into the event: participants in the mapped events, their
    /// apparel and chosen comment fields, and removals. Removed registrations are marked rather than
    /// deleted, and participants reactivated by hand are never removed again.
    /// </summary>
    public static class RunsignupDownload
    {
        public const string ExternalIdPrefix = "rsu-";
        /// <summary>The state given to the old row kept when a Runsignup bib changes.</summary>
        public static readonly string BibChanged = RegistrationState.Removed(RegistrationState.BIB_CHANGED);

        public static async Task<RsuDownloadSummary> Download(IdbInterface database, Event theEvent, Action<string>? progress = null)
        {
            if (!theEvent.IsRegistrationLinked || database.GetApi(theEvent.RegistrationApiId) is not ApiObject api)
            {
                throw new ApiException("This event isn't linked to a Runsignup race.");
            }
            RsuDownloadData data = new();
            progress?.Invoke("Race");
            // A race lists its events from every year; only this year's belong to the event.
            data.RaceEvents = EventsNearest(theEvent, (await RunsignupHandlers.GetRace(api, theEvent.RegistrationRaceId).ConfigureAwait(false)).Events);
            HashSet<string> current = [.. data.RaceEvents.Select(e => e.EventId)];
            List<string> eventIds = [.. database.GetRegistrationDistances(theEvent.Identifier)
                .Where(d => !d.IsSkipped && current.Contains(d.RsuEventId)).Select(d => d.RsuEventId)];
            if (eventIds.Count == 0)
            {
                throw new ApiException("None of this year's Runsignup events are mapped to a distance. Edit the Runsignup link to map them.");
            }
            data.Participants = await RunsignupHandlers.GetParticipants(api, theEvent.RegistrationRaceId, eventIds, page => progress?.Invoke($"Page {page}")).ConfigureAwait(false);
            progress?.Invoke("Store");
            try
            {
                data.Purchases = await RunsignupHandlers.GetStorePurchases(api, theEvent.RegistrationRaceId).ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                // A race without a store, or a key without store access, shouldn't stop the download.
                data.StoreError = ex.Message;
            }
            progress?.Invoke("Removals");
            data.Removals = await RunsignupHandlers.GetRemovedParticipants(api, theEvent.RegistrationRaceId, eventIds).ConfigureAwait(false);
            progress?.Invoke("Saving");
            // Merging and saving can take a while for a big race. The database calls aren't async, so this
            // goes to the thread pool to keep it off the caller's (UI) thread.
            return await Task.Run(() => Apply(database, theEvent, data)).ConfigureAwait(false);
        }

        /// <summary>
        /// How far from race day an event can be and still belong to the same year's race. Virtual
        /// events often run for weeks or months after race day; the previous year's are about a year away.
        /// </summary>
        private const int SameYearDays = 120;

        /// <summary>
        /// The race's events for this event: Runsignup lists a race's events from every year, so this
        /// keeps the ones held nearest the event's date, plus any within four months of those (a race
        /// weekend, or a virtual race that runs on after race day). Events without a readable start
        /// time are kept only when none have one.
        /// </summary>
        public static List<RsuEvent> EventsNearest(Event theEvent, List<RsuEvent> events)
        {
            if (!EventDates.TryParse(theEvent.Date, out DateTime eventDate)) return events;
            List<(RsuEvent Event, DateTime Date)> dated = [];
            foreach (RsuEvent ev in events)
            {
                if (TryStartDate(ev.StartTime, out DateTime date)) dated.Add((ev, date));
            }
            if (dated.Count == 0) return events;
            DateTime nearest = dated.MinBy(d => Math.Abs((d.Date - eventDate).TotalDays)).Date;
            return [.. dated.Where(d => Math.Abs((d.Date - nearest).TotalDays) <= SameYearDays).Select(d => d.Event)];
        }

        /// <summary>The day a Runsignup event starts. Runsignup writes start times US style ("6/15/2026 8:00").</summary>
        internal static bool TryStartDate(string startTime, out DateTime date)
        {
            string[] formats = ["M/d/yyyy H:mm", "M/d/yyyy h:mm tt", "M/d/yyyy HH:mm:ss", "M/d/yyyy h:mm:ss tt", "M/d/yyyy",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd"];
            if (DateTime.TryParseExact(startTime.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime parsed)
                || DateTime.TryParse(startTime, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                date = parsed.Date;
                return true;
            }
            date = default;
            return false;
        }

        /// <summary>Merges a download into the event and saves it.</summary>
        public static RsuDownloadSummary Apply(IdbInterface database, Event theEvent, RsuDownloadData data)
        {
            RsuDownloadSummary summary = new() { StoreError = data.StoreError };
            Dictionary<int, Distance> distancesById = database.GetDistances(theEvent.Identifier).ToDictionary(d => d.Identifier);
            List<RegistrationDistance> map = database.GetRegistrationDistances(theEvent.Identifier);
            Dictionary<string, Distance> distanceFor = [];
            foreach (RegistrationDistance rd in map)
            {
                if (!rd.IsSkipped && distancesById.TryGetValue(rd.DistanceId, out Distance? d))
                {
                    distanceFor[rd.RsuEventId] = d;
                }
            }
            HashSet<string> known = [.. map.Select(m => m.RsuEventId)];
            summary.UnmappedEvents.AddRange(data.RaceEvents.Where(e => !known.Contains(e.EventId)).Select(e => e.Name));

            List<RsuParticipant> incoming = [];
            foreach (RsuParticipant p in data.Participants)
            {
                if (!distanceFor.ContainsKey(p.EventId))
                {
                    summary.SkippedUnmapped++;
                    continue;
                }
                if (p.FirstName.Length == 0 && p.LastName.Length == 0) continue;
                incoming.Add(p);
            }
            summary.Downloaded = incoming.Count;
            Dictionary<string, List<RsuItem>> storeItems = MatchPurchases(incoming, data.Purchases, out int unmatched);
            Dictionary<string, string> giveaways = data.RaceEvents.GroupBy(e => e.EventId).ToDictionary(g => g.Key, g => g.First().Giveaway);
            summary.UnmatchedPurchases = unmatched;

            List<Participant> existing = database.GetParticipants(theEvent.Identifier);
            Dictionary<int, string> before = existing.ToDictionary(p => p.EventSpecific.Identifier, Signature);
            RunsignupParticipantSource source = new(theEvent, distanceFor, storeItems, giveaways);
            MergeResult result = ParticipantMerger.Merge(existing, incoming, source);
            summary.SkippedEdited = result.Skipped;
            summary.Reinstated = source.Reinstated;

            // People skipped as edited locally still take the Runsignup id and come back into the race.
            foreach (RsuParticipant p in incoming)
            {
                string key = source.Key(p);
                Participant local = result.ByKey[key];
                if (result.ToAdd.Contains(local) || result.ToUpdate.Contains(local)) continue;
                bool changed = false;
                if (local.EventSpecific.ExternalId != key)
                {
                    local.EventSpecific.ExternalId = key;
                    changed = true;
                }
                if (local.EventSpecific.IsRemoved)
                {
                    local.EventSpecific.Removed = RegistrationState.ACTIVE;
                    summary.Reinstated++;
                    changed = true;
                }
                if (changed) result.ToUpdate.Add(local);
            }

            // Removals apply to everyone the event knows by a Runsignup id.
            Dictionary<string, Participant> byKey = [];
            foreach (Participant p in existing.Where(p => p.EventSpecific.ExternalId.StartsWith(ExternalIdPrefix)))
            {
                byKey[p.EventSpecific.ExternalId] = p;
            }
            foreach ((string key, Participant p) in result.ByKey)
            {
                byKey[key] = p;
            }
            HashSet<string> active = [.. incoming.Select(source.Key)];
            List<Participant> toDelete = [];
            foreach (RsuRemoval removal in data.Removals)
            {
                string key = ExternalIdPrefix + removal.RegistrationId;
                if (active.Contains(key) || !byKey.TryGetValue(key, out Participant? local) || toDelete.Contains(local)) continue;
                if (local.EventSpecific.IsReactivated)
                {
                    summary.KeptReactivated++;
                    continue;
                }
                string reason = RegistrationState.FromRunsignup(removal.Reason);
                if (reason == RegistrationState.TRANSFERRED && removal.NewRegistrationId.Length > 0)
                {
                    // Keep the old entry only while it holds a bib the new entry doesn't have yet.
                    bool newHasBib = byKey.TryGetValue(ExternalIdPrefix + removal.NewRegistrationId, out Participant? replacement) && replacement.Bib.Length > 0;
                    if (local.Bib.Length == 0 || newHasBib)
                    {
                        result.ToUpdate.Remove(local);
                        toDelete.Add(local);
                        summary.TransfersDropped++;
                        continue;
                    }
                }
                string state = RegistrationState.Removed(reason);
                if (local.EventSpecific.Removed == state) continue;
                local.EventSpecific.Removed = state;
                summary.MarkedRemoved++;
                if (!result.ToUpdate.Contains(local)) result.ToUpdate.Add(local);
            }

            // Only count and save real changes.
            List<Participant> toUpdate = [.. result.ToUpdate.Distinct().Where(p => !before.TryGetValue(p.EventSpecific.Identifier, out string? sig) || sig != Signature(p))];
            summary.Updated = toUpdate.Count(p => !toDelete.Contains(p)) - summary.MarkedRemoved;
            if (summary.Updated < 0) summary.Updated = 0;
            summary.Unchanged = incoming.Count - result.ToAdd.Count(p => p.EventSpecific.ExternalId.Length > 0) - summary.Updated - summary.SkippedEdited;
            summary.Added = result.ToAdd.Count(p => p.EventSpecific.ExternalId.Length > 0);

            if (toUpdate.Count > 0) database.UpdateParticipants(toUpdate);
            if (result.ToAdd.Count > 0) database.AddParticipants(result.ToAdd);
            if (toDelete.Count > 0) database.RemoveParticipantEntries(toDelete);

            // Chips from Runsignup, only where both the bib and the chip are set.
            Dictionary<string, string> chips = database.GetBibChips(theEvent.Identifier).ToDictionary(bc => bc.Chip, bc => bc.Bib);
            List<BibChipAssociation> newChips = [];
            foreach (RsuParticipant p in incoming.Where(p => p.Bib.Length > 0 && p.Chip.Length > 0))
            {
                if (chips.TryGetValue(p.Chip, out string? bib) && bib == p.Bib) continue;
                newChips.Add(new BibChipAssociation { EventId = theEvent.Identifier, Bib = p.Bib, Chip = p.Chip });
                chips[p.Chip] = p.Bib;
            }
            if (newChips.Count > 0) database.AddBibChipAssociation(theEvent.Identifier, newChips);
            return summary;
        }

        /// <summary>
        /// Store purchases by registration. A purchase without a registration goes to the buyer's
        /// registration when they have exactly one; otherwise it's counted as unmatched.
        /// </summary>
        internal static Dictionary<string, List<RsuItem>> MatchPurchases(List<RsuParticipant> participants, List<RsuPurchase> purchases, out int unmatched)
        {
            unmatched = 0;
            Dictionary<string, List<RsuItem>> output = [];
            HashSet<string> registrations = [.. participants.Select(p => p.RegistrationId)];
            Dictionary<string, List<string>> byUser = participants.Where(p => p.UserId.Length > 0)
                .GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.Select(p => p.RegistrationId).ToList());
            foreach (RsuPurchase purchase in purchases)
            {
                string registration = purchase.RegistrationId;
                if (registration.Length == 0 || registration == "0")
                {
                    if (byUser.TryGetValue(purchase.UserId, out List<string>? regs) && regs.Count == 1)
                    {
                        registration = regs[0];
                    }
                    else
                    {
                        unmatched++;
                        continue;
                    }
                }
                // Purchases for registrations in events that aren't downloaded are left out.
                if (!registrations.Contains(registration)) continue;
                if (!output.TryGetValue(registration, out List<RsuItem>? items))
                {
                    items = [];
                    output[registration] = items;
                }
                items.AddRange(purchase.Items);
            }
            return output;
        }

        /// <summary>
        /// The giveaway ("Long Sleeve Shirt (Large)"), add-ons, then store items, joined with "; ".
        /// A giveaway or add-on option they turned down ("No shirt") is left out.
        /// </summary>
        internal static string Apparel(RsuParticipant p, string giveawayName, IEnumerable<RsuItem>? store)
        {
            List<RsuItem> items = [];
            string choice = p.GiveawayChoice.Trim();
            if (choice.Length > 0 && !IsDeclined(choice))
            {
                items.Add(giveawayName.Trim().Length > 0 ? new RsuItem(giveawayName.Trim(), choice, 1) : new RsuItem(choice, "", 1));
            }
            // Add-ons can carry a "No thanks" style option too.
            items.AddRange(p.AddOns.Where(a => !IsDeclined(a.Option)));
            items.AddRange(store ?? []);
            return string.Join("; ", items.Select(i => i.ToString()).Where(s => s.Length > 0));
        }

        private static readonly string[][] DeclinePrefixes =
        [
            ["no"], ["none"], ["nothing"], ["na"], ["n", "a"], ["decline"], ["declined"], ["skip"], ["opt", "out"], ["opted", "out"],
            ["dont", "want"], ["dont", "need"], ["do", "not", "want"], ["do", "not", "need"], ["i", "dont", "want"], ["i", "dont", "need"],
            ["i", "do", "not", "want"], ["i", "do", "not", "need"], ["not", "needed"], ["not", "wanted"],
        ];

        /// <summary>
        /// Whether a giveaway choice says no, such as "No shirt", "No preference", "No shirt - donate my
        /// fee", "None" or "Don't want a shirt". Only whole words count, so a choice that merely starts
        /// with the same letters ("Northwest Medium", "Nordic Blue L") is kept.
        /// </summary>
        internal static bool IsDeclined(string choice)
        {
            string[] words = Words(choice);
            return words.Length > 0 && DeclinePrefixes.Any(prefix => words.Length >= prefix.Length && words.Take(prefix.Length).SequenceEqual(prefix));
        }

        /// <summary>Lower-case words, with apostrophes dropped ("don't" is "dont") and other punctuation as spaces.</summary>
        private static string[] Words(string text) =>
            new string([.. text.ToLowerInvariant().Replace("'", "").Replace("\u2019", "").Select(c => char.IsLetterOrDigit(c) ? c : ' ')])
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// The chosen fields (a comma list such as "q:51234,team_name") in order, joined with " | ",
        /// leaving out empty answers.
        /// </summary>
        internal static string Comments(RsuParticipant p, string fields)
        {
            List<string> parts = [];
            foreach (string field in fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string value = field switch
                {
                    CommentFields.TeamName => p.TeamName,
                    CommentFields.Age => p.Age,
                    CommentFields.CityState => string.Join(", ", new[] { p.City, p.State }.Where(s => s.Length > 0)),
                    _ when field.StartsWith(CommentFields.QuestionPrefix) =>
                        p.Answers.FirstOrDefault(a => a.QuestionId == field[CommentFields.QuestionPrefix.Length..])?.Response ?? "",
                    _ => "",
                };
                if (value.Trim().Length > 0) parts.Add(value.Trim());
            }
            return string.Join(" | ", parts);
        }

        /// <summary>Runsignup birthdates as yyyy/MM/dd. Runsignup writes them year first or US style.</summary>
        internal static string Birthdate(string dob)
        {
            string[] formats = ["yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy", "M/d/yyyy", "yyyy/MM/dd"];
            return DateTime.TryParseExact(dob.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
                ? date.ToString(Birthdates.Format, CultureInfo.InvariantCulture)
                : Birthdates.Normalize(dob);
        }

        private static string Signature(Participant p) => string.Join("\u001f",
            p.FirstName, p.LastName, p.Birthdate, p.Gender, p.Email, p.Phone, p.Street, p.City, p.State, p.Zip, p.Country,
            p.EventSpecific.Bib, p.EventSpecific.DistanceIdentifier, p.EventSpecific.Apparel, p.EventSpecific.Comments,
            p.EventSpecific.Anonymous, p.EventSpecific.ExternalId, p.EventSpecific.Removed);
    }

    /// <summary>The built-in comment fields the link wizard offers next to race questions.</summary>
    public static class CommentFields
    {
        public const string QuestionPrefix = "q:";
        public const string TeamName = "team_name";
        public const string Age = "age";
        public const string CityState = "city_state";

        public static readonly (string Field, string Name)[] BuiltIn =
        [
            (TeamName, "Team name"),
            (Age, "Age"),
            (CityState, "City, State"),
        ];
    }

    /// <summary>Runsignup people for <see cref="ParticipantMerger"/>, matched on rsu-{registration_id}.</summary>
    internal class RunsignupParticipantSource(Event theEvent, Dictionary<string, Distance> distanceFor, Dictionary<string, List<RsuItem>> storeItems,
        Dictionary<string, string> giveaways) : MergeSource<RsuParticipant>
    {
        /// <summary>How many removed participants this download found back in the race.</summary>
        public int Reinstated { get; private set; }

        public override string LocalKey(Participant local) =>
            local.EventSpecific.ExternalId.StartsWith(RunsignupDownload.ExternalIdPrefix) ? local.EventSpecific.ExternalId : "";

        public override string Key(RsuParticipant item) => RunsignupDownload.ExternalIdPrefix + item.RegistrationId;

        public override (string First, string Last, string Birthdate, string Distance) BasicKey(RsuParticipant item) =>
            (item.FirstName, item.LastName, RunsignupDownload.Birthdate(item.Birthdate), distanceFor[item.EventId].Name);

        // Name matching is only for people entered before the link existed, never old or removed entries.
        public override bool CanMatchByName(Participant local) => local.EventSpecific.ExternalId.Length == 0 && !local.EventSpecific.IsRemoved;

        public override Participant? OldBibRow(Participant local)
        {
            local.Identifier = -1;
            local.EventSpecific.ExternalId = "";
            local.EventSpecific.Removed = RunsignupDownload.BibChanged;
            return local;
        }

        public override Participant Updated(RsuParticipant item, Participant old)
        {
            if (old.EventSpecific.IsRemoved) Reinstated++;
            return Build(item, old);
        }

        public override Participant Created(RsuParticipant item) => Build(item, null);

        private Participant Build(RsuParticipant item, Participant? old)
        {
            Distance distance = distanceFor[item.EventId];
            EventSpecific es = new(
                old?.EventSpecific.Identifier ?? -1,
                theEvent.Identifier,
                distance.Identifier,
                distance.Name,
                item.Bib,
                old?.EventSpecific.CheckedIn ?? 0,
                theEvent.RegistrationCommentFields.Length > 0
                    ? RunsignupDownload.Comments(item, theEvent.RegistrationCommentFields)
                    : old?.EventSpecific.Comments ?? "",
                old?.EventSpecific.Owes ?? "",
                old?.EventSpecific.Other ?? "",
                old?.EventSpecific.Status ?? Constants.Timing.EVENTSPECIFIC_UNKNOWN,
                old?.EventSpecific.AgeGroupName ?? "",
                old?.EventSpecific.AgeGroupId ?? Constants.Timing.TIMERESULT_DUMMYAGEGROUP,
                item.Anonymous,
                old?.EventSpecific.SmsEnabled ?? false,
                RunsignupDownload.Apparel(item, giveaways.GetValueOrDefault(item.EventId) ?? "", storeItems.GetValueOrDefault(item.RegistrationId)),
                old?.EventSpecific.Division ?? "",
                old?.EventSpecific.Version ?? Constants.Timing.EVENTSPECIFIC_DEFAULT_VERSION,
                old?.EventSpecific.Version ?? Constants.Timing.EVENTSPECIFIC_DEFAULT_VERSION)
            {
                ExternalId = Key(item),
                // Someone reactivated by hand stays that way, so later removals leave them in the race.
                Removed = old is { EventSpecific.IsReactivated: true } ? old.EventSpecific.Removed : Constants.Registration.ACTIVE,
            };
            Participant output = new(
                old?.Identifier ?? Constants.Timing.PARTICIPANT_DUMMYIDENTIFIER,
                item.FirstName,
                item.LastName,
                item.Street,
                item.City,
                item.State,
                item.Zip,
                RunsignupDownload.Birthdate(item.Birthdate),
                es,
                item.Email,
                item.Phone,
                old?.Mobile ?? "",
                old?.Parent ?? "",
                item.Country,
                // Runsignup has one street line; FormatData splits off an apartment after a comma, so start empty each time.
                "",
                item.Gender,
                old?.EcName ?? "",
                old?.EcPhone ?? "");
            output.FormatData(theEvent.UseMaleFemale);
            return output;
        }
    }
}
