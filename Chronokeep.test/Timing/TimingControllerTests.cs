using Chronokeep.Objects;
using Chronokeep.Timing;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Chronokeep.Test.Timing
{
    /// <summary>
    /// A stand-in RFID Ultra on 127.0.0.1 (loopback only, so there's no firewall prompt).
    /// </summary>
    internal sealed class FakeUltra : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly Task<TcpClient> accept;
        private TcpClient? client;

        public FakeUltra()
        {
            listener.Start();
            accept = listener.AcceptTcpClientAsync();
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        private TcpClient Client
        {
            get
            {
                if (client == null)
                {
                    Assert.True(accept.Wait(5000), "The controller never connected to the fake reader.");
                    client = accept.Result;
                    client.NoDelay = true;
                }
                return client;
            }
        }

        public void Send(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            Client.GetStream().Write(bytes, 0, bytes.Length);
            Client.GetStream().Flush();
        }

        /// <summary>Reads whatever the controller has sent so far (waits up to a second for something).</summary>
        public string Received()
        {
            var stream = Client.GetStream();
            stream.ReadTimeout = 1000;
            byte[] buf = new byte[1024];
            try
            {
                int n = stream.Read(buf, 0, buf.Length);
                return Encoding.ASCII.GetString(buf, 0, n);
            }
            catch (IOException)
            {
                return "";
            }
        }

        /// <summary>The reader drops the connection.</summary>
        public void Close() => Client.Close();

        public void Dispose()
        {
            try { client?.Close(); } catch { }
            try { listener.Stop(); } catch { }
        }
    }

    public class TimingControllerTests : ReaderTestBase
    {
        private readonly TimingController controller;
        private readonly List<FakeUltra> readers = [];

        public TimingControllerTests()
        {
            controller = new TimingController(Window, Db);
            // Clear the static new-reads flag left by anything before this test.
            TimingController.NewReadsExist();
        }

        private FakeUltra NewReader()
        {
            var r = new FakeUltra();
            readers.Add(r);
            return r;
        }

        private static TimingSystem System(int port)
            => new(0, "127.0.0.1", port, Location, Constants.Readers.SYSTEM_RFID);

        private TimingSystem Connect(FakeUltra reader)
        {
            var sys = System(reader.Port);
            controller.ConnectTimingSystem(sys);
            return sys;
        }

        private void StartRun()
        {
            controller.Start();
            Assert.True(TimingController.IsRunning(), "The controller loop never started.");
        }

        /// <summary>Waits for the loop to finish.</summary>
        private static bool StopRun(int ms = 8000) => WaitFor(() => !TimingController.IsRunning(), ms);

        private static bool WaitFor(Func<bool> condition, int ms = 5000)
        {
            var until = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < until)
            {
                if (condition()) return true;
                Thread.Sleep(25);
            }
            return condition();
        }

        private int CallCount(string name)
        {
            lock (FakeWindow.Calls)
            {
                return FakeWindow.Calls.Count(c => c == name);
            }
        }

        private static string ChipLine(string chip = "1000", long seconds = 1_000_000) => $"0,{chip},{seconds},250,1,-45,0,1,BOX1,0000000000000,0,{seconds}\n";

        public override void Dispose()
        {
            controller.Shutdown();
            StopRun();
            foreach (var r in readers) r.Dispose();
            base.Dispose();
        }

        [Fact]
        public void Connect_MarksTheSystemConnectedAndAsksForStatus()
        {
            var reader = NewReader();
            var sys = Connect(reader);

            Assert.Equal(SYSTEM_STATUS.CONNECTED, sys.Status);
            Assert.Same(sys, Assert.Single(controller.GetConnectedSystems()));
            Assert.Equal("?\n", reader.Received());
        }

        [Fact]
        public void Connect_NothingListening_IsDisconnected()
        {
            int port;
            using (var r = new FakeUltra()) port = r.Port;   // a port that was just free and now isn't listening

            var sys = System(port);
            controller.ConnectTimingSystem(sys);

            Assert.Equal(SYSTEM_STATUS.DISCONNECTED, sys.Status);
            Assert.Empty(controller.GetConnectedSystems());
        }

        [Fact]
        public void Run_ChipRead_IsSavedAndTheWorkerIsNotified()
        {
            var reader = NewReader();
            Connect(reader);
            StartRun();

            reader.Send(ChipLine("1000"));

            Assert.True(WaitFor(() => SavedReads().Count == 1), "The chip read was never saved.");
            Assert.Equal("1000", SavedReads()[0].ChipNumber);
            Assert.True(WaitFor(() => CallCount("NotifyTimingWorker") >= 1));
            Assert.True(TimingController.NewReadsExist());
            Assert.False(TimingController.NewReadsExist());   // reading the flag clears it
        }

        [Fact]
        public void Run_ReadSplitAcrossPackets_IsSavedOnce()
        {
            var reader = NewReader();
            Connect(reader);
            StartRun();

            string line = ChipLine("2000");
            reader.Send(line[..10]);
            Thread.Sleep(200);
            reader.Send(line[10..] + ChipLine("2001"));

            Assert.True(WaitFor(() => SavedReads().Count == 2));
            Assert.Equal(["2000", "2001"], SavedReads().Select(r => r.ChipNumber).OrderBy(c => c));
        }

        [Fact]
        public void Run_IgnoredChip_IsNotSaved()
        {
            IgnoreChip("3000");
            var reader = NewReader();
            Connect(reader);
            StartRun();

            reader.Send(ChipLine("3000") + ChipLine("3001"));

            Assert.True(WaitFor(() => SavedReads().Count == 1));
            Thread.Sleep(200);
            Assert.Equal("3001", Assert.Single(SavedReads()).ChipNumber);
        }

        [Fact]
        public void Run_StatusAndTime_UpdateTheSystem()
        {
            var reader = NewReader();
            var sys = Connect(reader);
            StartRun();

            reader.Send("S=10\n8:5:3 15-6-2026 (1)\n");

            Assert.True(WaitFor(() => sys.SystemStatus == TimingSystem.READING_STATUS_READING && sys.SystemTime != ""));
            Assert.Equal("15 Jun 2026 08:05:03", sys.SystemTime);
            Assert.True(WaitFor(() => CallCount("UpdateTimingFromController") >= 1));
        }

        [Fact]
        public void Run_ReaderDropsTheConnection_ReportsItAndStops()
        {
            var reader = NewReader();
            var sys = Connect(reader);
            StartRun();

            reader.Close();

            Assert.True(StopRun(), "The controller loop didn't stop after its only reader went away.");
            Assert.False(TimingController.IsRunning());
            Assert.Equal(1, CallCount("TimingSystemDisconnected"));
            Assert.Empty(controller.GetConnectedSystems());
        }

        [Fact]
        public void Run_OneReaderDrops_OtherKeepsWorking()
        {
            var a = NewReader();
            var b = NewReader();
            Connect(a);
            Connect(b);
            StartRun();

            a.Close();
            Assert.True(WaitFor(() => CallCount("TimingSystemDisconnected") == 1));

            b.Send(ChipLine("4000"));
            Assert.True(WaitFor(() => SavedReads().Count == 1));
            Assert.True(TimingController.IsRunning());
            Assert.Single(controller.GetConnectedSystems());
        }

        [Fact]
        public void Run_ReturnsWhileTheLoopIsRunning()
        {
            var reader = NewReader();
            Connect(reader);
            StartRun();

            var second = new Thread(controller.Run) { IsBackground = true };
            second.Start();
            Assert.True(second.Join(5000), "Run() should return right away while the loop is running.");
            Assert.True(TimingController.IsRunning());
        }

        [Fact]
        public void Run_ReadsOnTheCallingThread()
        {
            var reader = NewReader();
            Connect(reader);
            var thread = new Thread(controller.Run) { IsBackground = true };
            thread.Start();
            Assert.True(WaitFor(TimingController.IsRunning));

            reader.Send(ChipLine("1500"));
            Assert.True(WaitFor(() => SavedReads().Count == 1));

            controller.Shutdown();
            Assert.True(thread.Join(8000));
            Assert.False(TimingController.IsRunning());
        }

        [Fact]
        public void Start_WithoutATimingSystem_DoesNothing()
        {
            controller.Start();
            Assert.False(TimingController.IsRunning());
        }

        [Fact]
        public void Start_AfterTheLoopStopped_StartsANewOne()
        {
            var a = NewReader();
            Connect(a);
            StartRun();
            a.Close();
            Assert.True(StopRun(), "The loop didn't stop after its only reader went away.");

            var b = NewReader();
            Connect(b);
            controller.Start();
            b.Send(ChipLine("7000"));

            Assert.True(WaitFor(() => SavedReads().Count == 1), "The reader connected after the loop stopped was never read.");
        }

        [Fact]
        public void ConnectingAndDisconnectingWhileRunning_KeepsTheLoopWorking()
        {
            var steady = NewReader();
            Connect(steady);
            StartRun();

            for (int i = 0; i < 15; i++)
            {
                var r = NewReader();
                var sys = Connect(r);
                controller.Start();
                r.Send("S=10\n");
                controller.DisconnectTimingSystem(sys);
            }

            steady.Send(ChipLine("8000"));
            Assert.True(WaitFor(() => SavedReads().Count == 1));
            Assert.True(TimingController.IsRunning());
            Assert.Single(controller.GetConnectedSystems());
            Assert.Equal(0, CallCount("TimingSystemDisconnected"));
        }

        [Fact]
        public void Shutdown_StopsTheLoop()
        {
            var reader = NewReader();
            Connect(reader);
            StartRun();

            controller.Shutdown();

            Assert.True(StopRun(), "The controller loop didn't stop after Shutdown().");
            Assert.False(TimingController.IsRunning());
        }

        [Fact]
        public void DisconnectTimingSystem_IsNotReportedAsAnUnexpectedDisconnect()
        {
            var a = NewReader();
            var b = NewReader();
            var sysA = Connect(a);
            Connect(b);
            StartRun();

            controller.DisconnectTimingSystem(sysA);
            Thread.Sleep(500);

            Assert.Equal(SYSTEM_STATUS.DISCONNECTED, sysA.Status);
            Assert.Equal(0, CallCount("TimingSystemDisconnected"));
            b.Send(ChipLine("5000"));
            Assert.True(WaitFor(() => SavedReads().Count == 1));
        }

        [Fact]
        public void ConnectWhileRunning_NewReaderIsRead()
        {
            var a = NewReader();
            Connect(a);
            StartRun();

            var b = NewReader();
            Connect(b);
            controller.Start();
            b.Send(ChipLine("6000"));

            Assert.True(WaitFor(() => SavedReads().Count == 1, 8000), "A reader connected while the loop was running was never read.");
        }
    }
}
