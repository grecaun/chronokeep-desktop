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
using Avalonia.Interactivity;
using Chronokeep.IO.HtmlTemplates;

namespace Chronokeep.UI.Util;

public partial class PrintSettingsWindow : ChronokeepWindow
{
    public PrintSettingsWindow()
    {
        InitializeComponent();
        ChronokeepInitialize();
        TopMarginBox.Text = HtmlPrintableTemplate.TopMargin.ToString();
        LeftMarginBox.Text = HtmlPrintableTemplate.LeftMargin.ToString();
        ScalingBox.Text = HtmlPrintableTemplate.Scale.ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Margins can't be negative and the page must have a size, so other values are ignored.
        if (int.TryParse(TopMarginBox.Text, out int topMargin) && topMargin >= 0)
        {
            HtmlPrintableTemplate.TopMargin = topMargin;
        }
        if (int.TryParse(LeftMarginBox.Text, out int leftMargin) && leftMargin >= 0)
        {
            HtmlPrintableTemplate.LeftMargin = leftMargin;
        }
        if (double.TryParse(ScalingBox.Text, out double scaling) && scaling > 0)
        {
            HtmlPrintableTemplate.Scale = scaling;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
