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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Chronokeep.Objects.Runsignup
{
    /// <summary>Who a Runsignup key belongs to (partner, race...), from entity-info.</summary>
    public record RsuEntityInfo(string Type, string Name, string SpecificId)
    {
        /// <summary>"Partner: Your Timing Co."</summary>
        public string Describe() => $"{(Type.Length > 0 ? char.ToUpperInvariant(Type[0]) + Type[1..] : "Account")}: {Name}";
    }

    /// <summary>
    /// One event in a Runsignup race, such as the 5K or the Kids 1 Mile. Giveaway is the item that
    /// comes with registration ("Long Sleeve Shirt"); each participant's choice holds only the option.
    /// </summary>
    public record RsuEvent(string EventId, string Name, string StartTime, string Distance, string Giveaway = "");

    /// <summary>A Runsignup race. NextDate is its next date, LastDate the last one it was held (MM/dd/yyyy).</summary>
    public record RsuRace(string RaceId, string Name, string NextDate, List<RsuEvent> Events, string LastDate = "")
    {
        public override string ToString() => NextDate.Length > 0 ? $"{Name} ({NextDate})" : LastDate.Length > 0 ? $"{Name} ({LastDate})" : Name;
    }

    /// <summary>One thing bought: a giveaway, a registration add-on or a store item.</summary>
    public record RsuItem(string Name, string Option, int Quantity)
    {
        /// <summary>"Race Tee (M)", "Quarter-Zip (L) x2", or just "Mug" when there's no option.</summary>
        public override string ToString()
        {
            string text = Option.Length > 0 ? $"{Name} ({Option})" : Name;
            return Quantity > 1 ? $"{text} x{Quantity}" : text;
        }
    }

    /// <summary>An answer to a race question. Checkbox answers are joined with commas.</summary>
    public record RsuAnswer(string QuestionId, string QuestionText, string Response);

    public class RsuParticipant
    {
        public string RegistrationId { get; set; } = "";
        public string EventId { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Bib { get; set; } = "";
        public string Chip { get; set; } = "";
        public string Age { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Birthdate { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Zip { get; set; } = "";
        public string Country { get; set; } = "";
        public string TeamName { get; set; } = "";
        public bool Anonymous { get; set; }
        /// <summary>
        /// The giveaway option they chose, such as "Large" or "No shirt". The item itself is named on
        /// the event (<see cref="RsuEvent.Giveaway"/>).
        /// </summary>
        public string GiveawayChoice { get; set; } = "";
        public List<RsuItem> AddOns { get; set; } = [];
        public List<RsuAnswer> Answers { get; set; } = [];
    }

    /// <summary>A registration Runsignup removed, and why. Transfers name the registration that replaced it.</summary>
    public record RsuRemoval(string RegistrationId, string EventId, string Reason, string NewRegistrationId);

    /// <summary>A race store order. RegistrationId is empty when it wasn't tied to a registration.</summary>
    public record RsuPurchase(string RegistrationId, string UserId, List<RsuItem> Items);

    /// <summary>One page of store purchases, and the token for the next page (empty on the last).</summary>
    public record RsuPurchasePage(List<RsuPurchase> Purchases, string NextToken);

    /// <summary>
    /// Reads Runsignup's JSON. It is read leniently: ids come back as numbers or strings, lists come
    /// back as one object or an array, and the giveaway and add-on shapes aren't documented.
    /// </summary>
    public static class RsuJson
    {
        /// <summary>Runsignup reports errors as {"error":{"error_code":..,"error_msg":".."}}, sometimes with a 200.</summary>
        public static string? Error(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out JsonElement error)) return null;
            string message = error.ValueKind == JsonValueKind.String ? Text(error) : First(error, "error_msg", "message");
            return message.Length > 0 ? message : "Runsignup returned an error.";
        }

        public static RsuEntityInfo EntityInfo(JsonElement root) =>
            new(Str(root, "entity_type"), Str(root, "entity_name"), Str(root, "entity_specific_id"));

        public static List<RsuRace> Races(JsonElement root)
        {
            List<RsuRace> output = [];
            if (!root.TryGetProperty("races", out JsonElement races)) return output;
            foreach (JsonElement item in Items(races))
            {
                output.Add(Race(item));
            }
            return output;
        }

        /// <summary>A race, either bare or wrapped as {"race": {...}}.</summary>
        public static RsuRace Race(JsonElement race)
        {
            if (race.ValueKind == JsonValueKind.Object && race.TryGetProperty("race", out JsonElement inner)) race = inner;
            List<RsuEvent> events = [];
            if (race.ValueKind == JsonValueKind.Object && race.TryGetProperty("events", out JsonElement list))
            {
                foreach (JsonElement ev in Items(list))
                {
                    JsonElement e = ev.ValueKind == JsonValueKind.Object && ev.TryGetProperty("event", out JsonElement wrapped) ? wrapped : ev;
                    events.Add(new RsuEvent(Str(e, "event_id"), Str(e, "name"), Str(e, "start_time"), Str(e, "distance"), Str(e, "giveaway")));
                }
            }
            return new RsuRace(Str(race, "race_id"), Str(race, "name"), Str(race, "next_date"), events, Str(race, "last_date"));
        }

        /// <summary>
        /// The participants call answers with one {event, participants} block per event; with a single
        /// event it can be a bare object instead of an array.
        /// </summary>
        public static List<RsuParticipant> Participants(JsonElement root)
        {
            List<RsuParticipant> output = [];
            foreach (JsonElement block in Items(root))
            {
                string blockEventId = BlockEventId(block);
                foreach (JsonElement p in ParticipantList(block))
                {
                    RsuParticipant part = Participant(p);
                    if (part.EventId.Length == 0) part.EventId = blockEventId;
                    output.Add(part);
                }
            }
            return output;
        }

        public static List<RsuRemoval> Removals(JsonElement root)
        {
            List<RsuRemoval> output = [];
            foreach (JsonElement block in Items(root))
            {
                string blockEventId = BlockEventId(block);
                foreach (JsonElement p in ParticipantList(block))
                {
                    string eventId = Str(p, "event_id");
                    output.Add(new RsuRemoval(
                        Str(p, "registration_id"),
                        eventId.Length > 0 ? eventId : blockEventId,
                        Str(p, "removed_reason").ToLowerInvariant(),
                        Str(p, "new_registration_id")));
                }
            }
            return output;
        }

        public static RsuPurchasePage Purchases(JsonElement root)
        {
            List<RsuPurchase> purchases = [];
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("purchases", out JsonElement list))
            {
                foreach (JsonElement p in Items(list))
                {
                    List<RsuItem> items = [];
                    if (p.TryGetProperty("items", out JsonElement itemList))
                    {
                        items.AddRange(Items(itemList).Select(Item).Where(i => i.Name.Length > 0));
                    }
                    purchases.Add(new RsuPurchase(Str(p, "registration_id"), Str(p, "user_id"), items));
                }
            }
            return new RsuPurchasePage(purchases, NextToken(Str(root, "next_url")));
        }

        /// <summary>Pulls next_token out of a next_url, so the next page is asked for with our own credentials.</summary>
        internal static string NextToken(string nextUrl)
        {
            if (nextUrl.Length == 0) return "";
            int q = nextUrl.IndexOf('?');
            foreach (string pair in (q >= 0 ? nextUrl[(q + 1)..] : nextUrl).Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && pair[..eq] == "next_token") return Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
            return "";
        }

        private static RsuParticipant Participant(JsonElement p)
        {
            JsonElement user = p.TryGetProperty("user", out JsonElement u) ? u : default;
            JsonElement address = user.ValueKind == JsonValueKind.Object && user.TryGetProperty("address", out JsonElement a) ? a : default;
            RsuParticipant part = new()
            {
                RegistrationId = Str(p, "registration_id"),
                EventId = Str(p, "event_id"),
                UserId = Str(user, "user_id"),
                Bib = Str(p, "bib_num"),
                Chip = Str(p, "chip_num"),
                Age = Str(p, "age"),
                FirstName = Str(user, "first_name"),
                LastName = Str(user, "last_name"),
                Birthdate = Str(user, "dob"),
                Gender = Str(user, "gender"),
                Email = Str(user, "email"),
                Phone = Str(user, "phone"),
                Street = Str(address, "street"),
                City = Str(address, "city"),
                State = Str(address, "state"),
                Zip = Str(address, "zipcode"),
                Country = Str(address, "country_code"),
                TeamName = Str(p, "team_name"),
                Anonymous = Flag(user, "is_anonymous") || Flag(p, "is_anonymous"),
            };
            if (p.TryGetProperty("giveaway", out JsonElement giveaway))
            {
                part.GiveawayChoice = giveaway.ValueKind == JsonValueKind.Object
                    ? First(giveaway, "giveaway_option", "option", "option_name", "name", "giveaway")
                    : Text(giveaway);
            }
            foreach (string name in new[] { "participant_addons", "registration_addons", "addons", "add_ons" })
            {
                if (p.TryGetProperty(name, out JsonElement addOns))
                {
                    part.AddOns.AddRange(Items(addOns).SelectMany(AddOn).Where(i => i.Name.Length > 0));
                }
            }
            if (p.TryGetProperty("question_responses", out JsonElement questions))
            {
                foreach (JsonElement q in Items(questions))
                {
                    string response = Str(q, "response");
                    if (response.Length == 0 && q.TryGetProperty("responses", out JsonElement many))
                    {
                        response = string.Join(", ", Items(many).Select(Text).Where(r => r.Length > 0));
                    }
                    part.Answers.Add(new RsuAnswer(Str(q, "question_id"), Str(q, "question_text"), response));
                }
            }
            return part;
        }

        /// <summary>
        /// An item may be plain text ("T-Shirt (M)") or an object. Objects are read by the names Runsignup
        /// uses for store items (item_name, item_option, quantity) and the likely ones for add-ons.
        /// </summary>
        /// <summary>
        /// A registration add-on. Runsignup sends each chosen option on its own, with its own quantity:
        /// {"addon_name":"T-Shirt","quantity":"1","participant_addon_options":[{"participant_addon_option_text":"Small","quantity":"1"}]}.
        /// Each option becomes its own item ("T-Shirt (Small)").
        /// </summary>
        private static IEnumerable<RsuItem> AddOn(JsonElement e)
        {
            RsuItem addOn = Item(e);
            List<RsuItem> options = [];
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("participant_addon_options", out JsonElement list))
            {
                foreach (JsonElement option in Items(list))
                {
                    string text = option.ValueKind == JsonValueKind.Object ? First(option, "participant_addon_option_text", "option_text", "option") : Text(option);
                    if (text.Length == 0) continue;
                    int quantity = int.TryParse(Str(option, "quantity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int q) && q > 0 ? q : 1;
                    options.Add(addOn with { Option = text, Quantity = quantity });
                }
            }
            return options.Count > 0 ? options : [addOn];
        }

        private static RsuItem Item(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return new RsuItem(Text(e), "", 1);
            string name = First(e, "item_name", "addon_name", "name", "giveaway", "giveaway_name", "addon_desc", "description");
            string option = First(e, "item_option", "addon_option", "option", "option_name", "selected_option", "giveaway_option");
            int quantity = int.TryParse(First(e, "quantity", "num", "qty"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int q) && q > 0 ? q : 1;
            return new RsuItem(name, option, quantity);
        }

        private static string BlockEventId(JsonElement block) =>
            block.ValueKind == JsonValueKind.Object && block.TryGetProperty("event", out JsonElement ev) ? Str(ev, "event_id") : "";

        private static IEnumerable<JsonElement> ParticipantList(JsonElement block)
        {
            if (block.ValueKind != JsonValueKind.Object) return [];
            if (block.TryGetProperty("participants", out JsonElement list) && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0) return Items(list);
            if (block.TryGetProperty("event", out JsonElement ev) && ev.ValueKind == JsonValueKind.Object
                && ev.TryGetProperty("participants", out JsonElement nested)) return Items(nested);
            return [];
        }

        /// <summary>An array's elements, a lone value as one element, and nothing for null.</summary>
        private static IEnumerable<JsonElement> Items(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Array => e.EnumerateArray().Where(x => x.ValueKind != JsonValueKind.Null).ToList(),
            JsonValueKind.Object or JsonValueKind.String or JsonValueKind.Number => [e],
            _ => [],
        };

        private static string First(JsonElement e, params string[] names)
        {
            foreach (string name in names)
            {
                string value = Str(e, name);
                if (value.Length > 0) return value;
            }
            return "";
        }

        internal static string Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) ? Text(v) : "";

        private static string Text(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.String => v.GetString()!.Trim(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "T",
            JsonValueKind.False => "F",
            _ => "",
        };

        private static bool Flag(JsonElement e, string name)
        {
            string v = Str(e, name);
            return v is "T" or "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
