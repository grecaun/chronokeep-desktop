using System;
using Avalonia.Controls;
using Avalonia.Input;
using Chronokeep.Objects;
using System.Threading;
using System.Threading.Tasks;
using Chronokeep.Helpers;

namespace Chronokeep.UI.UhfRfidReader;

public partial class ChipPersonWindow : ChronokeepWindow
{
    private readonly ChipReaderWindow readerWindow;
    private readonly string eventDate;
    private readonly object locker = new();

    public ChipPersonWindow(ChipReaderWindow reader, string eventDate)
    {
        this.readerWindow = reader;
        this.eventDate = eventDate;
        InitializeComponent();
    }

    public async void UpdateInfo(Participant? person, string chip)
    {
        try
        {
            await Task.Run(() =>
            {
                lock (locker)
                {
                    Monitor.Pulse(locker);
                }
                Thread.Sleep(100);
            });
            if (person != null)
            {
                Bib.Text = "Bib: " + person.EventSpecific.Bib;
                Chip.Text = "Chip: " + chip;
                PersonName.Text = $"{person.FirstName} {person.LastName}";
                AgeGender.Text = $"{person.Age(eventDate)} {person.Gender}";
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
                lock (locker)
                {
                    Monitor.Wait(locker, 5000);
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
        catch (Exception)
        {
            Log.D("UI.Timing.UhfRfidReader.ChipPersonWindow", "Error updating info.");
        }
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

    protected override void SetMaximizeIcon()
    {      
    }

    protected override void Maximize()
    {
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
    }
}