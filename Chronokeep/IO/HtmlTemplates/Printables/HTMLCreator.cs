using Chronokeep.Objects;
using System.Collections.Generic;

namespace Chronokeep.IO.HtmlTemplates.Printables
{
    public partial class ResultsPrintableOverall(Event theEvent,
        Dictionary<string, List<TimeResult>> distanceResults,
        Dictionary<string, List<TimeResult>> dnfResultsDictionary)
    { }

    public partial class ResultsPrintableAgeGroup(
        Event theEvent,
        Dictionary<string, Dictionary<(int, string), List<TimeResult>>> distanceResults,
        Dictionary<string, Dictionary<(int, string), List<TimeResult>>> dnfResultsDictionary,
        Dictionary<int, AgeGroup> ageGroups)
    { }

    public partial class ResultsPrintableGender(
        Event theEvent,
        Dictionary<string, Dictionary<string, List<TimeResult>>> distanceResults,
        Dictionary<string, Dictionary<string, List<TimeResult>>> dnfResultsDictionary)
    { }

    public partial class AwardsPrintable(
        Event theEvent,
        Dictionary<string, List<string>> distanceGroups,
        Dictionary<string, Dictionary<string, List<TimeResult>>> distanceResults)
    { }
}