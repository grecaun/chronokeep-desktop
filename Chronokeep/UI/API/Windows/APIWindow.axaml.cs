using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Util;
using System.Collections.Generic;

namespace Chronokeep.UI.API.Windows;

public partial class APIWindow : Window
{
    private readonly IMainWindow window;
    private readonly IDBInterface database;
    private readonly Event? theEvent;

    // Variables relating to information we're collecting.
    private APIObject? api;
    private string slug = "", year = "";

    public APIWindow(IMainWindow window, IDBInterface database)
    {
        InitializeComponent();
        this.window = window;
        this.database = database;
        this.MinHeight = 100;
        this.MinWidth = 300;
        this.Width = 330;
        theEvent = database.GetCurrentEvent();
        List<APIObject> apis = database.GetAllAPI();
        apis.RemoveAll(x => !Constants.APIConstants.API_RESULTS[x.Type]);
        if (theEvent == null || theEvent.Identifier < 1 || apis.Count < 1)
        {
            Log.E("UI.API.APIWindow", "event not found or no apis set up");
            APIFrame.Content = new APIErrorPage(this, apis.Count < 1);
        }
        else
        {
            APIFrame.Content = new APIPage1(this, database);
        }
    }

    public static APIWindow NewWindow(IMainWindow window, IDBInterface database)
    {
        return new(window, database);
    }

    public void GotoPage2(APIObject api)
    {
        this.api = api;
        database.SetAppSetting(Constants.Settings.LAST_USED_API_ID, api.Identifier.ToString());
        APIFrame.Content = new APIPage2(this, database, api, theEvent!);
    }

    public void GotoPage3(string slug)
    {
        this.slug = slug;
        APIFrame.Content = new APIPage3(this, api!, theEvent!, slug);
    }

    public void Finish(string year)
    {
        this.year = year;
        if (api!.Identifier > 0 && this.slug != "" && this.year != "")
        {
            theEvent!.API_ID = api.Identifier;
            theEvent.API_Event_ID = this.slug + "," + this.year;
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
        window?.WindowFinalize(this);
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        MaximizeIcon?.IsVisible = WindowState == WindowState.Normal;
        UnMaximizeIcon?.IsVisible = WindowState == WindowState.Maximized;
    }
}