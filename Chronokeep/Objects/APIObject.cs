using Chronokeep.Network.Remote;
using Chronokeep.Objects.ChronokeepRemote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Chronokeep.Objects
{
    public class ApiObject(int id, string type, string url, string nickname, string authToken, string webUrl)
    {
        public ApiObject() : this(
            0,
            Constants.ApiConstants.CHRONOKEEP_RESULTS,
            Constants.ApiConstants.API_URL[Constants.ApiConstants.CHRONOKEEP_RESULTS],
            "",
            "",
            "") { }

        public int Identifier { get; set; } = id;
        public string Type { get; set; } = type;
        public string Url { get; set; } = url;
        public string Nickname { get; set; } = nickname;
        public string AuthToken { get; set; } = authToken;
        public string WebUrl { get; set; } = webUrl;

        public async Task<List<RemoteReader>> GetReaders()
        {
            if (Type != Constants.ApiConstants.CHRONOKEEP_REMOTE && Type != Constants.ApiConstants.CHRONOKEEP_REMOTE_SELF)
            {
                throw new Exception("not a valid reader type");
            }
            GetReadersResponse response = await RemoteHandlers.GetReaders(this);
            return response.Readers;
        }

        public async Task<(List<ChipRead>, RemoteNotification)> GetReads(RemoteReader reader, DateTime start, DateTime end)
        {
            if (Type != Constants.ApiConstants.CHRONOKEEP_REMOTE && Type != Constants.ApiConstants.CHRONOKEEP_REMOTE_SELF)
            {
                throw new Exception("not a valid reader type");
            }
            GetReadsResponse result = await RemoteHandlers.GetReads(
                    this,
                    reader.Name,
                    Constants.Timing.UnixDateToEpoch(start.ToUniversalTime()),
                    Constants.Timing.UnixDateToEpoch(end.ToUniversalTime())
                );
            List<ChipRead> output = [];
            output.AddRange(result.Reads.Select(read => read.ConvertToChipRead(reader.EventId, reader.LocationId)));
            return (output, result.Notification);
        }

        public async Task<long> DeleteReads(RemoteReader reader, DateTime start, DateTime end)
        {
            if (Type != Constants.ApiConstants.CHRONOKEEP_REMOTE && Type != Constants.ApiConstants.CHRONOKEEP_REMOTE_SELF)
            {
                throw new Exception("not a valid reader type");
            }
            DeleteReadsResponse result = await RemoteHandlers.DeleteReads(
                    this,
                    reader.Name,
                    Constants.Timing.UnixDateToEpoch(start.ToUniversalTime()),
                    Constants.Timing.UnixDateToEpoch(end.ToUniversalTime())
                );
            return result.Count;
        }
    }
}
