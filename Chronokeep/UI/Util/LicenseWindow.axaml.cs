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
using System.IO;
using System.Reflection;

namespace Chronokeep.UI.Util;

public partial class LicenseWindow : ChronokeepWindow
{
    public LicenseWindow()
    {
        InitializeComponent();
        ChronokeepInitialize();
        string licenseText;
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chronokeep.agpl-3.0.txt")!)
        {
            using StreamReader reader = new(stream);
            licenseText = reader.ReadToEnd();
        }
        MessageBox.Text = licenseText;
        RightButton.Click += (_, _) =>
        {
            Close();
        };
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
