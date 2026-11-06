using Chronokeep.Objects;

namespace Chronokeep.Test.Logic
{
    public class ParticipantLogicTests
    {
        private static string D(int y, int m, int d) => new DateTime(y, m, d).ToShortDateString();

        private static Participant Make(
            string first = "Jane", string last = "Runner", string birthday = "",
            string bib = "1", string gender = "F", string street = "1 Main St", string street2 = "",
            string state = "OR", string country = "US", string phone = "", string city = "Eugene",
            int distanceId = 1, string distanceName = "5K")
        {
            return new Participant(first, last, street, city, state, "97401", birthday == "" ? D(1990, 6, 15) : birthday,
                new EventSpecific(1, distanceId, distanceName, bib, 0, "", "", "", false, false, "", ""),
                "", phone, "", "", country, street2, gender, "", "");
        }

        [Theory]
        [InlineData(2026, 6, 14, 35)] // day before birthday
        [InlineData(2026, 6, 15, 36)] // on birthday
        [InlineData(2026, 12, 31, 36)]
        [InlineData(2026, 1, 1, 35)]
        public void GetAge_CountsBirthdaysBeforeEventDate(int y, int m, int d, int expected)
        {
            var p = Make(birthday: D(1990, 6, 15));
            Assert.Equal(expected, p.GetAge(D(y, m, d)));
            Assert.Equal(expected.ToString(), p.Age(D(y, m, d)));
        }

        [Fact]
        public void Age_OverOneHundredTwenty_IsTreatedAsUnknown()
        {
            var p = Make(birthday: D(1880, 1, 1));
            Assert.Equal(-1, p.GetAge(D(2026, 6, 15)));
            Assert.Equal("", p.Age(D(2026, 6, 15)));
            Assert.Equal("", p.Birthdate);
        }

        [Fact]
        public void Birthdate_Unparseable_ReturnsEmpty()
        {
            var p = Make(birthday: "not a date");
            Assert.Equal("", p.Birthdate);
        }

        [Theory]
        [InlineData("JOHN", "John")]
        [InlineData("john", "John")]
        [InlineData("McDonald", "McDonald")]
        [InlineData("o'neil", "O'neil")]
        public void FormatData_CapitalizesNames(string raw, string expected)
        {
            var p = Make(first: raw, last: raw);
            p.FormatData(false);
            Assert.Equal(expected, p.FirstName);
            Assert.Equal(expected, p.LastName);
        }

        [Theory]
        [InlineData("Oregon", "OR")]
        [InlineData("washington", "WA")]
        [InlineData("District of Columbia", "DC")]
        [InlineData("or", "OR")]
        [InlineData("Ontario", "Ontario")]
        public void FormatData_AbbreviatesUsStates(string raw, string expected)
        {
            var p = Make(state: raw);
            p.FormatData(false);
            Assert.Equal(expected, p.State);
        }

        [Theory]
        [InlineData("USA", "US")]
        [InlineData("United States of America", "US")]
        [InlineData("canada", "CA")]
        [InlineData("Mexico", "Mexico")]
        public void FormatData_NormalizesCountries(string raw, string expected)
        {
            var p = Make(country: raw);
            p.FormatData(false);
            Assert.Equal(expected, p.Country);
        }

        [Theory]
        [InlineData("5551234567", "555-123-4567")]
        [InlineData("(555) 123.4567", "555-123-4567")]
        [InlineData("+1 555 123 4567", "1-555-123-4567")]
        [InlineData("12345", "12345")]
        public void FormatData_FormatsPhoneNumbers(string raw, string expected)
        {
            var p = Make(phone: raw);
            p.FormatData(false);
            Assert.Equal(expected, p.Phone);
        }

