using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    class Participants
    {
        internal static Participant? AddParticipant(Participant person, SQLiteConnection connection)
        {
            person.FormatData();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "INSERT INTO participants " +
                "(" +
                "participant_first, " +
                "participant_last, " +
                "participant_street, " +
                "participant_city, " +
                "participant_state, " +
                "participant_zip, " +
                "participant_birthday, " +
                "participant_email, " +
                "participant_phone, " +
                "participant_mobile, " +
                "participant_parent, " +
                "participant_country, " +
                "participant_street2, " +
                "participant_gender, " +
                "emergencycontact_name, " +
                "emergencycontact_phone" +
                ")" +
                " VALUES " +
                "(" +
                "@first," +
                "@last," +
                "@street," +
                "@city," +
                "@state," +
                "@zip," +
                "@birthdate," +
                "@email," +
                "@phone," +
                "@mobile," +
                "@parent," +
                "@country," +
                "@street2," +
                "@gender," +
                "@ecname," +
                "@ecphone" +
                "); SELECT participant_id FROM participants WHERE participant_first=@first " +
                "AND participant_last=@last AND participant_street=@street AND participant_city=@city AND " +
                "participant_state=@state AND participant_zip=@zip;";
            command.Parameters.AddRange([
                new("@first", person.FirstName),
                new("@last", person.LastName),
                new("@street", person.Street),
                new("@city", person.City),
                new("@state", person.State),
                new("@zip", person.Zip),
                new("@birthdate", person.Birthdate),
                new("@email", person.Email),
                new("@phone", person.Phone),
                new("@mobile", person.Mobile),
                new("@parent", person.Parent),
                new("@country", person.Country),
                new("@street2", person.Street2),
                new("@ecname", person.EcName),
                new("@ecphone", person.EcPhone),
                new("@gender", person.Gender) ]);
            command.ExecuteNonQuery();
            person.Identifier = GetParticipantID(person, connection);
            command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "INSERT INTO eventspecific " +
                "(" +
                "participant_id, " +
                "event_id, " +
                "distance_id, " +
                "eventspecific_bib, " +
                "eventspecific_checkedin, " +
                "eventspecific_comments, " +
                "eventspecific_owes, " +
                "eventspecific_other, " +
                "eventspecific_age_group_name, " +
                "eventspecific_age_group_id, " +
                "eventspecific_anonymous, " +
                "eventspecific_sms_enabled, " +
                "eventspecific_apparel, " +
                "eventspecific_division, " +
                "eventspecific_version, " +
                "eventspecific_uploaded_version" +
                ") " +
                "VALUES " +
                "(" +
                "@participant," +
                "@event," +
                "@distance," +
                "@bib," +
                "@checkedin," +
                "@comments," +
                "@owes," +
                "@other," +
                "@ageGroupName," +
                "@ageGroupId," +
                "@anon," +
                "@sms," +
                "@apparel," +
                "@division, " +
                "@version, " +
                "@uploaded" +
                ")";
            command.Parameters.AddRange([
                new("@participant", person.Identifier),
                new("@event", person.EventSpecific.EventIdentifier),
                new("@distance", person.EventSpecific.DistanceIdentifier),
                new("@bib", person.EventSpecific.Bib),
                new("@checkedin", person.EventSpecific.CheckedIn),
                new("@comments", person.EventSpecific.Comments),
                new("@owes", person.EventSpecific.Owes),
                new("@other", person.EventSpecific.Other),
                new("@ageGroupName", person.EventSpecific.AgeGroupName),
                new("@ageGroupId", person.EventSpecific.AgeGroupId),
                new("@anon", person.EventSpecific.Anonymous ? 1 : 0),
                new("@sms", person.EventSpecific.SMSEnabled ? 1 : 0),
                new("@apparel", person.EventSpecific.Apparel),
                new("@division", person.EventSpecific.Division),
                new("@version", person.EventSpecific.Version),
                new("@uploaded", person.EventSpecific.UploadedVersion)
            ]);
            command.ExecuteNonQuery();
            long outVal = connection.LastInsertRowId;
            person.EventSpecific.Identifier = (int)outVal;
            return person;
        }

        internal static void RemoveParticipant(int identifier, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM eventspecific WHERE participant_id=@0; DELETE FROM participants WHERE participant_id=@0";
            command.Parameters.AddRange([
                    new("@0", identifier) ]);
            command.ExecuteNonQuery();
        }

        internal static void RemoveEventSpecific(int identifier, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM eventspecific WHERE participant_id=@0;";
            command.Parameters.AddRange([
                    new("@0", identifier) ]);
            command.ExecuteNonQuery();
        }

        internal static void UpdateParticipant(Participant person, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "UPDATE participants SET " +
                "participant_first=@first, " +
                "participant_last=@last, " +
                "participant_street=@street, " +
                "participant_city=@city, " +
                "participant_state=@state, " +
                "participant_zip=@zip, " +
                "participant_birthday=@birthdate, " +
                "emergencycontact_name=@ecname, " +
                "emergencycontact_phone=@ecphone, " +
                "participant_email=@email, " +
                "participant_phone=@phone, " +
                "participant_mobile=@mobile, " +
                "participant_parent=@parent, " +
                "participant_country=@country, " +
                "participant_street2=@street2, " +
                "participant_gender=@gender " +
                "WHERE participant_id=@participantid";
            command.Parameters.AddRange([
                    new("@first", person.FirstName),
                    new("@last", person.LastName),
                    new("@street", person.Street),
                    new("@city", person.City),
                    new("@state", person.State),
                    new("@zip", person.Zip),
                    new("@birthdate", person.Birthdate),
                    new("@ecname", person.EcName),
                    new("@ecphone", person.EcPhone),
                    new("@email", person.Email),
                    new("@participantid", person.Identifier),
                    new("@phone", person.Phone),
                    new("@mobile", person.Mobile),
                    new("@parent", person.Parent),
                    new("@country", person.Country),
                    new("@street2", person.Street2),
                    new("@gender", person.Gender) ]);
            command.ExecuteNonQuery();
            command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "UPDATE eventspecific SET " +
                "distance_id=@distanceId, " +
                "eventspecific_bib=@bib, " +
                "eventspecific_checkedin=@checkedin, " +
                "eventspecific_owes=@owes, " +
                "eventspecific_other=@other, " +
                "eventspecific_comments=@comments, " +
                "eventspecific_status=@status, " +
                "eventspecific_age_group_name=@ageGroupName, " +
                "eventspecific_age_group_id=@ageGroupId, " +
                "eventspecific_anonymous=@anon, " +
                "eventspecific_sms_enabled=@sms, " +
                "eventspecific_apparel=@apparel, " +
                "eventspecific_division=@division, " +
                "eventspecific_version=@version, " +
                "eventspecific_uploaded_version=@uploaded " +
                "WHERE eventspecific_id=@eventspecid";
            command.Parameters.AddRange([
                    new("@distanceId", person.EventSpecific.DistanceIdentifier),
                    new("@bib", person.EventSpecific.Bib),
                    new("@checkedin", person.EventSpecific.CheckedIn),
                    new("@eventspecid", person.EventSpecific.Identifier),
                    new("@owes", person.EventSpecific.Owes),
                    new("@other", person.EventSpecific.Other),
                    new("@comments", person.EventSpecific.Comments),
                    new("@status", person.EventSpecific.Status),
                    new("@ageGroupName", person.EventSpecific.AgeGroupName),
                    new("@ageGroupId", person.EventSpecific.AgeGroupId),
                    new("@anon", person.EventSpecific.Anonymous ? 1 : 0),
                    new("@sms", person.EventSpecific.SMSEnabled ? 1 : 0),
                    new("@apparel", person.EventSpecific.Apparel),
                    new("@division", person.EventSpecific.Division),
                    new("@version", person.EventSpecific.Version),
                    new("@uploaded", person.EventSpecific.UploadedVersion)
                ]);
            command.ExecuteNonQuery();
        }

        internal static void V44UpdateParticipant(Participant person, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "UPDATE participants SET " +
                "participant_first=@first, " +
                "participant_last=@last, " +
                "participant_street=@street, " +
                "participant_city=@city, " +
                "participant_state=@state, " +
                "participant_zip=@zip, " +
                "participant_birthday=@birthdate, " +
                "emergencycontact_name=@ecname, " +
                "emergencycontact_phone=@ecphone, " +
                "participant_email=@email, " +
                "participant_mobile=@mobile, " +
                "participant_parent=@parent, " +
                "participant_country=@country, " +
                "participant_street2=@street2, " +
                "participant_gender=@gender " +
                "WHERE participant_id=@participantid";
            command.Parameters.AddRange([
                    new("@first", person.FirstName),
                    new("@last", person.LastName),
                    new("@street", person.Street),
                    new("@city", person.City),
                    new("@state", person.State),
                    new("@zip", person.Zip),
                    new("@birthdate", person.Birthdate),
                    new("@ecname", person.EcName),
                    new("@ecphone", person.EcPhone),
                    new("@email", person.Email),
                    new("@participantid", person.Identifier),
                    new("@mobile", person.Mobile),
                    new("@parent", person.Parent),
                    new("@country", person.Country),
                    new("@street2", person.Street2),
                    new("@gender", person.Gender) ]);
            command.ExecuteNonQuery();
            command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "UPDATE eventspecific SET " +
                "distance_id=@distanceId, " +
                "eventspecific_bib=@bib, " +
                "eventspecific_checkedin=@checkedin, " +
                "eventspecific_owes=@owes, " +
                "eventspecific_other=@other, " +
                "eventspecific_comments=@comments, " +
                "eventspecific_status=@status, " +
                "eventspecific_age_group_name=@ageGroupName, " +
                "eventspecific_age_group_id=@ageGroupId " +
                "WHERE eventspecific_id=@eventspecid";
            command.Parameters.AddRange([
                    new("@distanceId", person.EventSpecific.DistanceIdentifier),
                    new("@bib", person.EventSpecific.Bib),
                    new("@checkedin", person.EventSpecific.CheckedIn),
                    new("@eventspecid", person.EventSpecific.Identifier),
                    new("@owes", person.EventSpecific.Owes),
                    new("@other", person.EventSpecific.Other),
                    new("@comments", person.EventSpecific.Comments),
                    new("@status", person.EventSpecific.Status),
                    new("@ageGroupName", person.EventSpecific.AgeGroupName),
                    new("@ageGroupId", person.EventSpecific.AgeGroupId),
                ]);
            command.ExecuteNonQuery();
        }

        internal static List<Participant> GetParticipants(SQLiteConnection connection)
        {
            Log.D("SQLite.Participants", "Getting all participants for all events.");
            return GetParticipantsWorker("SELECT MAX(s.eventspecific_id) AS max_id, * FROM participants p " +
                "JOIN eventspecific s ON p.participant_id = s.participant_id " +
                "LEFT JOIN bib_chip_assoc c ON c.bib = s.eventspecific_bib AND c.event_id=s.event_id " +
                "JOIN distances d ON s.distance_id = d.distance_id " +
                "GROUP BY s.eventspecific_id " +
                "ORDER BY p.participant_last ASC, p.participant_first ASC;",
                -1, -1, connection);
        }

        internal static List<Participant> GetParticipants(int eventId, SQLiteConnection connection)
        {
            Log.D("SQLite.Participants", "Getting all participants for event with id of " + eventId);
            return GetParticipantsWorker("SELECT MAX(s.eventspecific_id) AS max_id, * FROM participants p " +
                "JOIN eventspecific s ON p.participant_id = s.participant_id " +
                "JOIN distances d ON s.distance_id = d.distance_id " +
                "LEFT JOIN bib_chip_assoc c ON c.bib = s.eventspecific_bib AND c.event_id=s.event_id " +
                "WHERE s.event_id=@event " +
                "GROUP BY s.eventspecific_id " +
                "ORDER BY p.participant_last ASC, p.participant_first ASC;",
                eventId, -1, connection);
        }

        internal static List<Participant> GetParticipants(int eventId, int distanceId, SQLiteConnection connection)
        {
            Log.D("SQLite.Participants", "Getting all participants for event with id of " + eventId);
            return GetParticipantsWorker("SELECT MAX(s.eventspecific_id) AS max_id, * FROM participants p " +
                "JOIN eventspecific s ON p.participant_id = s.participant_id " +
                "JOIN distances d ON s.distance_id = d.distance_id " +
                "LEFT JOIN bib_chip_assoc c ON c.bib = s.eventspecific_bib AND c.event_id=s.event_id " +
                "WHERE s.event_id=@event AND d.distance_id=@distance " +
                "GROUP BY s.eventspecific_id " +
                "ORDER BY p.participant_last ASC, p.participant_first ASC;",
                eventId, distanceId, connection);
        }

        internal static List<Participant> GetParticipantsWorker(string query, int eventId, int distanceId, SQLiteConnection connection)
        {
            List<Participant> output = [];
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = query;
            if (eventId != -1)
            {
                command.Parameters.Add(new("@event", eventId));
            }
            if (distanceId != -1)
            {
                command.Parameters.Add(new("@distance", distanceId));
            }
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new Participant(
                    Convert.ToInt32(reader["participant_id"]),
                    reader["participant_first"].ToString()!,
                    reader["participant_last"].ToString()!,
                    reader["participant_street"].ToString()!,
                    reader["participant_city"].ToString()!,
                    reader["participant_state"].ToString()!,
                    reader["participant_zip"].ToString()!,
                    reader["participant_birthday"].ToString()!,
                    new EventSpecific(
                        Convert.ToInt32(reader["eventspecific_id"]),
                        Convert.ToInt32(reader["event_id"]),
                        Convert.ToInt32(reader["distance_id"]),
                        reader["distance_name"].ToString()!,
                        reader["eventspecific_bib"].ToString()!,
                        Convert.ToInt32(reader["eventspecific_checkedin"]),
                        reader["eventspecific_comments"].ToString()!,
                        reader["eventspecific_owes"].ToString()!,
                        reader["eventspecific_other"].ToString()!,
                        Convert.ToInt32(reader["eventspecific_status"]),
                        reader["eventspecific_age_group_name"].ToString()!,
                        Convert.ToInt32(reader["eventspecific_age_group_id"]),
                        Convert.ToInt16(reader["eventspecific_anonymous"]) != 0,
                        Convert.ToInt16(reader["eventspecific_sms_enabled"]) != 0,
                        reader["eventspecific_apparel"] != DBNull.Value ? reader["eventspecific_apparel"].ToString()! : "",
                        reader["eventspecific_division"] != DBNull.Value ? reader["eventspecific_division"].ToString()! : "",
                        Convert.ToInt32(reader["eventspecific_version"]),
                        Convert.ToInt32(reader["eventspecific_uploaded_version"])
                        ),
                    reader["participant_email"].ToString()!,
                    reader["participant_phone"].ToString()!,
                    reader["participant_mobile"].ToString()!,
                    reader["participant_parent"].ToString()!,
                    reader["participant_country"].ToString()!,
                    reader["participant_street2"].ToString()!,
                    reader["participant_gender"].ToString()!,
                    reader["emergencycontact_name"].ToString()!,
                    reader["emergencycontact_phone"].ToString()!
                    ));
            }
            reader.Close();
            return output;
        }

        internal static Participant? GetParticipantWorker(SQLiteDataReader reader)
        {
            if (reader.Read())
            {
                return new Participant(
                    Convert.ToInt32(reader["participant_id"]),
                    reader["participant_first"].ToString()!,
                    reader["participant_last"].ToString()!,
                    reader["participant_street"].ToString()!,
                    reader["participant_city"].ToString()!,
                    reader["participant_state"].ToString()!,
                    reader["participant_zip"].ToString()!,
                    reader["participant_birthday"].ToString()!,
                    new EventSpecific(
                        Convert.ToInt32(reader["eventspecific_id"]),
                        Convert.ToInt32(reader["event_id"]),
                        Convert.ToInt32(reader["distance_id"]),
                        reader["distance_name"].ToString()!,
                        reader["eventspecific_bib"].ToString()!,
                        Convert.ToInt32(reader["eventspecific_checkedin"]),
                        reader["eventspecific_comments"].ToString()!,
                        reader["eventspecific_owes"].ToString()!,
                        reader["eventspecific_other"].ToString()!,
                        Convert.ToInt32(reader["eventspecific_status"]),
                        reader["eventspecific_age_group_name"].ToString()!,
                        Convert.ToInt32(reader["eventspecific_age_group_id"]),
                        Convert.ToInt16(reader["eventspecific_anonymous"]) != 0,
                        Convert.ToInt16(reader["eventspecific_sms_enabled"]) != 0,
                        reader["eventspecific_apparel"] != DBNull.Value ? reader["eventspecific_apparel"].ToString()! : "",
                        reader["eventspecific_division"] != DBNull.Value ? reader["eventspecific_division"].ToString()! : "",
                        Convert.ToInt32(reader["eventspecific_version"]),
                        Convert.ToInt32(reader["eventspecific_uploaded_version"])
                        ),
                    reader["participant_email"].ToString()!,
                    reader["participant_phone"].ToString()!,
                    reader["participant_mobile"].ToString()!,
                    reader["participant_parent"].ToString()!,
                    reader["participant_country"].ToString()!,
                    reader["participant_street2"].ToString()!,
                    reader["participant_gender"].ToString()!,
                    reader["emergencycontact_name"].ToString()!,
                    reader["emergencycontact_phone"].ToString()!
                    );
            }
            return null;
        }

        internal static Participant? GetParticipantEventSpecific(int eventIdentifier, int eventSpecificId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM participants AS p " +
                "JOIN eventspecific AS s ON p.participant_id=s.participant_id " +
                "JOIN distances AS d ON s.distance_id=d.distance_id " +
                "LEFT JOIN bib_chip_assoc c ON c.bib = s.eventspecific_bib " +
                "WHERE s.event_id=@eventid " +
                "AND s.eventspecific_id=@eventSpecId";
            command.Parameters.Add(new("@eventid", eventIdentifier));
            command.Parameters.Add(new("@eventSpecId", eventSpecificId));
            SQLiteDataReader reader = command.ExecuteReader();
            Participant? output = GetParticipantWorker(reader);
            reader.Close();
            return output;
        }

        internal static Participant? GetParticipantBib(int eventIdentifier, string bib, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM participants AS p " +
                "JOIN eventspecific AS s ON p.participant_id=s.participant_id " +
                "JOIN distances AS d ON s.distance_id=d.distance_id " +
                "LEFT JOIN bib_chip_assoc c ON c.bib = s.eventspecific_bib " +
                "WHERE s.event_id=@eventid " +
                "AND s.eventspecific_bib=@bib";
            command.Parameters.Add(new("@eventid", eventIdentifier));
            command.Parameters.Add(new("@bib", bib));
            SQLiteDataReader reader = command.ExecuteReader();
            Participant? output = GetParticipantWorker(reader);
            reader.Close();
            return output;
        }

        internal static Participant? GetParticipantChip(int eventIdentifier, string chip, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM participants AS p, eventspecific AS s, distances AS d, " +
                    "bib_chip_assoc as b WHERE p.participant_id=s.participant_id AND s.event_id=@eventid " +
                    "AND d.distance_id=s.distance_id AND " +
                    "s.eventspecific_bib=b.bib AND b.chip=@chip AND b.event_id=s.event_id;";
            command.Parameters.AddRange([
                new("@eventid", eventIdentifier),
                new("@chip", chip)
                ]);
            SQLiteDataReader reader = command.ExecuteReader();
            Participant? output = GetParticipantWorker(reader);
            reader.Close();
            return output;
        }

        internal static Participant? GetParticipant(int eventId, int identifier, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM participants AS p " +
                "JOIN eventspecific AS s ON p.participant_id=s.participant_id " +
                "JOIN distances AS d ON s.distance_id=d.distance_id " +
                "LEFT JOIN bib_chip_assoc c ON c.bib = s.eventspecific_bib " +
                "WHERE s.event_id=@eventid AND p.participant_id=@partId";
            command.Parameters.Add(new("@eventid", eventId));
            command.Parameters.Add(new("@partId", identifier));
            SQLiteDataReader reader = command.ExecuteReader();
            Participant? output = GetParticipantWorker(reader);
            reader.Close();
            return output;
        }

        internal static Participant? GetParticipant(int eventId, Participant unknown, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM participants AS p " +
                    "JOIN eventspecific AS s ON p.participant_id=s.participant_id " +
                    "JOIN distances AS d ON s.distance_id=d.distance_id " +
                    "LEFT JOIN bib_chip_assoc c ON c.bib = s.eventspecific_bib " +
                    "WHERE s.event_id=@eventid " +
                    "AND p.participant_first=@first AND p.participant_last=@last AND p.participant_street=@street " +
                    "AND p.participant_city=@city AND p.participant_state=@state AND p.participant_zip=@zip " +
                    "AND p.participant_birthday=@birthday";
            command.Parameters.AddRange([
                    new("@eventid", eventId),
                    new("@first", unknown.FirstName),
                    new("@last", unknown.LastName),
                    new("@street", unknown.Street),
                    new("@city", unknown.City),
                    new("@state", unknown.State),
                    new("@zip", unknown.Zip),
                    new("@birthday", unknown.Birthdate)
                ]);
            SQLiteDataReader reader = command.ExecuteReader();
            Participant? output = GetParticipantWorker(reader);
            reader.Close();
            return output;
        }

        internal static int GetParticipantID(Participant person, SQLiteConnection connection)
        {
            int output = -1;
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT participant_id FROM participants WHERE participant_first=@first AND " +
                "participant_last=@last AND participant_street=@street AND " +
                "participant_zip=@zip AND participant_birthday=@birthday";
            command.Parameters.AddRange(
            [
                new("@first", person.FirstName),
                new("@last", person.LastName),
                new("@street", person.Street),
                new("@zip", person.Zip),
                new("@birthday", person.Birthdate)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                try
                {
                    output = Convert.ToInt32(reader["participant_id"]);
                }
                catch
                {
                    output = -1;
                }
            }
            reader.Close();
            return output;
        }

        internal static List<string> GetDivisions(int eventIdentifier, SQLiteConnection connection)
        {
            List<string> output = [];
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT eventspecific_division FROM eventspecific AS e WHERE e.event_id=@eventID;";
            command.Parameters.AddRange([
                new SQLiteParameter("@eventID", eventIdentifier)
                ]);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(reader["eventspecific_division"].ToString()!);
            }
            reader.Close();
            return output;
        }

    }
}
