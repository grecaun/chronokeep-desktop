/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.IO.HtmlTemplates.Printables
{
    /// <summary>
    /// Builds the data shown on the results and awards printables from an event's results.
    /// </summary>
    internal static class PrintableResults
    {
        /// <summary>
        /// Gets the finish results to print.
        /// </summary>
        /// <param name="results">Every result for the event. This list is changed.</param>
        /// <param name="distances">The distances to print, or an empty list for every distance.</param>
        /// <returns>The finish results for known participants in the chosen distances.</returns>
        private static List<TimeResult> FinishResults(List<TimeResult> results, List<string> distances)
        {
            // Remove all unknown participants
            results.RemoveAll(x => Constants.Timing.CHIPREAD_DUMMYBIB == x.Bib);
            results.RemoveAll(x => x.DistanceName.Length < 1);
            if (distances.Count > 0)
            {
                results.RemoveAll(x => !distances.Contains(x.DistanceName));
            }
            // remove all segments that are not finish segments
            results.RemoveAll(x => Constants.Timing.SEGMENT_FINISH != x.SegmentId);
            return results;
        }

        /// <summary>
        /// For time based events, keeps only each person's last result.
        /// </summary>
        /// <param name="results">The finish results.</param>
        /// <param name="theEvent">The event the results are for.</param>
        /// <returns>The results to print.</returns>
        private static List<TimeResult> LastResults(List<TimeResult> results, Event theEvent)
        {
            if (Constants.Timing.EVENT_TYPE_TIME != theEvent.EventType)
            {
                return results;
            }
            // Keyed by person, not name, since two people can share a name.
            Dictionary<(int, string), TimeResult> lastResult = [];
            foreach (TimeResult individual in results)
            {
                (int, string) person = (individual.EventSpecificId, individual.Bib);
                if (lastResult.TryGetValue(person, out TimeResult? oLastRes))
                {
                    if (oLastRes.Occurrence < individual.Occurrence)
                    {
                        lastResult[person] = individual;
                    }
                }
                else
                {
                    lastResult[person] = individual;
                }
            }
            return [.. lastResult.Values];
        }

        /// <summary>
        /// Builds the overall results printable's data.
        /// </summary>
        /// <param name="results">Every result for the event. This list is changed.</param>
        /// <param name="theEvent">The event the results are for.</param>
        /// <param name="distances">The distances to print, or an empty list for every distance.</param>
        /// <returns>Results by distance, sorted by place, and DNF results by distance.</returns>
        internal static (Dictionary<string, List<TimeResult>> Results, Dictionary<string, List<TimeResult>> Dnf) Overall(
            List<TimeResult> results,
            Event theEvent,
            List<string> distances
            )
        {
            results = LastResults(FinishResults(results, distances), theEvent);
            Dictionary<string, List<TimeResult>> distanceResults = [];
            Dictionary<string, List<TimeResult>> dnfResultDictionary = [];
            foreach (TimeResult result in results)
            {
                if (!distanceResults.TryGetValue(result.DistanceName, out List<TimeResult>? oDistResultList))
                {
                    oDistResultList = [];
                    distanceResults[result.DistanceName] = oDistResultList;
                }
                if (Constants.Timing.TIMERESULT_STATUS_DNF == result.Status)
                {
                    if (!dnfResultDictionary.TryGetValue(result.DistanceName, out List<TimeResult>? oDnfResList))
                    {
                        oDnfResList = [];
                        dnfResultDictionary[result.DistanceName] = oDnfResList;
                    }
                    oDnfResList.Add(result);
                }
                else
                {
                    oDistResultList.Add(result);
                }
            }
            foreach (List<TimeResult> distResults in distanceResults.Values)
            {
                distResults.Sort(TimeResult.CompareByDistancePlace);
            }
            return (distanceResults, dnfResultDictionary);
        }

        /// <summary>
        /// Builds the gender results printable's data. Results without a gender are left out.
        /// </summary>
        /// <param name="results">Every result for the event. This list is changed.</param>
        /// <param name="theEvent">The event the results are for.</param>
        /// <param name="distances">The distances to print, or an empty list for every distance.</param>
        /// <returns>Results by distance then gender, sorted by place, and DNF results grouped the same way.</returns>
        internal static (Dictionary<string, Dictionary<string, List<TimeResult>>> Results, Dictionary<string, Dictionary<string, List<TimeResult>>> Dnf) ByGender(
            List<TimeResult> results,
            Event theEvent,
            List<string> distances
            )
        {
            results = FinishResults(results, distances);
            // remove all results without a gender specified
            results.RemoveAll(x => "Not Specified" == x.Gender);
            results = LastResults(results, theEvent);
            // separate each grouping by distance, then by gender
            Dictionary<string, Dictionary<string, List<TimeResult>>> distanceResults = [];
            Dictionary<string, Dictionary<string, List<TimeResult>>> dnfResultsDictionary = [];
            foreach (TimeResult result in results)
            {
                if (!distanceResults.TryGetValue(result.DistanceName, out Dictionary<string, List<TimeResult>>? oDistResDict))
                {
                    oDistResDict = [];
                    distanceResults[result.DistanceName] = oDistResDict;
                }
                if (!oDistResDict.TryGetValue(result.Gender, out List<TimeResult>? oDistGenderResList))
                {
                    oDistGenderResList = [];
                    oDistResDict[result.Gender] = oDistGenderResList;
                }
                if (Constants.Timing.TIMERESULT_STATUS_DNF == result.Status)
                {
                    if (!dnfResultsDictionary.TryGetValue(result.DistanceName, out Dictionary<string, List<TimeResult>>? oDnfResDict))
                    {
                        oDnfResDict = [];
                        dnfResultsDictionary[result.DistanceName] = oDnfResDict;
                    }
                    if (!oDnfResDict.TryGetValue(result.Gender, out List<TimeResult>? oDnfGndResList))
                    {
                        oDnfGndResList = [];
                        oDnfResDict[result.Gender] = oDnfGndResList;
                    }
                    oDnfGndResList.Add(result);
                }
                else
                {
                    oDistGenderResList.Add(result);
                }
            }
            foreach (Dictionary<string, List<TimeResult>> genderResults in distanceResults.Values)
            {
                foreach (List<TimeResult> genderResultList in genderResults.Values)
                {
                    genderResultList.Sort(TimeResult.CompareByDistancePlace);
                }
            }
            return (distanceResults, dnfResultsDictionary);
        }

        /// <summary>
        /// Builds the age group results printable's data. Results without a gender are left out.
        /// </summary>
        /// <param name="results">Every result for the event. This list is changed.</param>
        /// <param name="theEvent">The event the results are for.</param>
        /// <param name="distances">The distances to print, or an empty list for every distance.</param>
        /// <returns>Results by distance then (age group id, gender), sorted by place, and DNF results grouped the same way.</returns>
        internal static (Dictionary<string, Dictionary<(int, string), List<TimeResult>>> Results, Dictionary<string, Dictionary<(int, string), List<TimeResult>>> Dnf) ByAgeGroup(
            List<TimeResult> results,
            Event theEvent,
            List<string> distances
            )
        {
            results = FinishResults(results, distances);
            // remove all results without a gender specified
            results.RemoveAll(x => "Not Specified" == x.Gender);
            results = LastResults(results, theEvent);
            Dictionary<string, Dictionary<(int, string), List<TimeResult>>> distanceResults = [];
            Dictionary<string, Dictionary<(int, string), List<TimeResult>>> dnfResultsDictionary = [];
            foreach (TimeResult result in results)
            {
                if (!distanceResults.TryGetValue(result.DistanceName, out Dictionary<(int, string), List<TimeResult>>? oDistResDict))
                {
                    oDistResDict = [];
                    distanceResults[result.DistanceName] = oDistResDict;
                }
                if (!oDistResDict.TryGetValue((result.AgeGroupId, result.Gender), out List<TimeResult>? oDistResList))
                {
                    oDistResList = [];
                    oDistResDict[(result.AgeGroupId, result.Gender)] = oDistResList;
                }
                if (Constants.Timing.TIMERESULT_STATUS_DNF == result.Status)
                {
                    if (!dnfResultsDictionary.TryGetValue(result.DistanceName, out Dictionary<(int, string), List<TimeResult>>? oDnfResDict))
                    {
                        oDnfResDict = [];
                        dnfResultsDictionary[result.DistanceName] = oDnfResDict;
                    }
                    if (!oDnfResDict.TryGetValue((result.AgeGroupId, result.Gender), out List<TimeResult>? oDnfResList))
                    {
                        oDnfResList = [];
                        oDnfResDict[(result.AgeGroupId, result.Gender)] = oDnfResList;
                    }
                    oDnfResList.Add(result);
                }
                else
                {
                    oDistResList.Add(result);
                }
            }
            foreach (Dictionary<(int, string), List<TimeResult>> groupResults in distanceResults.Values)
            {
                foreach (List<TimeResult> groupResultList in groupResults.Values)
                {
                    groupResultList.Sort(TimeResult.CompareByDistancePlace);
                }
            }
            return (distanceResults, dnfResultsDictionary);
        }

        /// <summary>
        /// The age groups to show on the age group printable: the event's age groups plus one for people with an unknown age.
        /// </summary>
        /// <param name="ageGroups">The event's age groups.</param>
        /// <param name="theEvent">The event.</param>
        /// <returns>Age groups by group id.</returns>
        internal static Dictionary<int, AgeGroup> AgeGroups(List<AgeGroup> ageGroups, Event theEvent)
        {
            Dictionary<int, AgeGroup> output = ageGroups.ToDictionary(x => x.GroupId, x => x);
            output[Constants.Timing.TIMERESULT_DUMMYAGEGROUP] = new AgeGroup(theEvent.Identifier, Constants.Timing.COMMON_AGEGROUPS_DISTANCEID, -1, 3000);
            return output;
        }

        /// <summary>
        /// Builds the awards printable's data.
        /// </summary>
        /// <param name="results">Every result for the event. This list is changed.</param>
        /// <param name="theEvent">The event the results are for.</param>
        /// <param name="distances">The distances to print, or an empty list for every distance.</param>
        /// <param name="options">Which awards to print and how many people get each one.</param>
        /// <param name="customAgeGroups">The custom age groups to give awards for.</param>
        /// <returns>
        /// Groups is each distance's award groups in the order they're printed. Results is the winners by
        /// distance then award group, where an award group is a gender ("Women") for overall awards or a
        /// gender and age group ("Women 20-29") for age group and custom awards.
        /// </returns>
        internal static (Dictionary<string, List<string>> Groups, Dictionary<string, Dictionary<string, List<TimeResult>>> Results) Awards(
            List<TimeResult> results,
            Event theEvent,
            List<string> distances,
            AwardOptions options,
            IEnumerable<AgeGroup> customAgeGroups
            )
        {
            results = FinishResults(results, distances);
            // Remove all DNF results.
            results.RemoveAll(x => Constants.Timing.TIMERESULT_STATUS_DNF == x.Status);
            results = LastResults(results, theEvent);
            results.Sort(TimeResult.CompareByDistancePlace);
            // This dictionary stores the list of results with the key being the distance and the header for the grouping (Overall, Age Group, Custom)
            Dictionary<string, Dictionary<string, List<TimeResult>>> resultsDictionary = [];
            // This dictionary keeps track of the number of individuals in an age group so we can choose to not print age group awards
            // and still exclude them from the results.
            Dictionary<(string, string), int> ageGroupCounter = [];
            foreach (TimeResult result in results)
            {
                // Gather the gender and modify it to what we want for use in results.
                string gender = result.Gender switch
                {
                    "Woman" => "Women",
                    "Man" => "Men",
                    "Not Specified" => "",
                    _ => result.Gender
                };
                bool addedToAgeGroupResults = false;
                if (!resultsDictionary.TryGetValue(result.DistanceName, out Dictionary<string, List<TimeResult>>? distResultsDict))
                {
                    distResultsDict = [];
                    resultsDictionary[result.DistanceName] = distResultsDict;
                }
                // Get the overall (gender) results.
                if (result.GenderPlace <= options.NumOverall)
                {
                    // Check if we're printing the overall results.
                    if (options.PrintOverall)
                    {
                        if (!distResultsDict.TryGetValue(gender, out List<TimeResult>? ovResults))
                        {
                            ovResults = [];
                            distResultsDict[gender] = ovResults;
                        }
                        ovResults.Add(result);
                    }
                    // Check if we were told to exclude overall from age group awards.
                    // Exclude any genders we don't know about.
                    // Results are in place order, so the age group counter tells us if the person is in the age group results.
                    if (!options.ExcludeOverallAg && "" != gender)
                    {
                        string ageGroup = $"{gender} {result.AgeGroupName}";
                        int oAgCount = ageGroupCounter.GetValueOrDefault((result.DistanceName, ageGroup), 0);
                        if (oAgCount < options.NumAgeGroups)
                        {
                            if (options.PrintAgeGroups)
                            {
                                if (!distResultsDict.TryGetValue(ageGroup, out List<TimeResult>? oResList))
                                {
                                    oResList = [];
                                    distResultsDict[ageGroup] = oResList;
                                }
                                oResList.Add(result);
                            }
                            ageGroupCounter[(result.DistanceName, ageGroup)] = oAgCount + 1;
                            addedToAgeGroupResults = true;
                        }
                    }
                    // This is almost the same as the age groups category.
                    // Check if told to exclude from custom results.
                    // If we were told to print custom results
                    // exclude any unknown genders
                    // check if we were told to exclude age group winners from custom winners, if so only include ones we didn't add above.
                    // this will exclude any that would have won an age group award even if we didn't actually print it.
                    // this is the behavior we want and should work the same for overall as well
                    if (options.ExcludeOverallCustom
                        || !options.PrintCustom
                        || "" == gender
                        || (options.ExcludeAgeGroupsCustom && addedToAgeGroupResults)) continue;
                    AddToCustomGroups(result, gender, theEvent, options, customAgeGroups, distResultsDict);
                }
                else if ("" != gender)
                {
                    // We're not in the overall results.
                    // Check for age groups.
                    string ageGroup = $"{gender} {result.AgeGroupName}";
                    if (!distResultsDict.TryGetValue(ageGroup, out List<TimeResult>? oResList))
                    {
                        oResList = [];
                        distResultsDict[ageGroup] = oResList;
                    }
                    // We're doing it this way so we can exclude people from custom if we want even if we don't print the age group.
                    int oAgCount = ageGroupCounter.GetValueOrDefault((result.DistanceName, ageGroup), 0);
                    if (oAgCount < options.NumAgeGroups)
                    {
                        if (options.PrintAgeGroups)
                        {
                            oResList.Add(result);
                        }
                        ageGroupCounter[(result.DistanceName, ageGroup)] = oAgCount + 1;
                        addedToAgeGroupResults = true;
                    }
                    // Check for custom groups.
                    // Ensure we don't care about excluding age group winners, or they didn't actually win
                    if (!options.PrintCustom ||
                        (options.ExcludeAgeGroupsCustom && addedToAgeGroupResults)) continue;
                    AddToCustomGroups(result, gender, theEvent, options, customAgeGroups, distResultsDict);
                }
            }
            // Collect all the groups into lists according to their distance
            // We do this so we can sort them by age group.
            Dictionary<string, List<string>> distanceGroups = [];
            foreach (string dist in resultsDictionary.Keys)
            {
                Dictionary<string, List<TimeResult>> distResultsDictionary = resultsDictionary[dist];
                foreach (string group in distResultsDictionary.Keys.Where(group => distResultsDictionary[group].Count > 0))
                {
                    if (!distanceGroups.TryGetValue(dist, out List<string>? distGroupList))
                    {
                        distGroupList = [];
                        distanceGroups[dist] = distGroupList;
                    }
                    if (!distGroupList.Contains(group))
                    {
                        distGroupList.Add(group);
                    }
                }
            }
            // sort our lists
            foreach (List<string> groups in distanceGroups.Values)
            {
                groups.Sort(CompareGroups);
            }
            // Leave out distances with no winners at all, there's nothing to print for them.
            foreach (string dist in resultsDictionary.Keys.Where(dist => !distanceGroups.ContainsKey(dist)).ToList())
            {
                resultsDictionary.Remove(dist);
            }
            return (distanceGroups, resultsDictionary);
        }

        /// <summary>
        /// Adds a winner to each custom age group they're in, up to the number of custom awards.
        /// </summary>
        /// <param name="result">The result to add.</param>
        /// <param name="gender">The gender as shown on the awards ("Women", "Men").</param>
        /// <param name="theEvent">The event, used for the person's age on race day.</param>
        /// <param name="options">The award options.</param>
        /// <param name="customAgeGroups">The custom age groups.</param>
        /// <param name="distResultsDict">The award groups for the result's distance.</param>
        private static void AddToCustomGroups(
            TimeResult result,
            string gender,
            Event theEvent,
            AwardOptions options,
            IEnumerable<AgeGroup> customAgeGroups,
            Dictionary<string, List<TimeResult>> distResultsDict
            )
        {
            int age = result.Age(theEvent.Date);
            foreach (AgeGroup group in customAgeGroups)
            {
                if (age < group.StartAge || age > group.EndAge) continue;
                string customGroup = $"{gender} {group.PrettyName()}";
                if (!distResultsDict.TryGetValue(customGroup, out List<TimeResult>? customResults))
                {
                    customResults = [];
                    distResultsDict[customGroup] = customResults;
                }
                // only add to the results if we're under the number of results we can print
                if (customResults.Count < options.NumCustom)
                {
                    customResults.Add(result);
                }
            }
        }

        /// <summary>
        /// Orders award groups: overall (gender only) groups first, then by gender, then by age group with "Under" first and "Over" last.
        /// </summary>
        internal static int CompareGroups(string group1, string group2)
        {
            if ("Overall" == group1)
            {
                return -1;
            }
            if ("Overall" == group2)
            {
                return 1;
            }
            string[] firstSplit1 = group1.Split(' ');
            string[] firstSplit2 = group2.Split(' ');
            if (firstSplit1.Length < 2 || firstSplit2.Length < 2)
            {
                return string.Compare(group1, group2, StringComparison.Ordinal);
            }
            // if genders are not equal, sort by gender
            if (firstSplit1[0] != firstSplit2[0])
            {
                return string.Compare(firstSplit1[0], firstSplit2[0], StringComparison.Ordinal);
            }
            if (firstSplit1[1].Equals("Under", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }
            if (firstSplit2[1].Equals("Under", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (firstSplit1[1].Equals("Over", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (firstSplit2[1].Equals("Over", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }
            string[] secondSplit1 = firstSplit1[1].Split('-');
            string[] secondSplit2 = firstSplit2[1].Split('-');
            if (secondSplit1.Length < 2 || secondSplit2.Length < 2)
            {
                return string.Compare(firstSplit1[1], firstSplit2[1], StringComparison.Ordinal);
            }
            bool sOneOkay = int.TryParse(secondSplit1[0], out int start1);
            bool sTwoOkay = int.TryParse(secondSplit2[0], out int start2);
            if (!sOneOkay || !sTwoOkay)
            {
                return string.Compare(firstSplit1[1], firstSplit2[1], StringComparison.Ordinal);
            }
            return start1.CompareTo(start2);
        }
    }

    /// <summary>
    /// Which awards to print and how many people get each one.
    /// </summary>
    internal class AwardOptions
    {
        public bool PrintOverall { get; init; } = true;
        public int NumOverall { get; init; } = 3;
        // Exclude overall winners from age group awards.
        public bool ExcludeOverallAg { get; init; }
        // Exclude overall winners from custom group awards.
        public bool ExcludeOverallCustom { get; init; }
        public bool PrintAgeGroups { get; init; } = true;
        public int NumAgeGroups { get; init; } = 3;
        // Exclude winners in the Age Groups sections from custom sections.
        public bool ExcludeAgeGroupsCustom { get; init; }
        public bool PrintCustom { get; init; } = true;
        public int NumCustom { get; init; } = 3;
    }
}
