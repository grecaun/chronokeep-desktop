using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using System.Data.SQLite;

// The SQLite layer keeps static state (Results' cached event/locations/segments),
// so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Chronokeep.Test.Database
{
    /// <summary>
    /// Gives every test a freshly initialized SQLite database in a temp file,
    /// plus helpers for building the rows most tests depend on.
    /// </summary>
    public abstract class DatabaseTestBase : IDisposable
    {
        private readonly string directory;

        internal string DbPath { get; }
        internal SqLiteInterface Db { get; }

        protected DatabaseTestBase()
        {
            directory = Path.Combine(Path.GetTempPath(), "chronokeep-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            DbPath = Path.Combine(directory, "test.sqlite");
            Globals.ErrorLogPath = Path.Combine(directory, "error_log.txt");
            Db = new SqLiteInterface(DbPath);
            Db.Initialize();
        }

        public virtual void Dispose()
        {
            SQLiteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
                // A connection the production code didn't dispose may still hold the file; it's in temp, so leave it.
            }
            catch (UnauthorizedAccessException)
            {
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>Runs a raw scalar query against the test database.</summary>
        internal object? Scalar(string sql)
        {
            using SQLiteConnection connection = new($"Data Source={DbPath};Version=3");
            connection.Open();
            using SQLiteCommand command = new(sql, connection);
            return command.ExecuteScalar();
        }

        internal long Count(string table, string where = "1=1")
        {
            return Convert.ToInt64(Scalar($"SELECT COUNT(*) FROM {table} WHERE {where};"));
        }

        internal static string DateString(int year = 2026, int month = 6, int day = 15)
            => new DateTime(year, month, day).ToShortDateString();

        internal Event AddEvent(string name = "Test Race", string? date = null, bool makeCurrent = false)
        {
            Event e = new()
            {
                Name = name,
                Date = date ?? DateString(),
                YearCode = "2026",
            };
            e.Identifier = Db.AddEvent(e);
            if (makeCurrent)
            {
                Db.SetCurrentEvent(e.Identifier);
            }
            return e;
        }

        internal Distance AddDistance(int eventId, string name = "5K")
        {
            Distance d = new(name, eventId)
            {
                DistanceValue = 3.1,
                Certification = "CERT-1",
            };
            d.Identifier = Db.AddDistance(d);
            return d;
        }

        internal static Participant NewParticipant(
            int eventId,
            Distance distance,
            string bib,
            string first = "Jane",
            string last = "Runner",
            string birthday = "01/02/1990",
            string gender = "F",
            string division = "")
        {
            return new Participant(
                first, last, "123 Main St", "Springfield", "OR", "97477", birthday,
                new EventSpecific(eventId, distance.Identifier, distance.Name, bib, 0,
                    "comment", "owes", "other", false, false, "M", division),
                "jane@example.com", "555-0100", "555-0101", "Parent", "USA", "Apt 1",
                gender, "EC Name", "555-0199");
        }

        internal Participant AddParticipant(
            int eventId,
            Distance distance,
            string bib,
            string first = "Jane",
            string last = "Runner",
            string birthday = "01/02/1990",
            string gender = "F",
            string division = "")
        {
            Participant? p = Db.AddParticipant(NewParticipant(eventId, distance, bib, first, last, birthday, gender, division));
            Assert.NotNull(p);
            return p;
        }

        internal static ChipRead NewRead(int eventId, string chip, int seconds = 0, int locationId = Constants.Timing.LOCATION_FINISH)
            => new(eventId, locationId, chip, new DateTime(2026, 6, 15, 8, 0, 0).AddSeconds(seconds));
    }
}
