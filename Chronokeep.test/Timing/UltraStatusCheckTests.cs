using Chronokeep.Timing.Interfaces;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Chronokeep.Test.Timing
{
    /// <summary>
    /// Asking an RFID Ultra whether it's reading: shortly after connecting, and again whenever it has gone a while
    /// without a chip read. The intervals are shortened so the tests run quickly.
    /// </summary>
    public class UltraStatusCheckTests : ReaderTestBase
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly RfidUltraInterface reader;
        private Socket? readerSide;
        private Socket? chronokeepSide;

        public UltraStatusCheckTests()
        {
            listener.Start();
            reader = new RfidUltraInterface(Db, Location, Window);
        }

        public override void Dispose()
        {
            reader.Disconnect();
            chronokeepSide?.Dispose();
            readerSide?.Dispose();
            listener.Stop();
            base.Dispose();
        }

        /// <summary>Connects the way Chronokeep does, and returns the reader's end of the connection.</summary>
        private Socket Connect(TimeSpan delay, TimeSpan interval)
        {
            reader.StatusCheckDelay = delay;
            reader.StatusCheckInterval = interval;
            List<Socket>? sockets = reader.Connect("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            chronokeepSide = Assert.Single(sockets!);
            readerSide = listener.AcceptSocket();
            return readerSide;
        }

        /// <summary>Waits up to the given time for a status check, returning how long it took, or null if none came.</summary>
        private static TimeSpan? NextStatusCheck(Socket readerSide, TimeSpan within)
        {
            Stopwatch watch = Stopwatch.StartNew();
            if (!readerSide.Poll((int)(within.TotalMilliseconds * 1000), SelectMode.SelectRead)) return null;
            byte[] buffer = new byte[16];
            int received = readerSide.Receive(buffer);
            Assert.Equal("?\n", Encoding.Latin1.GetString(buffer, 0, received));
            return watch.Elapsed;
        }

        private const string ChipRead = "0,058003,1781510400,250,2,-45,0,1,BOX1,8:00:00.250 15-6-2026,0,17\n";

        [Fact]
        public void Asks_ShortlyAfterConnecting_NotStraightAway()
        {
            Socket readerSide = Connect(TimeSpan.FromMilliseconds(400), TimeSpan.FromSeconds(30));

            Assert.Null(NextStatusCheck(readerSide, TimeSpan.FromMilliseconds(200)));
            Assert.NotNull(NextStatusCheck(readerSide, TimeSpan.FromSeconds(2)));
            // and not again until it has gone 30 seconds without a read
            Assert.Null(NextStatusCheck(readerSide, TimeSpan.FromMilliseconds(500)));
        }

        [Fact]
        public void Asks_AgainEachIntervalWithoutReads()
        {
            Socket readerSide = Connect(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(500));
            Assert.NotNull(NextStatusCheck(readerSide, TimeSpan.FromSeconds(2)));

            for (int i = 0; i < 2; i++)
            {
                TimeSpan? next = NextStatusCheck(readerSide, TimeSpan.FromSeconds(3));
                Assert.NotNull(next);
                Assert.InRange(next.Value, TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(1500));
            }
        }

        [Fact]
        public void ChipReads_PutOffTheNextCheck()
        {
            Socket readerSide = Connect(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(600));
            Assert.NotNull(NextStatusCheck(readerSide, TimeSpan.FromSeconds(2)));

            // Reads keep arriving more often than the interval, so there's no need to ask.
            for (int i = 0; i < 6; i++)
            {
                reader.ParseMessages(ChipRead, chronokeepSide!);
                Assert.Null(NextStatusCheck(readerSide, TimeSpan.FromMilliseconds(250)));
            }
            // Once they stop, it asks again.
            Assert.NotNull(NextStatusCheck(readerSide, TimeSpan.FromSeconds(2)));
        }

        [Fact]
        public void Disconnecting_StopsTheChecks()
        {
            Socket readerSide = Connect(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(300));
            Assert.NotNull(NextStatusCheck(readerSide, TimeSpan.FromSeconds(2)));

            reader.Disconnect();

            Assert.Null(NextStatusCheck(readerSide, TimeSpan.FromMilliseconds(900)));
        }

        [Fact]
        public void ALostConnection_StopsTheChecksWithoutErrors()
        {
            Socket readerSide = Connect(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200));
            Assert.NotNull(NextStatusCheck(readerSide, TimeSpan.FromSeconds(2)));

            // The timing loop closes the socket when the reader goes away, without telling the interface.
            chronokeepSide!.Close();
            Thread.Sleep(700);

            // The next check couldn't send, so the timer stopped itself (an exception on its thread would have
            // ended the test run instead).
            var timer = typeof(RfidUltraInterface).GetField("statusTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            Assert.Null(timer.GetValue(reader));
        }
    }
}
