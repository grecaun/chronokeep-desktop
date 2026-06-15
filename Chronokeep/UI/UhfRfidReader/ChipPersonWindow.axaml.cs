using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chronokeep.Objects;
using System.Threading;
using System.Threading.Tasks;

namespace Chronokeep.UI.UhfRfidReader;

public partial class ChipPersonWindow : Window
{
    private readonly ChipReaderWindow readerWindow;
    private readonly string eventDate;
    private readonly object _locker = new();

    public ChipPersonWindow(ChipReaderWindow reader, string eventDate)
    {
        this.readerWindow = reader;
        this.eventDate = eventDate;
        InitializeComponent();
    }

    public async void UpdateInfo(Participant person, string chip)
    {
        await Task.Run(() =>
        {
            lock (_locker)
            {
                Monitor.Pulse(_locker);
            }
            Thread.Sleep(100);
        });
        if (person != null)
        {
            Bib.Text = "Bib: " + person.EventSpecific.Bib;
            Chip.Text = "Chip: " + chip;
            PersonName.Text = string.Format("{0} {1}", person.FirstName, person.LastName);
            AgeGender.Text = string.Format("{0} {1}", person.Age(eventDate), person.Gender);
            Distance.Text = "" + person.EventSpecific.DistanceName;
            Unknown.Text = "";
            Unknown.IsVisible = false;
            InfoHolder.IsVisible = true;
        }
        else
        {
            Bib.Text = "";
            Chip.Text = "";
            PersonName.Text = "";
            AgeGender.Text = "";
            Distance.Text = "";
            Unknown.Text = "Information not found.";
            Unknown.IsVisible = true;
            InfoHolder.IsVisible = false;
        }
        await Task.Run(() =>
        {
            lock (_locker)
            {
                Monitor.Wait(_locker, 5000);
            }
        });
        Bib.Text = "";
        PersonName.Text = "";
        AgeGender.Text = "";
        Distance.Text = "";
        Unknown.Text = "";
        Unknown.IsVisible = false;
        InfoHolder.IsVisible = false;
    }


    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        readerWindow.PersonWindowClosing();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}