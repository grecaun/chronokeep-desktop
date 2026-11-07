using Chronokeep.Helpers;
using System.Runtime.InteropServices;

namespace Chronokeep.Test.Logic
{
    public class ProgramFoldersTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "chronokeep-tests", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try { Directory.Delete(directory, true); } catch { }
        }

        [Fact]
        public void DataFolder_EachOperatingSystem()
        {
            Assert.Equal(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "Chronokeep"),
                ProgramFolders.GetDataFolder(OSPlatform.Windows));
            Assert.Equal("/Users/Shared/Chronokeep", ProgramFolders.GetDataFolder(OSPlatform.OSX));
            Assert.Equal("/var/lib/chronokeep", ProgramFolders.GetDataFolder(OSPlatform.Linux));
            Assert.Equal("/var/lib/chronokeep", ProgramFolders.GetDataFolder(OSPlatform.FreeBSD));
        }

        [Fact]
        public void LogsAndExports_AreInTheDataFolder()
        {
            Assert.Equal(Path.Combine(ProgramFolders.Data, "logs"), ProgramFolders.Logs);
            Assert.Equal(Path.Combine(ProgramFolders.Data, "exports"), ProgramFolders.Exports);
        }

        [Fact]
        public void CreateFolder_CreatesParentsToo()
        {
            string path = Path.Combine(directory, "Chronokeep", "logs");
            Assert.True(ProgramFolders.CreateFolder(path));
            Assert.True(Directory.Exists(path));
            // Already there is fine.
            Assert.True(ProgramFolders.CreateFolder(path));
        }

        [Fact]
        public void CreateFolder_AFileInTheWay_IsFalse()
        {
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, "Chronokeep");
            File.WriteAllText(file, "");
            Assert.False(ProgramFolders.CreateFolder(file));
        }

        [Fact]
        public void CanWrite()
        {
            Directory.CreateDirectory(directory);
            Assert.True(ProgramFolders.CanWrite(directory));
            Assert.Empty(Directory.GetFiles(directory));
            Assert.False(ProgramFolders.CanWrite(Path.Combine(directory, "missing")));
        }

        [Fact]
        public void ShareFile()
        {
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, "Chronokeep.sqlite");
            File.WriteAllText(file, "");
            Assert.True(ProgramFolders.ShareFile(file));
            if (!OperatingSystem.IsWindows())
            {
                UnixFileMode mode = File.GetUnixFileMode(file);
                Assert.True(mode.HasFlag(UnixFileMode.OtherWrite));
                Assert.True(mode.HasFlag(UnixFileMode.GroupWrite));
            }
        }

        [Fact]
        public void FolderProblemMessage_NamesTheFolder()
        {
            Assert.Contains("/var/lib/chronokeep", ProgramFolders.FolderProblemMessage("/var/lib/chronokeep"));
        }
    }
}
