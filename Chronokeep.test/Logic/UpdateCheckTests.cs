using Chronokeep.Updates;

namespace Chronokeep.Test.Logic
{
    public class UpdateCheckTests
    {
        [Theory]
        [InlineData("v2.0.0", 2, 0, 0)]
        [InlineData("2.0.0", 2, 0, 0)]
        [InlineData("V1.9.10", 1, 9, 10)]
        [InlineData("v1.9.10-5-gabc123\n", 1, 9, 10)]   // git describe output in version.txt
        [InlineData("v2.1.0-beta", 2, 1, 0)]
        [InlineData("v2.0.0 – Avalonia release", 2, 0, 0)]
        [InlineData("  v3.4.5", 3, 4, 5)]
        public void TryParse_ReadsVersion(string text, int major, int minor, int patch)
        {
            Assert.True(Updates.Version.TryParse(text, out var v));
            Assert.Equal((major, minor, patch), (v.Major, v.Minor, v.Patch));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Chronokeep 2.0")]
        [InlineData("v2.0")]
        [InlineData("latest")]
        [InlineData("v99999999999.0.0")]
        public void TryParse_RejectsWhatItCantRead(string? text)
        {
            Assert.False(Updates.Version.TryParse(text, out var v));
            Assert.Equal("v0.0.0", v.ToString());
        }

        private static GithubRelease Release(string tag, string name = "", bool draft = false, bool pre = false)
            => new() { TagName = tag, Name = name, Draft = draft, PreRelease = pre };

        [Fact]
        public void FindLatest_PicksNewestVersion()
        {
            var (release, version) = Check.FindLatest([Release("v1.9.10"), Release("v2.0.1"), Release("v2.0.0")]);
            Assert.Equal("v2.0.1", release!.TagName);
            Assert.Equal("v2.0.1", version.ToString());
        }

        [Fact]
        public void FindLatest_SkipsDraftsAndPrereleases()
        {
            var (release, _) = Check.FindLatest([
                Release("v2.0.0"),
                Release("v2.1.0", draft: true),
                Release("v2.2.0-beta", pre: true)]);
            Assert.Equal("v2.0.0", release!.TagName);
        }

        [Fact]
        public void FindLatest_UsesTagBeforeName()
        {
            var (release, version) = Check.FindLatest([Release("v2.0.1", name: "v2.0.0 – Avalonia release")]);
            Assert.Equal("v2.0.1", version.ToString());
            Assert.NotNull(release);
        }

        [Fact]
        public void FindLatest_FallsBackToNameWithoutTag()
        {
            var (_, version) = Check.FindLatest([Release("", name: "v1.9.9")]);
            Assert.Equal("v1.9.9", version.ToString());
        }

        [Fact]
        public void FindLatest_SkipsUnreadableReleasesInsteadOfFailing()
        {
            var (release, _) = Check.FindLatest([Release("latest", name: "Chronokeep 2.0"), Release("v1.9.10")]);
            Assert.Equal("v1.9.10", release!.TagName);
        }

        [Fact]
        public void FindLatest_NothingUsable_ReturnsNoRelease()
        {
            var (release, version) = Check.FindLatest([Release("v3.0.0", draft: true)]);
            Assert.Null(release);
            Assert.Equal("v0.0.0", version.ToString());
        }
    }
}
