using Chronokeep.IO.HtmlTemplates.Printables;
using Chronokeep.Objects;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Logic
{
    /// <summary>Builds results the way the results database hands them to the printables.</summary>
    internal static class PrintableData
    {
        public static readonly DateTime EventDate = new(2026, 6, 15);

        public static Event TheEvent(int type = TimingConstants.EVENT_TYPE_DISTANCE) => new()
        {
            Identifier = 1,
            Name = "Spring Run",
            Date = EventDate.ToShortDateString(),
            EventType = type,
        };

        private static int nextId = 1;

        public static TimeResult R(
            string bib,
            string name,
            string gender = "Woman",
            string distance = "5K",
            int place = 1,
            int genderPlace = 1,
            int age = 30,
            int ageGroupId = 3,
            string ageGroupName = "30-39",
            int status = TimingConstants.TIMERESULT_STATUS_PROCESSED,
            int segment = TimingConstants.SEGMENT_FINISH,
            int occurrence = 1,
            int eventSpecificId = -1)
        {
            int id = eventSpecificId == -1 ? nextId++ : eventSpecificId;
            TimeResult r = new(1, -1, id, TimingConstants.LOCATION_FINISH, segment, occurrence, 1500 + place, 0,
                TimeResult.BibToIdentifier(bib), 1500 + place, 0, DateTime.Now, bib, status, "");
            string[] split = name.Split(' ');
            EventSpecific es = new(1, 1, distance, bib, 0, "", "", "", false, false, "", "")
            {
                AgeGroupId = ageGroupId,
                AgeGroupName = ageGroupName,
            };
            r.SetParticipant(new Participant(split[0], split.Length > 1 ? split[1] : "", "", "", "", "",
                new DateTime(EventDate.Year - age, 1, 1).ToShortDateString(), es, "", "", "", "", "", "", gender, "", ""));
            r.Place = place;
            r.GenderPlace = genderPlace;
            return r;
        }
    }

    public class PrintableResultsTests
    {
        private static TimeResult R(string bib, string name, string gender = "Woman", string distance = "5K", int place = 1, int genderPlace = 1,
            int age = 30, int ageGroupId = 3, string ageGroupName = "30-39", int status = TimingConstants.TIMERESULT_STATUS_PROCESSED,
            int segment = TimingConstants.SEGMENT_FINISH, int occurrence = 1, int eventSpecificId = -1)
            => PrintableData.R(bib, name, gender, distance, place, genderPlace, age, ageGroupId, ageGroupName, status, segment, occurrence, eventSpecificId);

        [Fact]
        public void Overall_OnlyFinishesForKnownRunners_SortedByPlace()
        {
            TimeResult unknown = R("7", "Ghost");
            unknown.Bib = TimingConstants.CHIPREAD_DUMMYBIB;
            List<TimeResult> results =
            [
                R("2", "Bea Second", place: 2),
                R("1", "Ann First", place: 1),
                R("3", "Cat Split", segment: 5),
                R("4", "Dee Dnf", place: 3, status: TimingConstants.TIMERESULT_STATUS_DNF),
                R("5", "Eve Other", distance: "10K"),
                unknown,
            ];

            var (distanceResults, dnf) = PrintableResults.Overall(results, PrintableData.TheEvent(), ["5K"]);

            Assert.Equal(["5K"], distanceResults.Keys);
            Assert.Equal(["Ann First", "Bea Second"], distanceResults["5K"].Select(r => r.ParticipantName));
            Assert.Equal("Dee Dnf", Assert.Single(dnf["5K"]).ParticipantName);
        }

        [Fact]
        public void NoDistancesChosen_PrintsEveryDistance()
        {
            List<TimeResult> results = [R("1", "Ann First"), R("2", "Bob Other", distance: "10K")];
            var (distanceResults, _) = PrintableResults.Overall(results, PrintableData.TheEvent(), []);
            Assert.Equal(["10K", "5K"], distanceResults.Keys.Order());
        }

        [Fact]
        public void TimeEvent_KeepsEachPersonsLastLap()
        {
            List<TimeResult> results =
            [
                R("1", "Ann Laps", occurrence: 1, eventSpecificId: 100),
                R("1", "Ann Laps", occurrence: 3, eventSpecificId: 100),
                R("1", "Ann Laps", occurrence: 2, eventSpecificId: 100),
            ];
            var (distanceResults, _) = PrintableResults.Overall(results, PrintableData.TheEvent(TimingConstants.EVENT_TYPE_TIME), []);
            Assert.Equal(3, Assert.Single(distanceResults["5K"]).Occurrence);
        }

        [Fact]
        public void TimeEvent_TwoPeopleWithTheSameName_AreBothPrinted()
        {
            List<TimeResult> results =
            [
                R("1", "John Smith", gender: "Man", occurrence: 4, eventSpecificId: 100),
                R("2", "John Smith", gender: "Man", occurrence: 6, eventSpecificId: 200),
            ];
            var (distanceResults, _) = PrintableResults.Overall(results, PrintableData.TheEvent(TimingConstants.EVENT_TYPE_TIME), []);
            Assert.Equal(["1", "2"], distanceResults["5K"].Select(r => r.Bib).Order());
        }

        [Fact]
        public void ByGender_GroupsAndLeavesOutUnknownGender()
        {
            List<TimeResult> results =
            [
                R("1", "Ann First", place: 1),
                R("2", "Bob Second", gender: "Man", place: 2),
                R("3", "Cam Third", gender: "Not Specified", place: 3),
                R("4", "Dee Fourth", place: 4),
            ];
            var (distanceResults, _) = PrintableResults.ByGender(results, PrintableData.TheEvent(), []);
            Assert.Equal(["Man", "Woman"], distanceResults["5K"].Keys.Order());
            Assert.Equal(["Ann First", "Dee Fourth"], distanceResults["5K"]["Woman"].Select(r => r.ParticipantName));
        }

        [Fact]
        public void ByAgeGroup_GroupsByAgeGroupAndGender()
        {
            List<TimeResult> results =
            [
                R("1", "Ann First", ageGroupId: 3, place: 1),
                R("2", "Bob Second", gender: "Man", ageGroupId: 3, place: 2),
                R("3", "Cat Third", ageGroupId: 4, ageGroupName: "40-49", place: 3),
                R("4", "Dee Fourth", ageGroupId: 3, place: 4, status: TimingConstants.TIMERESULT_STATUS_DNF),
            ];
            var (distanceResults, dnf) = PrintableResults.ByAgeGroup(results, PrintableData.TheEvent(), []);
            Assert.Equal("Ann First", Assert.Single(distanceResults["5K"][(3, "Woman")]).ParticipantName);
            Assert.Equal("Bob Second", Assert.Single(distanceResults["5K"][(3, "Man")]).ParticipantName);
            Assert.Equal("Cat Third", Assert.Single(distanceResults["5K"][(4, "Woman")]).ParticipantName);
            Assert.Equal("Dee Fourth", Assert.Single(dnf["5K"][(3, "Woman")]).ParticipantName);
        }

        [Fact]
        public void AgeGroups_AddsOneForUnknownAges()
        {
            Dictionary<int, AgeGroup> groups = PrintableResults.AgeGroups([new AgeGroup(1, 1, 30, 39) { GroupId = 3 }], PrintableData.TheEvent());
            Assert.Equal([TimingConstants.TIMERESULT_DUMMYAGEGROUP, 3], groups.Keys.Order());
        }
    }

    public class AwardResultsTests
    {
        private static readonly Event TheEvent = PrintableData.TheEvent();

        private static TimeResult W(string bib, int genderPlace, int age, string ageGroup)
            => PrintableData.R(bib, $"Woman{bib} Runner", "Woman", "5K", genderPlace, genderPlace, age, age / 10, ageGroup);

        private static TimeResult M(string bib, int genderPlace, int age, string ageGroup)
            => PrintableData.R(bib, $"Man{bib} Runner", "Man", "5K", genderPlace + 100, genderPlace, age, age / 10, ageGroup);

        private static Dictionary<string, List<string>> Winners(List<TimeResult> results, AwardOptions options, List<AgeGroup>? custom = null)
        {
            var (_, byGroup) = PrintableResults.Awards(results, TheEvent, [], options, custom ?? []);
            return byGroup["5K"].Where(kv => kv.Value.Count > 0).ToDictionary(kv => kv.Key, kv => kv.Value.Select(r => r.Bib).ToList());
        }

        [Fact]
        public void Default_TopThreeOverallAndTopThreeInEachAgeGroup()
        {
            List<TimeResult> results =
            [
                W("1", 1, 25, "20-29"), W("2", 2, 25, "20-29"), W("3", 3, 35, "30-39"),
                W("4", 4, 25, "20-29"), W("5", 5, 25, "20-29"), W("6", 6, 35, "30-39"),
                M("10", 1, 45, "40-49"),
            ];

            Dictionary<string, List<string>> winners = Winners(results, new AwardOptions { PrintCustom = false });

            Assert.Equal(["1", "2", "3"], winners["Women"]);
            // Overall winners can also win their age group.
            Assert.Equal(["1", "2", "4"], winners["Women 20-29"]);
            Assert.Equal(["3", "6"], winners["Women 30-39"]);
            Assert.Equal(["10"], winners["Men"]);
            Assert.Equal(["10"], winners["Men 40-49"]);
        }

        [Fact]
        public void ExcludingOverallWinners_AgeGroupsGoToTheNextRunners()
        {
            List<TimeResult> results =
            [
                W("1", 1, 25, "20-29"), W("2", 2, 25, "20-29"), W("3", 3, 25, "20-29"),
                W("4", 4, 25, "20-29"), W("5", 5, 25, "20-29"),
            ];

            Dictionary<string, List<string>> winners = Winners(results, new AwardOptions { ExcludeOverallAg = true, NumAgeGroups = 2, PrintCustom = false });

            Assert.Equal(["1", "2", "3"], winners["Women"]);
            Assert.Equal(["4", "5"], winners["Women 20-29"]);
        }

        [Fact]
        public void NotPrintingOverall_StillShowsAgeGroups()
        {
            List<TimeResult> results = [W("1", 1, 25, "20-29"), W("2", 2, 35, "30-39")];
            Dictionary<string, List<string>> winners = Winners(results, new AwardOptions { PrintOverall = false, PrintCustom = false });
            Assert.False(winners.ContainsKey("Women"));
            Assert.Equal(["1"], winners["Women 20-29"]);
            Assert.Equal(["2"], winners["Women 30-39"]);
        }

        [Fact]
        public void OverallWinnerOutsideTheAgeGroupCount_CanStillWinTheirAgeGroup()
        {
            // One age group award each. The 2nd woman overall is the fastest 30-39 woman,
            // and overall winners aren't excluded, so she should win that age group.
            List<TimeResult> results =
            [
                W("1", 1, 25, "20-29"), W("2", 2, 35, "30-39"), W("3", 3, 35, "30-39"), W("4", 4, 35, "30-39"),
            ];

            Dictionary<string, List<string>> winners = Winners(results, new AwardOptions { NumAgeGroups = 1, PrintCustom = false });

            Assert.Equal(["2"], winners["Women 30-39"]);
        }

        [Fact]
        public void CustomGroups_HaveTheirOwnWinners()
        {
            List<TimeResult> results =
            [
                W("1", 1, 25, "20-29"), W("2", 2, 45, "40-49"), W("3", 3, 55, "50-59"),
            ];
            List<AgeGroup> custom = [new AgeGroup(1, 1, 40, 59, "Masters")];

            Dictionary<string, List<string>> winners = Winners(results, new AwardOptions { NumOverall = 1 }, custom);

            Assert.Equal(["2", "3"], winners["Women Masters"]);
            Assert.Equal(["2"], winners["Women 40-49"]);
            Assert.Equal(["3"], winners["Women 50-59"]);
        }

        [Fact]
        public void CustomGroups_ExcludingAgeGroupWinners_GoToTheNextRunner()
        {
            List<TimeResult> results =
            [
                W("1", 1, 25, "20-29"), W("2", 2, 45, "40-49"), W("3", 3, 45, "40-49"),
            ];
            List<AgeGroup> custom = [new AgeGroup(1, 1, 40, 59, "Masters")];

            Dictionary<string, List<string>> winners = Winners(results, new AwardOptions { NumOverall = 1, NumAgeGroups = 1, ExcludeAgeGroupsCustom = true }, custom);

            Assert.Equal(["2"], winners["Women 40-49"]);
            Assert.Equal(["3"], winners["Women Masters"]);
        }

        [Fact]
        public void CustomGroups_OverallWinnersCount()
        {
            List<TimeResult> results = [W("1", 1, 45, "40-49"), W("2", 2, 25, "20-29")];
            List<AgeGroup> custom = [new AgeGroup(1, 1, 40, 59, "Masters")];
            Dictionary<string, List<string>> winners = Winners(results, new AwardOptions(), custom);
            Assert.Equal(["1"], winners["Women Masters"]);
        }

        [Fact]
        public void DnfsAndUnknownGendersDontWinAgeGroups()
        {
            TimeResult dnf = W("1", 1, 25, "20-29");
            dnf.Status = TimingConstants.TIMERESULT_STATUS_DNF;
            TimeResult unknown = PrintableData.R("2", "Sam Runner", "Not Specified", "5K", 5, 5, 25, 2, "20-29");
            Dictionary<string, List<string>> winners = Winners([dnf, unknown, W("3", 2, 25, "20-29")], new AwardOptions { NumOverall = 1, PrintCustom = false });
            Assert.Equal(["3"], winners["Women 20-29"]);
            Assert.DoesNotContain(winners.Keys, k => k.Contains("Not Specified") || k.StartsWith(' '));
        }

        [Fact]
        public void Groups_ListOnlyGroupsWithWinners()
        {
            List<TimeResult> results = [W("1", 1, 25, "20-29"), M("2", 1, 35, "30-39")];
            var (groups, _) = PrintableResults.Awards(results, TheEvent, [], new AwardOptions { PrintCustom = false }, []);
            Assert.Equal(["Men", "Men 30-39", "Women", "Women 20-29"], groups["5K"]);
        }

        [Theory]
        [InlineData("Men", "Men 10-19")]
        [InlineData("Men 10-19", "Women")]
        [InlineData("Women Under 10", "Women 10-19")]
        [InlineData("Women 60-69", "Women Over 70")]
        [InlineData("Women 20-29", "Women 30-39")]
        [InlineData("Women 5-9", "Women 10-19")]
        [InlineData("Women 20-29", "Women 100-109")]
        public void CompareGroups_FirstComesBeforeSecond(string first, string second)
        {
            Assert.True(PrintableResults.CompareGroups(first, second) < 0, $"{first} should come before {second}");
            Assert.True(PrintableResults.CompareGroups(second, first) > 0, $"{second} should come after {first}");
        }
    }

    /// <summary>The printables render the data built above.</summary>
    public class PrintableTemplateRenderTests
    {
        private static List<TimeResult> Results() =>
        [
            PrintableData.R("1", "Ann First", place: 1, genderPlace: 1, age: 25, ageGroupId: 2, ageGroupName: "20-29"),
            PrintableData.R("2", "Bob Second", gender: "Man", place: 2, genderPlace: 1, age: 35, ageGroupId: 3, ageGroupName: "30-39"),
            PrintableData.R("3", "Dee Dropped", place: 3, genderPlace: 2, status: TimingConstants.TIMERESULT_STATUS_DNF),
        ];

        [Fact]
        public void Overall()
        {
            Event e = PrintableData.TheEvent();
            var (results, dnf) = PrintableResults.Overall(Results(), e, []);
            string html = new ResultsPrintableOverall(e, results, dnf).TransformText();
            Assert.Contains("Spring Run", html);
            Assert.Contains("Ann First", html);
            Assert.Contains("Bob Second", html);
            Assert.Contains("Dee Dropped", html);
        }

        [Fact]
        public void Gender()
        {
            Event e = PrintableData.TheEvent();
            var (results, dnf) = PrintableResults.ByGender(Results(), e, []);
            string html = new ResultsPrintableGender(e, results, dnf).TransformText();
            Assert.Contains("Ann First", html);
            Assert.Contains("Bob Second", html);
        }

        [Fact]
        public void AgeGroup()
        {
            Event e = PrintableData.TheEvent();
            Dictionary<int, AgeGroup> groups = PrintableResults.AgeGroups(
                [new AgeGroup(1, 1, 20, 29) { GroupId = 2 }, new AgeGroup(1, 1, 30, 39) { GroupId = 3 }], e);
            var (results, dnf) = PrintableResults.ByAgeGroup(Results(), e, []);
            string html = new ResultsPrintableAgeGroup(e, results, dnf, groups).TransformText();
            Assert.Contains("Ann First", html);
            Assert.Contains("Women 20 - 29", html);
        }

        [Fact]
        public void Awards()
        {
            Event e = PrintableData.TheEvent();
            var (groups, results) = PrintableResults.Awards(Results(), e, [], new AwardOptions { PrintCustom = false }, []);
            string html = new AwardsPrintable(e, groups, results).TransformText();
            Assert.Contains("Women 20-29", html);
            Assert.Contains("Ann First", html);
            Assert.DoesNotContain("Dee Dropped", html);
        }

        [Fact]
        public void Awards_ADistanceWithNoWinners_StillRenders()
        {
            // The distance is left out of the data entirely.
            // Only custom awards, and nobody is in the custom age range.
            Event e = PrintableData.TheEvent();
            var (groups, results) = PrintableResults.Awards(Results(), e, [],
                new AwardOptions { PrintOverall = false, PrintAgeGroups = false }, [new AgeGroup(1, 1, 60, 99, "Seniors")]);
            string html = new AwardsPrintable(e, groups, results).TransformText();
            Assert.Contains("Spring Run", html);
        }
    }
}
