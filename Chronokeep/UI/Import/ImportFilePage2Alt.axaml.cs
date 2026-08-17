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

using Avalonia.Controls;
using Chronokeep.Objects;
using Chronokeep.UI.Parts;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.Import;

public partial class ImportFilePage2Alt : UserControl
{
    public ImportFilePage2Alt(string[] fileDistances, List<Distance> dbDistances, bool noDistances)
    {
        InitializeComponent();
        if (noDistances)
        {
            DistanceListBox.Items.Add(new DistanceAlternatePart("Default Distance", dbDistances));
        }
        else
        {
            foreach (string distance in fileDistances)
            {
                DistanceListBox.Items.Add(new DistanceAlternatePart(distance, dbDistances));
            }
        }
    }

    public List<ImportDistance> GetDistances()
    {
        List<ImportDistance> output = [];
        output.AddRange(DistanceListBox.Items.Cast<DistanceAlternatePart>().Select(distanceItem => new ImportDistance() { NameFromFile = distanceItem.NameFromFile(), DistanceId = distanceItem.DistanceId() }));
        return output;
    }

    public class ImportDistance
    {
        public string NameFromFile { get; init; } = "";
        public int DistanceId { get; init; }
    }
}
