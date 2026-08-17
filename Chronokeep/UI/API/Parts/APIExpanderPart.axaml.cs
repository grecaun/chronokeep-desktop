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
using Chronokeep.Database;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using System.Collections.Generic;

namespace Chronokeep.UI.API.Parts;

public partial class ApiExpanderPart : UserControl
{
    public ApiExpanderPart(
        ApiObject api,
        List<RemoteReader> readers,
        Dictionary<(int, string), RemoteReader> savedReaders,
        IdbInterface database,
        IMainWindow mainWindow)
    {
        InitializeComponent();
        ApiNameBlock.Text = api.Nickname;
        foreach (RemoteReader reader in readers)
        {
            reader.ApiiDentifier = api.Identifier;
            if (savedReaders.TryGetValue((reader.ApiiDentifier, reader.Name), out RemoteReader? rReader))
            {
                reader.LocationId = rReader.LocationId;
            }
            ReaderListView.Items.Add(new ReaderListItem(reader, api, savedReaders, database, mainWindow));
        }
    }

    public Dictionary<RemoteReader, bool> GetAutoDownloadDictionary()
    {
        Dictionary<RemoteReader, bool> output = [];
        foreach (ReaderListItem? item in ReaderListView.Items)
        {
            output[item!.GetUpdatedReader()] = item.AutoDownloadReads();
        }
        return output;
    }
}
