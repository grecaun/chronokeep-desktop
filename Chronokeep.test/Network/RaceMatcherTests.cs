using Chronokeep.Network.Runsignup;
using Chronokeep.Objects;
using Chronokeep.Objects.Runsignup;

namespace Chronokeep.Test.Network
{
    /// <summary>Finding the Runsignup race an event is, by name and date, from a timer's real race list.</summary>
    public class RaceMatcherTests
    {
        private static RsuRace Race(string id, string name, string next, string last = "") => new(id, name, next, [], last);

        private static readonly List<RsuRace> Races =
        [
            Race("174855", "2025 Seattle Ghost Marathon, Half & 50K", "", "11/29/2025"),
            Race("196229", "2026 Seattle Ghost Marathon, Half & 50K", "11/28/2026"),
            Race("210506", "40th Annual Bay View Women's Run", "05/15/2027"),
            Race("119170", "Anacortes Santa Run", "12/12/2026", "12/06/2025"),
            Race("179316", "Berry Dairy Days 5K and 10K Race", "", "06/20/2026"),
            Race("177526", "Fowl Fun Run 5K & 10K", "11/14/2026", "11/15/2025"),
            Race("169638", "Last Chance Marathon and Half Marathon", "12/31/2026", "12/31/2025"),
            Race("115393", "Never Say Never 10K", "05/01/2027", "05/02/2026"),
            Race("176741", "Skagit Valley Tulip Run 2 Mile, 5K & 10K", "", "04/18/2026"),
            Race("122579", "Swing the Gates 5k and 10k", "10/31/2026"),
            Race("217976", "Virtual Swing the Gates 5k and 10k", "11/01/2026"),
            Race("170102", "Woolley Trail Runs", "02/13/2027", "02/14/2026"),
        ];

        private static string? Match(string name, string date) => RaceMatcher.BestMatch(new Event { Name = name, Date = date }, Races)?.RaceId;

        [Theory]
        [InlineData("Fowl Fun Run", "2026/11/14", "177526")]
        [InlineData("Fowl Fun Run 2025", "2025/11/15", "177526")]
        [InlineData("Seattle Ghost Marathon", "2026/11/28", "196229")]
        [InlineData("Seattle Ghost Marathon", "2025/11/29", "174855")]
        [InlineData("Woolley Trail Run", "2027/02/13", "170102")]
        [InlineData("Last Chance Marathon", "2025/12/31", "169638")]
        [InlineData("Tulip Run", "2026/04/18", "176741")]
        [InlineData("Bay View Women's Run", "2027/05/15", "210506")]
        [InlineData("Swing the Gates", "2026/10/31", "122579")]
        [InlineData("Virtual Swing the Gates", "2026/11/01", "217976")]
        [InlineData("Never Say Never", "2026/05/02", "115393")]
        [InlineData("Berry Dairy Days & Berry Run", "2026/06/20", "179316")]
        public void Finds_the_race_by_name_and_date(string name, string date, string raceId)
        {
            Assert.Equal(raceId, Match(name, date));
        }

        [Theory]
        [InlineData("Turkey Trot", "2026/11/26")]
        [InlineData("Spring Fling 5K", "2026/06/15")]
        [InlineData("Run", "2026/03/01")]
        [InlineData("Fowl Weather Relay", "2026/03/01")]
        public void Picks_nothing_when_no_race_is_close(string name, string date)
        {
            Assert.Null(Match(name, date));
        }

        [Fact]
        public void A_name_that_matches_needs_a_date_nearby_unless_it_matches_completely()
        {
            Assert.Equal("170102", Match("Woolley Trail Runs", "2020/01/01"));
            Assert.Null(Match("Woolley Trail Half", "2020/01/01"));
        }

        [Fact]
        public void Identical_races_arent_guessed_between()
        {
            List<RsuRace> twins = [Race("1", "Spring Fling", "06/15/2026"), Race("2", "Spring Fling", "06/15/2026")];

            Assert.Null(RaceMatcher.BestMatch(new Event { Name = "Spring Fling", Date = "2026/06/15" }, twins));
        }

        [Fact]
        public void Words_ignore_years_ordinals_filler_and_plurals()
        {
            Assert.Equal(["bay", "view", "women", "run"], RaceMatcher.Words("40th Annual Bay View Women's Runs 2027"));
        }
    }
}
