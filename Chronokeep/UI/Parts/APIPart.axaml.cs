using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.UI.MainPages.Dashboard;

namespace Chronokeep.UI.Parts;

public partial class ApiPart : UserControl
{
    private readonly ApiPage page;
    public readonly ApiObject TheApi;

    public ApiPart(ApiPage page, ApiObject api)
    {
        InitializeComponent();
        TheApi = api;
        this.page = page;
        ApiNickname.Text = api.Nickname;
        ComboBoxItem? selected = null;
        foreach (string uid in Constants.ApiConstants.API_TYPE_NAMES.Keys)
        {
            ComboBoxItem newItem = new()
            {
                Content = Constants.ApiConstants.API_TYPE_NAMES[uid],
                Tag = uid,
                IsSelected = TheApi.Type.Equals(uid),
            };
            if (TheApi.Type.Equals(uid))
            {
                selected = newItem;
            }
            ApiType.Items.Add(newItem);
        }
        ApiUrl.Text = api.Url;
        ApiUrl.IsEnabled = Constants.ApiConstants.API_SELF_HOSTED[TheApi.Type];
        ApiToken.Text = api.AuthToken;
        ApiWebUrl.Text = api.WebUrl;
        if (selected != null)
        {
            ApiType.SelectedItem = selected;
        }
        else
        {
            ApiType.SelectedIndex = 0;
        }
    }

    public void UpdateResultsApi()
    {
        Log.D("UI.MainPages.APIPage", "Updating api.");
        TheApi.Nickname = ApiNickname.Text!;
        TheApi.Url = ApiUrl.Text!;
        if (!TheApi.Url.EndsWith('/'))
        {
            TheApi.Url += "/";
        }
        TheApi.AuthToken = ApiToken.Text!;
        TheApi.Type = (string)((ComboBoxItem)ApiType.SelectedItem!).Tag!;
        TheApi.WebUrl = ApiWebUrl.Text!;
        if (TheApi.WebUrl.Length > 0 && !TheApi.WebUrl.EndsWith('/'))
        {
            TheApi.WebUrl += "/";
        }
    }

    private void APIType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.D("UI.MainPages.APIPage", "Changing API Type!");
        // Ensure we've got something selected, and then change the URL if they've selected Chronokeep.
        if (ApiType.SelectedItem == null) return;
        string type = (string)((ComboBoxItem)ApiType.SelectedItem).Tag!;
        if (!Constants.ApiConstants.API_SELF_HOSTED[type])
        {
            TheApi.Url = Constants.ApiConstants.API_URL[type];
            ApiUrl.Text = TheApi.Url;
            ApiUrl.IsEnabled = false;
        }
        else
        {
            ApiUrl.IsEnabled = true;
        }
        if (Constants.ApiConstants.API_RESULTS[type])
        {
            ApiWebUrl.Text = TheApi.WebUrl;
            ApiWebUrl.IsEnabled = true;
        }
        else
        {
            ApiWebUrl.Text = "";
            ApiWebUrl.IsEnabled = false;
        }
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.APIPage", "Removing api.");
        page.RemoveApi(TheApi);
    }
}