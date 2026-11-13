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

using Chronokeep.Objects;
using System;
using System.Collections.Generic;

namespace Chronokeep.Helpers
{
    /// <summary>
    /// How one download source (the Chronokeep API, Runsignup) describes its people to
    /// <see cref="ParticipantMerger"/>: how to match them to local participants and how to build
    /// the updated or new participant.
    /// </summary>
    internal abstract class MergeSource<T>
    {
        /// <summary>The source's id for a local participant, or "" if it didn't come from this source.</summary>
        public abstract string LocalKey(Participant local);
        /// <summary>The source's id for a downloaded person.</summary>
        public abstract string Key(T item);
        /// <summary>First, last, birthdate and distance name, for people not matched by id.</summary>
        public abstract (string First, string Last, string Birthdate, string Distance) BasicKey(T item);
        /// <summary>Whether a person matched by id really is that local participant.</summary>
        public virtual bool IsSamePerson(T item, Participant local) => true;
        /// <summary>Whether a local participant may be matched by name, birthdate and distance.</summary>
        public virtual bool CanMatchByName(Participant local) => true;
        /// <summary>Whether this download may change the local participant. False skips them.</summary>
        public virtual bool CanUpdate(T item, Participant local) => !WasEditedLocally(local);
        public abstract Participant Updated(T item, Participant local);
        public abstract Participant Created(T item);
        /// <summary>
        /// When a bib changes, the old entry can be kept as its own row so results on the old bib still
        /// have a name. Returns that row, or null to keep nothing.
        /// </summary>
        public virtual Participant? OldBibRow(Participant local)
        {
            local.Identifier = -1;
            return local;
        }

        /// <summary>Edited in Chronokeep since it was last uploaded or downloaded.</summary>
        public static bool WasEditedLocally(Participant local) =>
            local.EventSpecific.UploadedVersion < local.EventSpecific.Version;
    }

    internal class MergeResult
    {
        public List<Participant> ToAdd { get; } = [];
        public List<Participant> ToUpdate { get; } = [];
        /// <summary>People a download left alone because they were edited in Chronokeep, or the source said not to.</summary>
        public int Skipped { get; set; }
        /// <summary>Every downloaded person's local participant after the merge, by the source's id.</summary>
        public Dictionary<string, Participant> ByKey { get; } = [];
    }

    /// <summary>
    /// Merges downloaded people into an event's participants. A person is matched by the source's id
    /// first, then by first name, last name, birthdate and distance. Participants edited in Chronokeep
    /// since the last upload aren't overwritten. Nothing is saved; the caller saves ToAdd and ToUpdate.
    /// </summary>
    internal static class ParticipantMerger
    {
        public static MergeResult Merge<T>(List<Participant> existing, IEnumerable<T> incoming, MergeSource<T> source)
        {
            MergeResult result = new();
            Dictionary<(string, string, string, string), Participant> byName = [];
            Dictionary<string, Participant> byKey = [];
            foreach (Participant p in existing)
            {
                if (source.CanMatchByName(p))
                {
                    byName[(p.FirstName, p.LastName, p.Birthdate, p.Distance.ToLower())] = p;
                }
                string key = source.LocalKey(p);
                if (key.Length > 0)
                {
                    byKey[key] = p;
                }
            }
            HashSet<Participant> used = [];
            foreach (T item in incoming)
            {
                string key = source.Key(item);
                (string first, string last, string birthdate, string distance) = source.BasicKey(item);
                Participant? old = null;
                if (byKey.TryGetValue(key, out Participant? keyed) && !used.Contains(keyed) && source.IsSamePerson(item, keyed))
                {
                    old = keyed;
                }
                else if (byName.TryGetValue((first, last, birthdate, distance.ToLower()), out Participant? named) && !used.Contains(named))
                {
                    old = named;
                }
                if (old == null)
                {
                    Participant created = source.Created(item);
                    result.ToAdd.Add(created);
                    result.ByKey[key] = created;
                    continue;
                }
                used.Add(old);
                if (!source.CanUpdate(item, old))
                {
                    result.Skipped++;
                    result.ByKey[key] = old;
                    continue;
                }
                Participant updated = source.Updated(item, old);
                if (old.Bib.Length > 0 && !old.Bib.Equals(updated.Bib, StringComparison.OrdinalIgnoreCase)
                    && source.OldBibRow(old) is Participant oldRow)
                {
                    result.ToAdd.Add(oldRow);
                }
                result.ToUpdate.Add(updated);
                result.ByKey[key] = updated;
            }
            return result;
        }
    }
}
