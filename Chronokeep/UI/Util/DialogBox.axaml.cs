using System;
using Avalonia.Controls;
using Chronokeep.Helpers;

namespace Chronokeep.UI.Util;

public partial class DialogBox : ChronokeepWindow
{
    public delegate void LeftClickDelegate();
    private string copyText = "";

    public DialogBox(string message, string leftButtonContent, string rightButtonContent, bool showLeftButton, LeftClickDelegate leftClick)
    {
        InitializeComponent();
        ChronokeepInitialize();
        MessageBox.Text = message;
        LeftButton.Content = leftButtonContent;
        RightButton.Content = rightButtonContent;
        LeftButton.IsVisible = showLeftButton;
        LeftButton.Click += (_, _) =>
        {
            Close();
            leftClick();
        };
        RightButton.Click += (_, _) =>
        {
            Close();
        };
        Topmost = true;
    }

    public static async void Show(string message)
    {
        try
        {
            DialogBox output = new(
                message,
                "",
                "OK",
                false,
                () => { }
            );
            await output.ShowDialog(MainWindow.MWindow!);
        }
        catch (Exception)
        {
            Log.D("UI.Util.DialogBox","Error trying to show dialog box.");
        }
    }

    public static async void Show(string message, string leftButtonContent, string rightButtonContent, LeftClickDelegate leftClick)
    {
        try
        {
            DialogBox output = new(
                message,
                leftButtonContent,
                rightButtonContent,
                true,
                leftClick
            );
            await output.ShowDialog(MainWindow.MWindow!);
        }
        catch (Exception)
        {
            Log.D("UI.Util.DialogBox","Error trying to show dialog box.");
        }
    }

    public static async void Show(string message, string copyText)
    {
        try
        {
            DialogBox output = new(
                message,
                "",
                "OK",
                false,
                () => { }
            )
            {
                copyText = copyText,
                CopyBox =
                {
                    Text = copyText,
                    IsVisible = true
                },
                Width = 500.0
            };
            await output.ShowDialog(MainWindow.MWindow!);
        }
        catch (Exception)
        {
            Log.D("UI.Util.DialogBox","Error trying to show dialog box.");
        }
    }

    private void CopyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CopyBox.Text = copyText;
    }
}