using Avalonia.Controls;
using Chronokeep.UI.API.Windows;

namespace Chronokeep.UI.API.Pages;

public partial class EditApiErrorPage : UserControl
{
    private readonly EditApiWindow window;

    public EditApiErrorPage(EditApiWindow window, bool noApi)
    {
        InitializeComponent();
        this.window = window;
        if (noApi)
        {
            ErrorLabel.Text = "Unable to find linked api/event.";
        }
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        window.Close();
    }
}