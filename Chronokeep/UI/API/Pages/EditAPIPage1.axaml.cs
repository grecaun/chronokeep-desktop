using Avalonia.Controls;
using Chronokeep.Database;
using Chronokeep.Objects;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.Util;

namespace Chronokeep.UI.API.Pages;

public partial class EditApiPage1 : UserControl
{
    private readonly EditApiWindow window;
    private readonly IDBInterface database;

    public EditApiPage1(EditApiWindow window, IDBInterface database)
    {
        InitializeComponent();
        this.window = window;
        this.database = database;
    }

    private void Unlink_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Event theEvent = database.GetCurrentEvent()!;
        // Check if we've actually got a linked event, then unlink it.
        if (theEvent.ApiId != Constants.APIConstants.NULL_ID && theEvent.ApiEventId != Constants.APIConstants.NULL_EVENT_ID)
        {
            theEvent.ApiId = Constants.APIConstants.NULL_ID;
            theEvent.ApiEventId = Constants.APIConstants.NULL_EVENT_ID;
            database.UpdateEvent(theEvent);
            window.NetworkUpdateResults();
        }
        else
        {
            DialogBox.Show("Unable to Link Event");
        }
        window.Close();
    }

    private void Edit_Event_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        window.GotoEditEvent();
    }

    private void Edit_Year_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        window.GotoEditYear();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        window.Close();
    }
}