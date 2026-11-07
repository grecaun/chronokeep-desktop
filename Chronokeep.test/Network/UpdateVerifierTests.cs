using Chronokeep.Updates;
using System.Net;
using System.Text;

namespace Chronokeep.Test.Network
{
    public class UpdateVerifierTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "chronokeep-tests", Guid.NewGuid().ToString("N"));

        public UpdateVerifierTests()
        {
            Directory.CreateDirectory(directory);
        }

        public void Dispose()
        {
            try { Directory.Delete(directory, true); } catch { }
        }

        // SHA-256 of "abc".
        private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        private string WriteFile(string name, string text)
        {
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, text);
            return path;
        }

        [Fact]
        public void FileSha256()
        {
            Assert.Equal(AbcHash, UpdateVerifier.FileSha256(WriteFile("abc.txt", "abc")));
        }

        [Fact]
        public void ParseChecksums_ReadsSha256sumOutput()
        {
            string text =
                $"{AbcHash}  chronokeep-setup.exe\n" +
                $"{AbcHash.ToUpperInvariant()} *Chronokeep-2.0.0-x64.dmg\r\n" +
                "not a checksum line\n" +
                "abc123  too-short.deb\n" +
                "\n";
            Dictionary<string, string> sums = UpdateVerifier.ParseChecksums(text);
            Assert.Equal(["Chronokeep-2.0.0-x64.dmg", "chronokeep-setup.exe"], sums.Keys.Order());
            Assert.Equal(AbcHash, sums["Chronokeep-2.0.0-x64.dmg"]);
        }

        [Theory]
        [InlineData("{\"attestations\":[{\"repository_id\":1,\"bundle_url\":\"https://x\"}]}", true)]
        [InlineData("{\"attestations\":[]}", false)]
        [InlineData("{\"message\":\"Not Found\"}", false)]
        [InlineData("not json", false)]
        public void HasAttestation(string json, bool expected)
        {
            Assert.Equal(expected, UpdateVerifier.HasAttestation(json));
        }

        /// <summary>A fake GitHub serving SHA256SUMS and the attestations API.</summary>
        private static FakeApiServer GitHub(string checksums, Func<string, string> attestations)
        {
            FakeApiServer server = new()
            {
                Responder = request =>
                {
                    if ("SHA256SUMS" == request.Path) return (HttpStatusCode.OK, checksums);
                    if (request.Path.StartsWith("attestations/sha256:")) return (HttpStatusCode.OK, attestations(request.Path["attestations/sha256:".Length..]));
                    return (HttpStatusCode.NotFound, "");
                }
            };
            return server;
        }

        private static async Task<string> Verify(FakeApiServer server, string fileName, string path, bool hasChecksums = true)
        {
            using HttpClient client = new();
            return await UpdateVerifier.Verify(client, hasChecksums ? $"{server.Url}SHA256SUMS" : "", $"{server.Url}attestations/sha256:", fileName, path, CancellationToken.None);
        }

        [Fact]
        public async Task Verify_MatchingAndAttested_IsFine()
        {
            using FakeApiServer server = GitHub($"{AbcHash}  chronokeep.deb\n",
                hash => AbcHash == hash ? "{\"attestations\":[{\"repository_id\":1}]}" : "{\"attestations\":[]}");
            Assert.Equal("", await Verify(server, "chronokeep.deb", WriteFile("chronokeep.deb", "abc")));
            Assert.Contains(server.Requests, r => $"attestations/sha256:{AbcHash}" == r.Path);
        }

        [Fact]
        public async Task Verify_ChangedFile_IsRejected()
        {
            using FakeApiServer server = GitHub($"{AbcHash}  chronokeep.deb\n", _ => "{\"attestations\":[{\"repository_id\":1}]}");
            string problem = await Verify(server, "chronokeep.deb", WriteFile("chronokeep.deb", "abd"));
            Assert.Contains("doesn't match", problem);
        }

        [Fact]
        public async Task Verify_NotAttested_IsRejected()
        {
            using FakeApiServer server = GitHub($"{AbcHash}  chronokeep.deb\n", _ => "{\"attestations\":[]}");
            string problem = await Verify(server, "chronokeep.deb", WriteFile("chronokeep.deb", "abc"));
            Assert.Contains("no record", problem);
        }

        [Fact]
        public async Task Verify_FileNotListed_IsRejected()
        {
            using FakeApiServer server = GitHub($"{AbcHash}  something-else.rpm\n", _ => "{\"attestations\":[{\"repository_id\":1}]}");
            string problem = await Verify(server, "chronokeep.deb", WriteFile("chronokeep.deb", "abc"));
            Assert.Contains("don't list chronokeep.deb", problem);
        }

        [Fact]
        public async Task Verify_ReleaseWithoutChecksums_IsRejected()
        {
            using FakeApiServer server = GitHub("", _ => "{\"attestations\":[]}");
            string problem = await Verify(server, "chronokeep.deb", WriteFile("chronokeep.deb", "abc"), hasChecksums: false);
            Assert.Contains("doesn't list checksums", problem);
            Assert.Empty(server.Requests);
        }

        [Fact]
        public async Task Verify_GitHubUnreachable_IsRejected()
        {
            string url;
            using (FakeApiServer stopped = new()) url = stopped.Url;
            using HttpClient client = new();
            string problem = await UpdateVerifier.Verify(client, $"{url}SHA256SUMS", $"{url}attestations/sha256:", "chronokeep.deb", WriteFile("chronokeep.deb", "abc"), CancellationToken.None);
            Assert.Contains("Unable to reach GitHub", problem);
        }
    }

    public class UpdatePackageTests
    {
        private static GithubRelease Release(params string[] names) => new()
        {
            Assets = [.. names.Select(n => new Assets { Name = n, BrowserDownloadUrl = $"https://github.com/x/{n}" })],
        };

        private static readonly GithubRelease Full = Release(
            "chronokeep.zip", "chronokeep-setup.exe", "chronokeep_2.0.0_amd64.deb", "chronokeep-2.0.0-1.x86_64.rpm",
            "Chronokeep-2.0.0-x64.dmg", "SHA256SUMS", "SHA256SUMS.asc", "chronokeep-signing-key.asc");

        [Theory]
        [InlineData("WindowsInstaller", "chronokeep-setup.exe")]
        [InlineData("MacDiskImage", "Chronokeep-2.0.0-x64.dmg")]
        [InlineData("LinuxDeb", "chronokeep_2.0.0_amd64.deb")]
        [InlineData("LinuxRpm", "chronokeep-2.0.0-1.x86_64.rpm")]
        public void FindAsset_EachKind(string kind, string expected)
        {
            Assert.Equal(expected, UpdatePackage.FindAsset(Full, Enum.Parse<UpdateKind>(kind))?.Name);
        }

        [Fact]
        public void FindAsset_NothingForThisSystem()
        {
            Assert.Null(UpdatePackage.FindAsset(Release("chronokeep-setup.exe"), UpdateKind.LinuxDeb));
            Assert.Null(UpdatePackage.FindAsset(Full, UpdateKind.None));
        }

        [Fact]
        public void ChecksumsUrl()
        {
            Assert.Equal("https://github.com/x/SHA256SUMS", UpdatePackage.ChecksumsUrl(Full));
            Assert.Equal("", UpdatePackage.ChecksumsUrl(Release("chronokeep-setup.exe")));
        }

        private static Func<string, bool> Has(params string[] files) => f => files.Contains(f);

        [Fact]
        public void LinuxKind_ByPackageManager()
        {
            Assert.Equal(UpdateKind.LinuxDeb, UpdatePackage.LinuxKind(Has("/usr/bin/dpkg")));
            Assert.Equal(UpdateKind.LinuxRpm, UpdatePackage.LinuxKind(Has("/usr/bin/rpm")));
            // Debian with rpm installed is still Debian.
            Assert.Equal(UpdateKind.LinuxDeb, UpdatePackage.LinuxKind(Has("/usr/bin/dpkg", "/usr/bin/rpm")));
            Assert.Equal(UpdateKind.None, UpdatePackage.LinuxKind(Has()));
        }

        [Fact]
        public void LinuxInstallCommand_Deb()
        {
            Assert.Equal(
                ["/usr/bin/pkexec", "/usr/bin/apt-get", "install", "-y", "/tmp/chronokeep_2.0.0_amd64.deb"],
                UpdatePackage.LinuxInstallCommand(UpdateKind.LinuxDeb, "/tmp/chronokeep_2.0.0_amd64.deb", Has("/usr/bin/pkexec", "/usr/bin/apt-get")));
        }

        [Fact]
        public void LinuxInstallCommand_Rpm()
        {
            Assert.Equal(
                ["/usr/bin/pkexec", "/usr/bin/dnf", "install", "-y", "/tmp/c.rpm"],
                UpdatePackage.LinuxInstallCommand(UpdateKind.LinuxRpm, "/tmp/c.rpm", Has("/usr/bin/pkexec", "/usr/bin/dnf", "/usr/bin/zypper")));
            Assert.Equal(
                ["/usr/bin/pkexec", "/usr/bin/zypper", "--non-interactive", "install", "/tmp/c.rpm"],
                UpdatePackage.LinuxInstallCommand(UpdateKind.LinuxRpm, "/tmp/c.rpm", Has("/usr/bin/pkexec", "/usr/bin/zypper")));
        }

        [Fact]
        public void LinuxInstallCommand_NoWayToInstall_IsEmpty()
        {
            Assert.Empty(UpdatePackage.LinuxInstallCommand(UpdateKind.LinuxDeb, "/tmp/c.deb", Has("/usr/bin/apt-get")));
            Assert.Empty(UpdatePackage.LinuxInstallCommand(UpdateKind.LinuxRpm, "/tmp/c.rpm", Has("/usr/bin/pkexec")));
        }

        [Fact]
        public void MacAppBundle_FromInsideTheApp()
        {
            string bundle = UpdatePackage.MacAppBundle("/Applications/Chronokeep.app/Contents/MacOS/");
            Assert.EndsWith("Chronokeep.app", bundle);
            Assert.Contains("Applications", bundle);
        }

        [Theory]
        [InlineData("/Users/someone/chronokeep/")]
        [InlineData("/Applications/Chronokeep/Contents/MacOS/")]
        public void MacAppBundle_NotRunningFromAnApp(string baseDirectory)
        {
            Assert.Equal("", UpdatePackage.MacAppBundle(baseDirectory));
        }
    }
}
