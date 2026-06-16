using Avalonia.Controls;

namespace Chronokeep.UI.Export.Parts;

public partial class HeaderPart : UserControl
{
    public string NameValue => HeaderName.Text!;

    public HeaderPart(string name)
    {
        InitializeComponent();
        HeaderName.Text = name;
    }
}