/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using Avalonia.Controls;
using Chronokeep.Helpers;

namespace Chronokeep.UI.Util;

public partial class DialogBox : ChronokeepWindow
{
    public delegate void ClickDelegate();
    private string copyText = "";

    private bool Exiting = false;

    public DialogBox(string message, string leftButtonContent, string rightButtonContent, bool showLeftButton, ClickDelegate leftClick)
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

    public DialogBox(string message, string rightButtonContent, ClickDelegate rightClick)
    {
        InitializeComponent();
        ChronokeepInitialize();
        MessageBox.Text = message;
        RightButton.Content = rightButtonContent;
        LeftButton.IsVisible = false;
        RightButton.Click += (_, _) =>
        {
            Close();
        };
        Topmost = true;
        Closing += (object? _, WindowClosingEventArgs e) => {
            if (!Exiting) {
                e.Cancel = true;
                Exiting = true;
                Close();
                rightClick();
            }
        };
    }

    public static void Show(string message)
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
            output.Show(MainWindow.MWindow!);
        }
        catch (Exception)
        {
            Log.D("UI.Util.DialogBox", "Error trying to show dialog box.");
        }
    }

    public static void Show(string message, string rightButtonContent, ClickDelegate rightClick)
    {
        try
        {
            DialogBox output = new(
                message,
                rightButtonContent,
                rightClick
            );
            output.Show(MainWindow.MWindow!);
        }
        catch (Exception)
        {
            Log.D("UI.Util.DialogBox", "Error trying to show dialog box.");
        }
    }

    public static async void AsyncShow(string message)
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
            Log.D("UI.Util.DialogBox", "Error trying to show dialog box.");
        }
    }

    public static async void AsyncShow(string message, string leftButtonContent, string rightButtonContent, ClickDelegate leftClick)
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

    public static async void AsyncShow(string message, string copyText)
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

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
