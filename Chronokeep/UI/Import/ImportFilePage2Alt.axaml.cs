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