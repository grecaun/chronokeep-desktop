using Avalonia.Controls;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;

namespace Chronokeep.UI.API.Windows;

public partial class EditApiWindow : ChronokeepWindow
{
    private readonly IMainWindow? window;

    // Variables relating to information we're collecting.
    private readonly ApiObject? api;
    private readonly string? slug, year;

    private EditApiWindow(IMainWindow window, IDBInterface database)
    {
        InitializeComponent();
        this.window = window;
        MinHeight = 100;
        MinWidth = 300;
        Width = 330;
        Event? theEvent1 = database.GetCurrentEvent();
        // Get API to upload.
        if (theEvent1 == null || theEvent1.Identifier < 1 || theEvent1.ApiId < 0 || theEvent1.ApiEventId.Length < 1)
        {
            Log.E("UI.API.APIWindow", "event not found or no apis set up");
            EditApiFrame.Content = new Pages.EditApiErrorPage(this, true);
            return;
        }
        api = database.GetAPI(theEvent1.ApiId);
        string[] eventIds = theEvent1.ApiEventId.Split(',');
        if (eventIds.Length != 2)
        {
            return;
        }
        slug = eventIds[0];
        year = eventIds[1];
        EditApiFrame.Content = new Pages.EditApiPage1(this, database);
    }

    public void NetworkUpdateResults()
    {
        window?.NetworkUpdateResults();
    }

    public static EditApiWindow NewWindow(IMainWindow window, IDBInterface database)
    {
        return new EditApiWindow(window, database);
    }

    public void GotoEditEvent()
    {
        EditApiFrame.Content = new Pages.EditEventPage(this, api!, slug!);
    }

    public void GotoEditYear()
    {
        EditApiFrame.Content = new Pages.EditYearPage(this, api!, slug!, year!);
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize(this);
    }

    protected override void Maximize()
    {
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
    }
}