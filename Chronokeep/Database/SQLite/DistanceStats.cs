using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class DistanceStats
    {
        internal static List<DistanceStat> GetDistanceStats(int eventId, bool condense, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT d.distance_id AS id, d.distance_name AS name, e.eventspecific_status AS status, " +
                "d.distance_linked_id AS linked_id, l.distance_name AS linked_name, " +
                "COUNT(e.eventspecific_status) AS count FROM distances d JOIN distances l ON " +
                "d.distance_linked_id=l.distance_id JOIN eventspecific e ON d.distance_id=e.distance_id " +
                "WHERE e.event_id=@event " +
                "GROUP BY d.distance_name, e.eventspecific_status;";
            command.Parameters.Add(new SQLiteParameter("@event", eventId));
            SQLiteDataReader reader = command.ExecuteReader();
            DistanceStat allstats = new()
            {
                DistanceName = "All",
                DistanceId = -1,
                Active = 0,
                Dnf = 0,
                Dns = 0,
                Finished = 0
            };
            Dictionary<int, DistanceStat> statsDictionary = [];
            while (reader.Read())
            {
                int distanceId = Convert.ToInt32(reader["id"].ToString());
                string distanceName = reader["name"].ToString()!;
                if (condense && int.TryParse(reader["linked_id"].ToString(), out int linked))
                {
                    distanceId = linked;
                    distanceName = reader["linked_name"].ToString()!;
                }
                statsDictionary.TryAdd(distanceId, new DistanceStat
                {
                    DistanceName = distanceName,
                    DistanceId = distanceId
                });
                if (!int.TryParse(reader["status"].ToString(), out int status)) continue;
                switch (status)
                {
                    case Constants.Timing.EVENTSPECIFIC_DNS:
                    case Constants.Timing.EVENTSPECIFIC_UNKNOWN:
                        statsDictionary[distanceId].Dns = Convert.ToInt32(reader["count"]);
                        allstats.Dns += statsDictionary[distanceId].Dns;
                        break;
                    case Constants.Timing.EVENTSPECIFIC_FINISHED:
                        statsDictionary[distanceId].Finished = Convert.ToInt32(reader["count"]);
                        allstats.Finished += statsDictionary[distanceId].Finished;
                        break;
                    case Constants.Timing.EVENTSPECIFIC_STARTED:
                        statsDictionary[distanceId].Active = Convert.ToInt32(reader["count"]);
                        allstats.Active += statsDictionary[distanceId].Active;
                        break;
                    case Constants.Timing.EVENTSPECIFIC_DNF:
                        statsDictionary[distanceId].Dnf = Convert.ToInt32(reader["count"]);
                        allstats.Dnf += statsDictionary[distanceId].Dnf;
                        break;
                }
            }
            reader.Close();
            List<DistanceStat> output =
            [
                .. statsDictionary.Values
            ];
            output.Sort((x1, x2) => x1.Active != x2.Active ? x2.Active.CompareTo(x1.Active) : string.Compare(x1.DistanceName, x2.DistanceName, StringComparison.Ordinal));
            if (output.Count > 1)
            {
                output.Insert(0, allstats);
            }
            return output;
        }
    }
}
