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
    public class Response
    {
        protected const string ERROR = "registration_error";
        protected const string DISCONNECT = "disconnect";
        protected const string PARTICIPANTS = "registration_participants";
        protected const string CONNECTION_SUCCESSFUL = "registration_connection_successful";

        [JsonPropertyName("command")]
        public string Command { get; set; } = "";
    }

    public class ConnectionSuccessfulResponse : Response
    {
        public ConnectionSuccessfulResponse()
        {
            Command = CONNECTION_SUCCESSFUL;
        }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("kind")]
        public string Type { get; set; } = "";
        [JsonPropertyName("version")]
        public int Version { get; set; }
    }

    public class ParticipantsResponse : Response
    {
        public ParticipantsResponse()
        {
            Command = PARTICIPANTS;
        }

        [JsonPropertyName("participants")]
        public List<Participant> Participants { get; set; } = [];
        [JsonPropertyName("distances")]
        public List<string> Distances { get; set; } = [];
    }

    public class ErrorResponse : Response
    {
        public ErrorResponse()
        {
            Command = ERROR;
        }

        [JsonPropertyName("error")]
        public string Error { get; set; } = "";
    }

    public class DisconnectResponse : Response
    {
        public DisconnectResponse()
        {
            Command = DISCONNECT;
        }
    }
}

