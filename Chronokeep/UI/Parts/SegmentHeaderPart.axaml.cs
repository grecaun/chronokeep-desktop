using Avalonia.Controls;
using Chronokeep.Objects;

namespace Chronokeep.UI.Parts;

public partial class SegmentHeaderPart : UserControl
{
    public SegmentHeaderPart(Event theEvent)
    {
        InitializeComponent();
        if (Constants.Timing.EVENT_TYPE_DISTANCE == theEvent.EventType) return;
        Occurrence.Height = 0;
        Occurrence.IsVisible = false;
    }
}