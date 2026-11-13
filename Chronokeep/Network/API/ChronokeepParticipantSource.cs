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
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.Network.API
{
    /// <summary>
    /// People downloaded from Chronokeep Results. They are matched on the id this computer gave them
    /// when uploading ({program id}-{entry id}), and only people with a bib are updated.
    /// </summary>
    internal class ChronokeepParticipantSource(Event theEvent, string uniqueId, Dictionary<string, Distance> distances) : MergeSource<ApiPerson>
    {
        /// <summary>
        /// Saves the downloaded people into the event, adding any distance they name that the event
        /// doesn't have yet.
        /// </summary>
        public static MergeResult Download(IdbInterface database, Event theEvent, List<ApiPerson> people)
        {
            string uniqueId = database.GetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER) is AppSetting programId ? $"{programId.Value}-" : "";
            Dictionary<string, Distance> distances = [];
            foreach (Distance d in database.GetDistances(theEvent.Identifier))
            {
                distances[d.Name.ToLower()] = d;
            }
            List<ApiPerson> valid = [];
            foreach (ApiPerson person in people)
            {
                person.Trim();
                person.FormatData();
                // A person must have a distance and a first or last name. Everything else is optional.
                if (person.Distance.Length < 1 || (person.First.Length < 1 && person.Last.Length < 1))
                {
                    continue;
                }
                if (!distances.ContainsKey(person.Distance.ToLower()))
                {
                    Distance newDistance = new(person.Distance, theEvent.Identifier);
                    newDistance.Identifier = database.AddDistance(newDistance);
                    distances[newDistance.Name.ToLower()] = newDistance;
                }
                valid.Add(person);
            }
            MergeResult result = ParticipantMerger.Merge(database.GetParticipants(theEvent.Identifier), valid, new ChronokeepParticipantSource(theEvent, uniqueId, distances));
            if (result.ToUpdate.Count > 0)
            {
                database.UpdateParticipants(result.ToUpdate);
            }
            if (result.ToAdd.Count > 0)
            {
                database.AddParticipants(result.ToAdd);
            }
            return result;
        }

        private Distance DistanceOf(ApiPerson person) => distances[person.Distance.ToLower()];

        public override string LocalKey(Participant local) => $"{uniqueId}{local.EventSpecific.Identifier}";

        public override string Key(ApiPerson item) => item.Identifier;

        public override (string First, string Last, string Birthdate, string Distance) BasicKey(ApiPerson item) =>
            (item.First, item.Last, item.Birthdate, item.Distance);

        public override bool IsSamePerson(ApiPerson item, Participant local) => local.IsSimilar(item);

        // Only update if a bib exists and it hasn't been changed in Chronokeep since it was uploaded.
        public override bool CanUpdate(ApiPerson item, Participant local) => item.Bib.Length > 0 && !WasEditedLocally(local);

        public override Participant Updated(ApiPerson person, Participant old) => new(
            old.Identifier,
            person.First.Length > 0 ? person.First : old.FirstName,
            person.Last.Length > 0 ? person.Last : old.LastName,
            old.Street,
            old.City,
            old.State,
            old.Zip,
            person.Birthdate,
            new EventSpecific(
                old.EventSpecific.Identifier,
                theEvent.Identifier,
                DistanceOf(person).Identifier,
                DistanceOf(person).Name,
                person.Bib,
                old.EventSpecific.CheckedIn,
                old.EventSpecific.Comments,
                old.EventSpecific.Owes,
                old.EventSpecific.Other,
                old.EventSpecific.Status,
                old.EventSpecific.AgeGroupName,
                old.EventSpecific.AgeGroupId,
                person.Anonymous,
                person.SmsEnabled,
                person.Apparel,
                old.EventSpecific.Division,
                old.EventSpecific.Version,
                old.EventSpecific.UploadedVersion
            )
            {
                ExternalId = old.EventSpecific.ExternalId,
                Removed = old.EventSpecific.Removed,
            },
            old.Email,
            old.Phone,
            person.Mobile.Length > 0 ? person.Mobile : old.Mobile,
            old.Parent,
            old.Country,
            old.Street2,
            person.Gender,
            old.EcName,
            old.EcPhone
        );

        public override Participant Created(ApiPerson person) => new(
            person.First,
            person.Last,
            "",
            "",
            "",
            "",
            person.Birthdate,
            new EventSpecific(
                theEvent.Identifier,
                DistanceOf(person).Identifier,
                DistanceOf(person).Name,
                person.Bib,
                0,
                "",
                "",
                "",
                person.Anonymous,
                person.SmsEnabled,
                person.Apparel,
                ""
            ),
            "",
            "",
            person.Mobile,
            "",
            "",
            "",
            person.Gender,
            "",
            ""
        );
    }
}
