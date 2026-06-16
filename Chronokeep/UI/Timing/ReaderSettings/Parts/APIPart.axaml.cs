using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects.ChronokeepPortal;
using Chronokeep.Timing.Interfaces;

namespace Chronokeep.UI.Timing.ReaderSettings.Parts;

public partial class ApiPart : UserControl
{
    private PortalApi api;
    private readonly ChronokeepInterface reader;

    public ApiPart(PortalApi api, ChronokeepInterface reader)
    {
        InitializeComponent();
        this.api = api;
        this.reader = reader;
        NameBox.Text = api.Nickname;
        KindBox.SelectedIndex = api.Kind switch
        {
            PortalApi.API_TYPE_CHRONOKEEP_REMOTE => 0,
            PortalApi.API_TYPE_CHRONOKEEP_REMOTE_SELF => 1,
            _ => 0,
        };
        TokenBox.Text = api.Token;
        UriBox.Text = api.Uri;
        PrivateUpdateUri();
    }

    public void UpdateApi(PortalApi iApi)
    {
        api = iApi;
        NameBox.Text = iApi.Nickname;
        KindBox.SelectedIndex = iApi.Kind switch
        {
            PortalApi.API_TYPE_CHRONOKEEP_REMOTE => 0,
            PortalApi.API_TYPE_CHRONOKEEP_REMOTE_SELF => 1,
            _ => 0,
        };
        TokenBox.Text = iApi.Token;
        UriBox.Text = iApi.Uri;
        PrivateUpdateUri();
    }

    private void PrivateUpdateUri()
    {
        switch (((ComboBoxItem)KindBox.SelectedItem!).Tag)
        {
            case PortalApi.API_TYPE_CHRONOKEEP_REMOTE:
                UriBox.IsVisible = false;
                UriBox.Text = PortalApi.API_URI_CHRONOKEEP_REMOTE;
                break;
            default:
                UriBox.IsVisible = true;
                UriBox.Text = api.Uri;
                break;
        }
    }

    private void KindBox_ValueChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Selected type changed.");
        PrivateUpdateUri();
    }
    private void DeleteApi(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Deleting api " + api.Id);
        reader.SendDeleteApi(api);
    }

    private void SaveApi(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Saving api " + api.Id);
        api.Nickname = NameBox.Text!.Trim();
        api.Token = TokenBox.Text!.Trim();
        api.Uri = UriBox.Text!.Trim();
        api.Kind = (string)((ComboBoxItem)KindBox.SelectedItem!).Tag!;
        reader.SendSaveApi(api);
    }
}