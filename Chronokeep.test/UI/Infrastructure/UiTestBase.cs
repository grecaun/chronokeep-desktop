using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Chronokeep.Objects;
using Chronokeep.Test.Database;
using Chronokeep.UI.Util;

namespace Chronokeep.Test.UI.Infrastructure
{
    /// <summary>
    /// Base for headless UI tests: a fresh SQLite database (from <see cref="DatabaseTestBase"/>),
    /// a <see cref="FakeMainWindow"/>, and helpers to host, find, click, and wait on controls.
    /// Every window a test opens is closed on dispose.
    /// </summary>
    public abstract class UiTestBase : DatabaseTestBase
    {
        protected FakeMainWindow MainWindow { get; } = new();
        private readonly List<Window> openWindows = [];

        protected UiTestBase()
        {
            // MainWindow writes the default app settings at startup; pages read them unguarded.
            Constants.Settings.SetupSettings(Db);
        }

        /// <summary>Shows the fake main window so it can own dialogs a test opens.</summary>
        protected void ShowMainWindow() => Show(MainWindow);

        /// <summary>The window the fake main window was asked to track, of the given type.</summary>
        protected T AddedWindow<T>() where T : Window
        {
            T? window = MainWindow.AddedWindows.OfType<T>().LastOrDefault();
            Assert.True(window != null, $"No {typeof(T).Name} was opened.");
            openWindows.Add(window!);
            return window!;
        }

        /// <summary>Seeds a current event with one distance and one participant, the minimum most pages need.</summary>
        internal (Event Event, Distance Distance) SeedEvent(string name = "Test Race")
        {
            Event theEvent = AddEvent(name, makeCurrent: true);
            Distance distance = AddDistance(theEvent.Identifier);
            AddParticipant(theEvent.Identifier, distance, "100");
            return (Db.GetCurrentEvent()!, distance);
        }

        /// <summary>Puts a control in a shown window so it gets a visual tree, templates, and layout.</summary>
        protected Window Host(Control content)
        {
            Window window = new() { Width = 1280, Height = 900, Content = content };
            return Show(window);
        }

        /// <summary>Shows a window, tracks it for cleanup, and lets pending UI work run.</summary>
        protected T Show<T>(T window) where T : Window
        {
            openWindows.Add(window);
            window.Show();
            Pump();
            return window;
        }

        /// <summary>Runs all queued dispatcher work (posted callbacks, layout, bindings).</summary>
        protected static void Pump() => Dispatcher.UIThread.RunJobs();

        /// <summary>
        /// Pumps the dispatcher until <paramref name="condition"/> holds. Pages load data on
        /// background tasks and post back, so a single <see cref="Pump"/> is not enough.
        /// </summary>
        protected static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000, string? because = null)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (true)
            {
                Pump();
                if (condition())
                {
                    return;
                }
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail($"Timed out after {timeoutMs} ms waiting for: {because ?? "condition"}");
                }
                await Task.Delay(20);
            }
        }

        /// <summary>Lets background loads finish even when there is nothing specific to wait for.</summary>
        protected static async Task Settle(int ms = 300)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < end)
            {
                Pump();
                await Task.Delay(20);
            }
            Pump();
        }

        /// <summary>
        /// Finds a named control in the control's name scope, failing the test if it is missing.
        /// Controls a test needs get a Name in the app's XAML; there are no find-by-text helpers.
        /// </summary>
        protected static T Find<T>(Control root, string name) where T : Control
        {
            T? found = root.FindControl<T>(name);
            Assert.True(found != null, $"Control '{name}' of type {typeof(T).Name} not found in {root.GetType().Name}.");
            return found!;
        }

        /// <summary>Raises Click the same way a user press does, running the page's handler.</summary>
        protected static void Click(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
        }

        /// <summary>Text of a ContentControl's content (buttons, labels) as shown.</summary>
        protected static string? ContentText(ContentControl control) => control.Content?.ToString();

        /// <summary>
        /// Lets the app's message and confirmation boxes open over the fake main window, where tests can
        /// read and answer them. Without this they have no window to open over and never appear.
        /// </summary>
        protected void AllowDialogs()
        {
            ShowMainWindow();
            global::Chronokeep.UI.MainWindow.MWindow = MainWindow;
        }

        /// <summary>The message boxes open over the main window, oldest first.</summary>
        protected List<DialogBox> OpenDialogs() =>
            [.. MainWindow.OwnedWindows.OfType<DialogBox>().Where(d => d.IsVisible)];

        /// <summary>Waits for a message box whose text contains the given words and returns it.</summary>
        protected async Task<DialogBox> WaitForDialog(string containing)
        {
            await WaitUntil(() => OpenDialogs().Any(d => DialogText(d).Contains(containing, StringComparison.OrdinalIgnoreCase)),
                because: $"a message saying '{containing}' (open: {string.Join(" | ", OpenDialogs().Select(DialogText))})");
            return OpenDialogs().Last(d => DialogText(d).Contains(containing, StringComparison.OrdinalIgnoreCase));
        }

        protected static string DialogText(DialogBox dialog) => Find<TextBlock>(dialog, "MessageBox").Text ?? "";

        /// <summary>Presses Yes (the left button) or No (the right button) on a confirmation.</summary>
        protected static void Answer(DialogBox dialog, bool yes) => Click(Find<Button>(dialog, yes ? "LeftButton" : "RightButton"));

        public override void Dispose()
        {
            foreach (DialogBox dialog in MainWindow.OwnedWindows.OfType<DialogBox>().ToList())
            {
                dialog.Close();
            }
            if (global::Chronokeep.UI.MainWindow.MWindow == MainWindow)
            {
                global::Chronokeep.UI.MainWindow.MWindow = null;
            }
            foreach (Window window in openWindows)
            {
                try
                {
                    window.Close();
                }
                catch (Exception)
                {
                    // A window that closed itself or failed to open has nothing left to clean up.
                }
            }
            Pump();
            base.Dispose();
        }
    }
}
