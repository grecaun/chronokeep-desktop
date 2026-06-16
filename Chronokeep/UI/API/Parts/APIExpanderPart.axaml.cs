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