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
