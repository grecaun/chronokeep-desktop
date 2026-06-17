using Avalonia.Controls;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Util;
using System.Collections.Generic;
using Chronokeep.Constants;

namespace Chronokeep.UI.API.Windows;

public partial class ApiWindow : ChronokeepWindow
{
    private readonly IMainWindow window;
    private readonly IdbInterface database;
    private readonly Event? theEvent;

    // Variables relating to information we're collecting.
    private ApiObject? api;
    private string slug = "", year = "";

    private ApiWindow(IMainWindow window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        List<ApiObject> apis = database.GetAllApi();
        apis.RemoveAll(x => !ApiConstants.API_RESULTS[x.Type]);
        if (theEvent == null || theEvent.Identifier < 1 || apis.Count < 1)
        {
            Log.E("UI.API.APIWindow", "event not found or no apis set up");
            ApiFrame.Content = new Pages.ApiErrorPage(this, apis.Count < 1);
        }
        else
        {
            ApiFrame.Content = new Pages.ApiPage1(this, database);
        }
    }

    public static ApiWindow NewWindow(IMainWindow window, IdbInterface database)
    {
        return new ApiWindow(window, database);
    }

    public void GotoPage2(ApiObject iApi)
    {
        api = iApi;
        database.SetAppSetting(Settings.LAST_USED_API_ID, iApi.Identifier.ToString());
        ApiFrame.Content = new Pages.ApiPage2(this, database, iApi, theEvent!);
    }

    public void GotoPage3(string iSlug)
    {
        slug = iSlug;
        ApiFrame.Content = new Pages.ApiPage3(this, api!, theEvent!, iSlug);
    }

    public void Finish(string iYear)
    {
        year = iYear;
        if (api!.Identifier > 0 && slug != "" && year != "")
        {
            theEvent!.ApiId = api.Identifier;
            theEvent.ApiEventId = slug + "," + year;
            database.UpdateEvent(theEvent);
            window.NetworkUpdateResults();
        }
        else
        {
            DialogBox.Show("One or more values retrieved is invalid.");
            return;
        }
        Close();
    }


    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}