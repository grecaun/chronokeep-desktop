using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Test.Database;

namespace Chronokeep.Test.Logic
{
    public class HardwareIdTests
    {
        private const ulong GB = 1024UL * 1024 * 1024;

        [Fact]
        public void Id_ListsEachPart()
        {
            string id = HardwareChecker.BuildHardwareId(
                "Microsoft Windows 11 Pro",
                [("Intel Core i7 ", 8, 16)],
                [8 * GB, 8 * GB],
                ["NVIDIA GeForce RTX 3060"]);

            Assert.Equal("Microsoft_Windows_11_Pro-Intel_Core_i78C-16P-2@16GB-NVIDIA_GeForce_RTX_3060", id);
        }

        [Fact]
        public void Id_AddsUpEveryProcessor()
        {
            string id = HardwareChecker.BuildHardwareId("Linux", [("Xeon", 4, 8), ("Xeon", 4, 8)], [GB], ["VGA"]);
            // Exactly 1 GB isn't over 1024 MB, so it stays in MB.
            Assert.Equal("Linux-Xeon+Xeon8C-16P-1@1024MB-VGA", id);
        }

        [Fact]
        public void Id_SeveralVideoControllers()
        {
            string id = HardwareChecker.BuildHardwareId("Linux", [("Arm", 4, 4)], [GB], ["Intel UHD", "NVIDIA"]);
            Assert.EndsWith("-Intel_UHD+NVIDIA", id);
        }

        [Theory]
        [InlineData(512UL, "1@512B")]
        [InlineData(1024UL, "1@1024B")]
        [InlineData(2048UL, "1@2KB")]
        [InlineData(3UL * 1024 * 1024, "1@3MB")]
        [InlineData(2UL * 1024 * 1024 * 1024 * 1024, "1@2TB")]
        public void Id_MemoryUsesTheLargestUnit(ulong bytes, string expected)
        {
            Assert.Contains($"-{expected}-", HardwareChecker.BuildHardwareId("OS", [("CPU", 1, 1)], [bytes], ["GPU"]));
        }

        [Fact]
        public void Id_IsTheSameEachTime()
        {
            string first = HardwareChecker.BuildHardwareId("OS", [("CPU", 2, 4)], [GB], ["GPU"]);
            string second = HardwareChecker.BuildHardwareId("OS", [("CPU", 2, 4)], [GB], ["GPU"]);
            Assert.Equal(first, second);
        }
    }

    public class HardwareCheckerTests : DatabaseTestBase
    {
        [Fact]
        public void FirstRun_SavesTheHardwareId()
        {
            // This reads the real computer's hardware, the way the main window does at startup.
            Constants.Settings.SetupSettings(Db);

            new HardwareChecker(Db).Run();

            AppSetting? saved = Db.GetAppSetting(Constants.Settings.HARDWARE_IDENTIFIER);
            Assert.NotNull(saved);
            Assert.NotEqual("", saved.Value);
        }
    }
}