        [Theory]
        [InlineData("m", false, "Man")]
        [InlineData("Male", true, "Male")]
        [InlineData("F", false, "Woman")]
        [InlineData("woman", true, "Female")]
        [InlineData("x", false, "Non-Binary")]
        [InlineData("non binary", false, "Non-Binary")]
        [InlineData("", false, "Not Specified")]
        [InlineData("two spirit", false, "Two Spirit")]
        public void FormatData_NormalizesGender(string raw, bool useMaleFemale, string expected)
        {
            var p = Make(gender: raw);
            p.FormatData(useMaleFemale);
            Assert.Equal(expected, p.Gender);
        }

        [Fact]
        public void FormatData_SplitsStreetIntoTwoLines()
        {
            var p = Make(street: "1 Main St, Apt 4");
            p.FormatData(false);
            Assert.Equal("1 Main St", p.Street);
            Assert.Equal("Apt 4", p.Street2);
        }

        [Fact]
        public void FormatData_StreetWithSeveralCommas_KeepsFirstPartTrimmed()
        {
            var p = Make(street: "1 Main St , Apt 4, Building C");
            p.FormatData(false);
            Assert.Equal("1 Main St", p.Street);
            Assert.Equal("", p.Street2);
        }

        [Fact]
        public void FormatData_KeepsExistingStreet2()
        {
            var p = Make(street: "1 Main St, Apt 4", street2: "Unit B");
            p.FormatData(false);
            Assert.Equal("1 Main St, Apt 4", p.Street);
            Assert.Equal("Unit B", p.Street2);
        }

        [Fact]
        public void FormatData_UnparseableBirthdate_BecomesUnknown()
        {
            var p = Make(birthday: "garbage");
            p.FormatData(false);
            Assert.Equal("", p.Birthdate);
            Assert.Equal(-1, p.GetAge(D(2026, 6, 15)));
        }

        [Fact]
        public void Trim_TrimsEveryField()
        {
            var p = new Participant(" Jane ", " Runner ", " 1 Main ", " City ", " OR ", " 97401 ", D(1990, 1, 1),
                new EventSpecific(1, 1, " 5K ", " 7 ", 0, " c ", " o ", " x ", false, false, " M ", " Elite "),
                " e@x.com ", " 555 ", " 556 ", " P ", " US ", " Apt ", " F ", " EC ", " 557 ");
            p.Trim();
            Assert.Equal("Jane", p.FirstName);
            Assert.Equal("Runner", p.LastName);
            Assert.Equal("7", p.Bib);
            Assert.Equal("Elite", p.Division);
            Assert.Equal("e@x.com", p.Email);
        }

        [Fact]
        public void CompareByBib_UsesNumericOrderWhenPossible()
        {
            var parts = new List<Participant> { Make(bib: "10"), Make(bib: "9"), Make(bib: "B2"), Make(bib: "100") };
            parts.Sort(Participant.CompareByBib);
            Assert.Equal(["9", "10", "100", "B2"], parts.Select(p => p.Bib));
        }

        [Fact]
        public void CompareTo_SortsByDistanceThenLastThenFirst()
        {
            var parts = new List<Participant>
            {
                Make(first: "Bob", last: "Smith", distanceId: 2, distanceName: "10K"),
                Make(first: "Amy", last: "Smith"),
                Make(first: "Zed", last: "Adams"),
            };
            parts.Sort();
            Assert.Equal(["Bob", "Zed", "Amy"], parts.Select(p => p.FirstName));
        }

        [Fact]
        public void Is_IgnoresCaseAndBib()
        {
            Assert.True(Make(first: "jane", bib: "1").Is(Make(first: "JANE", bib: "2")));
            Assert.False(Make(first: "Jane").Is(Make(first: "Janet")));
            Assert.False(Make().Is(null));
        }

        [Fact]
        public void IsNotMatch_SearchesBibAndNames()
        {
            var p = Make(first: "Jane", last: "Runner", bib: "42");
            Assert.False(p.IsNotMatch("run"));
            Assert.False(p.IsNotMatch("42"));
            Assert.True(p.IsNotMatch("zzz"));
        }
    }
}
