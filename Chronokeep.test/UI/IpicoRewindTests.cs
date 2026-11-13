using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.Timing.Windows;
using Chronokeep.UI.Util;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Readers = Chronokeep.Constants.Readers;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// A stand-in for an Ipico reader's replay port: once something connects and asks, it sends the
    /// given lines and hangs up, as the reader does when its replay is done.
    /// </summary>
    internal sealed class FakeIpicoReplay : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly Task served;

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public bool Connected { get; private set; }

        public FakeIpicoReplay(params string[] lines)
        {
            listener.Start();
            served = Task.Run(async () =>
            {
                try
                {
                    using TcpClient client = await listener.AcceptTcpClientAsync();
                    Connected = true;
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[16];
                    _ = await stream.ReadAsync(buffer);
                    byte[] replay = Encoding.ASCII.GetBytes(string.Concat(lines.Select(l => l + "\n")));
                    await stream.WriteAsync(replay);
                }
                catch (Exception)
                {
                    // The test ended before anything connected.
                }
            });
        }

        public void Dispose()
        {
            listener.Stop();
            served.Wait(TimeSpan.FromSeconds(2));
        }
    }

    /// <summary>Rewinding an Ipico reader: it's asked to replay from a time, and the replay is read from a second port.</summary>
    public class IpicoRewindTests : ReaderUiTestBase
    {
        // aa | reader | tag 12 | I | Q | yyMMdd | HHmmss | hundredths (hex) | checksum
        private static string Read(string tag, string time = "260615080000") => $"aa01{tag}0000{time}0aff";

        public IpicoRewindTests() => AllowDialogs();

        private (TimingSystem System, IpicoInterface Ipico) Ipico(int replayPort)
        {
            TimingSystem system = new("127.0.0.1", Readers.SYSTEM_IPICO) { Status = SYSTEM_STATUS.CONNECTED };
            IpicoInterface ipico = new(Db, TimingConstants.LOCATION_FINISH, Readers.SYSTEM_IPICO, MainWindow);
            ipico.SetSettingsSocket(Pair.Client);
            ipico.SetRewindAddress("127.0.0.1", replayPort);
            system.SystemInterface = ipico;
            return (system, ipico);
        }

        private List<ChipRead> Reads() => Db.GetChipReadsSafemode(Db.GetCurrentEvent()!.Identifier);

        [AvaloniaFact]
        public void Replay_reads_are_saved_and_timing_is_woken()
        {
            using FakeIpicoReplay replay = new(Read("058000123456"), Read("058000654321", "260615080130"));
            (_, IpicoInterface ipico) = Ipico(replay.Port);

            ipico.GetRewind();

            Assert.Equal(["058000123456", "058000654321"], Reads().Select(r => r.ChipNumber).Order());
            Assert.All(Reads(), r => Assert.Equal(TimingConstants.LOCATION_FINISH, r.LocationId));
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void An_empty_replay_leaves_timing_alone()
        {
            using FakeIpicoReplay replay = new();
            (_, IpicoInterface ipico) = Ipico(replay.Port);

            ipico.GetRewind();

            Assert.Empty(Reads());
            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void An_unreachable_replay_port_gives_up()
        {
            FakeIpicoReplay closed = new();
            int port = closed.Port;
            closed.Dispose();
            (_, IpicoInterface ipico) = Ipico(port);

            ipico.GetRewind();

            Assert.Empty(Reads());
        }

        [AvaloniaTheory]
        [InlineData("Reader1", "ttyS0")]
        [InlineData("Reader2", "ttyS1")]
        public async Task Confirmed_rewind_replays_the_chosen_reader_from_the_start_time(string readerChoice, string file)
        {
            using FakeIpicoReplay replay = new(Read("058000123456"));
            (TimingSystem system, _) = Ipico(replay.Port);
            RewindWindow window = Show(new RewindWindow(system, Parent));
            Find<CalendarDatePicker>(window, "FromDate").SelectedDate = new DateTime(2026, 6, 15);
            Find<MaskedTextBox>(window, "FromTime").Text = "07:30:15";
            Find<RadioButton>(window, readerChoice).IsChecked = true;

            Click(Find<Button>(window, "Rewind"));
            Answer(await WaitForDialog("can take up to 3 minutes"), true);

            Assert.Equal($"replay_start file.{file} port.10300 datetime.260615073015\n", Pair.Line());
            await WaitUntil(() => Reads().Count == 1, because: "the replayed read to be saved");
            await WaitUntil(() => !window.IsVisible, because: "the window to close once the rewind is done");
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public async Task Rewind_window_stays_open_and_busy_until_the_replay_ends()
        {
            using FakeIpicoReplay replay = new(Read("058000123456"));
            (TimingSystem system, _) = Ipico(replay.Port);
            RewindWindow window = Show(new RewindWindow(system, Parent));

            Click(Find<Button>(window, "Rewind"));
            DialogBox question = await WaitForDialog("can take up to 3 minutes");
            Assert.True(window.IsVisible, "The rewind window closed before the rewind was confirmed.");
            Answer(question, true);

            await WaitUntil(() => !window.IsVisible, because: "the window to close once the rewind is done");
            Assert.True(replay.Connected);
        }

        [AvaloniaFact]
        public async Task Declined_rewind_sends_nothing_and_keeps_the_window()
        {
            using FakeIpicoReplay replay = new(Read("058000123456"));
            (TimingSystem system, _) = Ipico(replay.Port);
            RewindWindow window = Show(new RewindWindow(system, Parent));

            Click(Find<Button>(window, "Rewind"));
            Answer(await WaitForDialog("can take up to 3 minutes"), false);
            await Settle();

            Assert.True(Pair.NothingSent());
            Assert.False(replay.Connected);
            Assert.True(window.IsVisible);
        }
    }
}
