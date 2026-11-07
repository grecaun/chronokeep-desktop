using Chronokeep.Helpers;

namespace Chronokeep.Test.Logic
{
    public class LogTests : IDisposable
    {
        private readonly string old = Globals.ErrorLogPath;
        private readonly string directory = Path.Combine(Path.GetTempPath(), "chronokeep-tests", Guid.NewGuid().ToString("N"));

        public LogTests()
        {
            Directory.CreateDirectory(directory);
        }

        public void Dispose()
        {
            Globals.ErrorLogPath = old;
            try { Directory.Delete(directory, true); } catch { }
        }

        [Fact]
        public void Error_IsWrittenToTheErrorLog()
        {
            Globals.ErrorLogPath = Path.Combine(directory, "error_log.txt");
            Log.E("Test.LogTests", "Something went wrong.");
            Assert.Contains("Test.LogTests", File.ReadAllText(Globals.ErrorLogPath));
            Assert.Contains("Something went wrong.", File.ReadAllText(Globals.ErrorLogPath));
        }

        [Fact]
        public void Error_FromManyThreads_AllWritten()
        {
            Globals.ErrorLogPath = Path.Combine(directory, "error_log.txt");
            Parallel.For(0, 200, i => Log.E("Test.LogTests", $"Error {i}."));
            Assert.Equal(200, File.ReadAllLines(Globals.ErrorLogPath).Length);
        }

        [Fact]
        public void Error_BeforeTheLogIsSetUp_DoesNothing()
        {
            Globals.ErrorLogPath = "";
            Log.E("Test.LogTests", "Too early.");
        }

        [Fact]
        public void Error_LogCantBeWritten_DoesntThrow()
        {
            Globals.ErrorLogPath = Path.Combine(directory, "missing", "error_log.txt");
            Log.E("Test.LogTests", "Nowhere to write this.");
        }
    }
}
