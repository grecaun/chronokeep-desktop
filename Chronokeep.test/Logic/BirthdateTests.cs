using System.Globalization;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;

namespace Chronokeep.Test.Logic
{
    /// <summary>
    /// Birthdates are stored and compared as yyyy/MM/dd, the year-first form Chronokeep Results accepts whatever form they arrive in.
    /// </summary>
    public class BirthdateTests : IDisposable
    {
        private readonly CultureInfo originalCulture = CultureInfo.CurrentCulture;

        public BirthdateTests() => CultureInfo.CurrentCulture = new CultureInfo("en-US");

        public void Dispose()
        {
            CultureInfo.CurrentCulture = originalCulture;
            GC.SuppressFinalize(this);
        }

        [Theory]
        [InlineData("1990-01-02", "1990/01/02")]
        [InlineData("1990-1-2", "1990/01/02")]
        [InlineData("1990/01/02", "1990/01/02")]
        [InlineData("1990/1/2", "1990/01/02")]
        [InlineData("01/02/1990", "1990/01/02")]
        [InlineData("1/2/1990", "1990/01/02")]
        [InlineData("1990-01-02 00:00:00", "1990/01/02")]
        [InlineData("1990-01-02T00:00:00", "1990/01/02")]
        [InlineData("1990/01/02 08:00:00 -07:00", "1990/01/02")]
        [InlineData("January 2, 1990", "1990/01/02")]
        [InlineData("  1990-01-02  ", "1990/01/02")]
        public void Normalize_writes_every_accepted_form_as_year_month_day(string input, string expected)
        {
            Assert.Equal(expected, Birthdates.Normalize(input));
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData("unknown", "unknown")]
        [InlineData(" n/a ", "n/a")]
        public void Normalize_keeps_empty_and_non_dates_without_inventing_a_date(string? input, string expected)
        {
            Assert.Equal(expected, Birthdates.Normalize(input));
        }

        [Fact]
        public void Slashed_day_month_order_follows_the_computer_culture_but_year_first_never_does()
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-GB");

            Assert.Equal("1990/02/01", Birthdates.Normalize("01/02/1990"));
            Assert.Equal("1990/01/02", Birthdates.Normalize("1990/01/02"));
            Assert.Equal("1990/01/02", Birthdates.Normalize("1990-01-02"));
        }

        [Fact]
        public void January_first_placeholder_uses_the_stored_format()
        {
            Assert.Equal("1975/01/01", Birthdates.JanuaryFirst(1975));
            Assert.Equal("0905/01/01", Birthdates.JanuaryFirst(905));
        }

        private static Participant NewParticipant(string birthday) => new(
            "Jane", "Runner", "", "", "", "", birthday,
            new EventSpecific(1, 1, "5K", "1", 0, "", "", "", false, false, "", ""),
            "", "", "", "", "", "", "F", "", "");

        [Fact]
        public void Participant_stores_any_input_form_as_year_month_day()
        {
            Assert.Equal("1990/01/02", NewParticipant("1/2/1990").Birthdate);
            Assert.Equal("1990/01/02", NewParticipant("1990/01/02").Birthdate);
        }

        [Fact]
        public void Participants_entered_in_different_forms_compare_equal()
        {
            Assert.Equal(NewParticipant("01/02/1990").Birthdate, NewParticipant("1990-1-2").Birthdate);
        }

        [Fact]
        public void Participant_format_data_uses_the_stored_format_and_a_placeholder_for_non_dates()
        {
            Participant dated = NewParticipant("1/2/1990");
            dated.FormatData(false);
            Assert.Equal("1990/01/02", dated.Birthdate);

            Participant undated = NewParticipant("unknown");
            undated.FormatData(false);
            // FormatData stores a placeholder 130 years back; the Birthdate getter hides anything over 120 years old.
            Assert.Equal("", undated.Birthdate);
        }

        [Fact]
        public void Uploaded_birthdates_use_a_layout_chronokeep_results_accepts()
        {
            // Chronokeep Results (results/types/participant.go) only accepts Go layouts 1/2/2006 and
            // 2006/1/2, and its own tests reject 2000-01-02. Year/month/day with slashes satisfies 2006/1/2.
            Participant runner = NewParticipant("June 16, 1990");
            ApiPerson upload = new(runner, "ID-");

            Assert.Equal("1990/06/16", upload.Birthdate);
            Assert.True(DateTime.TryParseExact(upload.Birthdate, "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
            Assert.DoesNotContain("-", upload.Birthdate);
        }

        [Fact]
        public void Downloaded_api_person_birthdates_use_the_stored_format()
        {
            ApiPerson person = new() { Birthdate = " 01/02/1990 " };
            person.Trim();
            person.FormatData();

            Assert.Equal("1990/01/02", person.Birthdate);
        }
    }
}
