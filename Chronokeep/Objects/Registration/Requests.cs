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

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Chronokeep.Objects.Registration
{
    public class Request
    {
        public const string GET_PARTICIPANTS       = "participant_get";
        public const string UPDATE_PARTICIPANT     = "participant_update";
        public const string ADD_PARTICIPANT        = "participant_add";
        public const string ADD_UPDATE_PARTICIPANT = "participant_add_update";
        public const string DISCONNECT             = "disconnect";
        public const string CONNECT                = "connect";

        [JsonPropertyName("command")]
        public string Command { get; init; } = "";
    }

    public class ModifyParticipant : Request
    {
        [JsonPropertyName("participant")]
        public Participant Participant { get; init; } = new();
    }

    public class ModifyMultipleParticipants : Request
    {
        [JsonPropertyName("participants")]
        public List<Participant> Participants { get; init; } = [];
    }
}

