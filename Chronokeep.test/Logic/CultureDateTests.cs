using Chronokeep.Helpers;
using Chronokeep.Interfaces.Timing;
using Chronokeep.Objects;
using Chronokeep.Test.Database;
using Chronokeep.Test.Timing;
using Chronokeep.Timing.Interfaces;

namespace Chronokeep.Test.Logic
{
    /// <summary>
    /// Event dates, ages and birthdates have to read the same whatever the computer's language,
    /// since a database can move between computers and the app runs worldwide.
    /// </summary>
    public class EventDateCultureTests : DatabaseTestBase
    {
        public static TheoryData<string> Cultures => ["en-US", "en-GB", "de-DE", "fi-FI", "fr-FR"];

        [Theory]
        [MemberData(nameof(Cultures))]
        public void New_events_store_their_date_the_same_way_everywhere(string culture)
        {
            Event e = CultureScope.In(culture, () => new Event("Spring Fling", new DateTime(2026, 6, 15).Ticks, "2026"));

            Assert.Equal("2026/06/15", e.Date);
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void Saved_events_read_back_with_the_same_date(string culture)
        {
            CultureScope.In(culture, () =>
            {
                Event e = new("Spring Fling", new DateTime(2026, 6, 15).Ticks, "2026");
                e.Identifier = Db.AddEvent(e);

                Event loaded = Db.GetEvent(e.Identifier)!;

                Assert.Equal("2026/06/15", loaded.Date);
                Assert.Equal(new DateTime(2026, 6, 15), EventDates.Parse(loaded.Date));
                Assert.Equal("2026", loaded.Year);
            });
        }

        [Fact]
        public void A_database_written_on_a_us_computer_opens_on_a_german_one()
        {
            Event e = CultureScope.In("en-US", () =>
            {
                Event created = new("Spring Fling", new DateTime(2026, 6, 5).Ticks, "2026");
                created.Identifier = Db.AddEvent(created);
                return created;
            });

            Event loaded = CultureScope.In("de-DE", () => Db.GetEvent(e.Identifier)!);

            Assert.Equal(new DateTime(2026, 6, 5), EventDates.Parse(loaded.Date));
            Assert.Equal("June 5, 2026", CultureScope.In("de-DE", () => loaded.LongDate));
        }

        [Theory]
        [InlineData("6/15/2026")]
        [InlineData("06/15/2026")]
        [InlineData("2026-06-15")]
        [InlineData("2026/06/15")]
        [InlineData(" 6/15/2026 ")]
        public void Any_accepted_form_is_stored_as_year_month_day(string given)
        {
            Event e = new() { Date = given };

            Assert.Equal("2026/06/15", e.Date);
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void Events_sort_newest_first_in_any_culture(string culture)
        {
            List<Event> events =
            [
                new() { Name = "Old", Date = "1/5/2025" },
                new() { Name = "New", Date = "12/1/2026" },
                new() { Name = "Mid", Date = "6/15/2026" },
            ];

            CultureScope.In(culture, () => events.Sort());

            Assert.Equal(["New", "Mid", "Old"], events.Select(e => e.Name));
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void Ages_count_birthdays_before_the_event_in_any_culture(string culture)
        {
            Event e = AddEvent(date: "6/15/2026", makeCurrent: true);
            Participant p = AddParticipant(e.Identifier, AddDistance(e.Identifier), "1", birthday: "1990/06/16");

            (string age, int getAge) = CultureScope.In(culture, () => (p.Age("6/15/2026"), p.GetAge("6/15/2026")));

            Assert.Equal(("35", 35), (age, getAge));
        }

        [Fact]
        public void A_birthdate_after_the_event_has_no_age()
        {
            Event e = AddEvent(date: "6/15/2026", makeCurrent: true);
            Participant p = AddParticipant(e.Identifier, AddDistance(e.Identifier), "1", birthday: "2030/01/01");

            Assert.Equal(("", -1), (p.Age("6/15/2026"), p.GetAge("6/15/2026")));
        }
    }

    /// <summary>Two-digit birth years go in the century that puts the birthdate in the past.</summary>
    public class TwoDigitBirthYearTests
    {
        [Theory]
        [InlineData("1/2/45", "1945/01/02")]
        [InlineData("1/2/90", "1990/01/02")]
        [InlineData("12/31/49", "1949/12/31")]
        public void Two_digit_years_that_would_be_in_the_future_are_last_century(string given, string expected)
        {
            Assert.Equal(expected, CultureScope.In("en-US", () => Birthdates.Normalize(given)));
        }

        [Fact]
        public void A_two_digit_year_already_in_the_past_stays_this_century()
        {
            Assert.Equal("2005/01/02", CultureScope.In("en-US", () => Birthdates.Normalize("1/2/05")));
        }

        [Fact]
        public void A_four_digit_future_year_is_kept_as_given()
        {
            Assert.Equal("2045/01/02", Birthdates.Normalize("2045/01/02"));
        }

        [Fact]
        public void Day_month_order_with_a_two_digit_year_follows_the_computer()
        {
            Assert.Equal("1945/02/01", CultureScope.In("en-GB", () => Birthdates.Normalize("1/2/45")));
        }
    }

    /// <summary>The Ultra reports its battery as V=24.8 in every country; reading it can't depend on the computer.</summary>
    public class UltraVoltageCultureTests : ReaderTestBase
    {
        [Theory]
        [InlineData("de-DE", "V=22.1\n", MessageType.VOLTAGELOW)]
        [InlineData("de-DE", "V=24.8\n", MessageType.VOLTAGENORMAL)]
        [InlineData("fr-FR", "V=22.1\n", MessageType.VOLTAGELOW)]
        [InlineData("fi-FI", "V=24.8\n", MessageType.VOLTAGENORMAL)]
        public void Voltage_reads_the_same_in_any_culture(string culture, string message, MessageType expected)
        {
            RfidUltraInterface ultra = new(Db, Location, Window);

            Dictionary<MessageType, List<string>> output = CultureScope.In(culture, () => ultra.ParseMessages(message, Sock));

            Assert.True(output.ContainsKey(expected), string.Join(", ", output.Keys));
        }
    }
}
