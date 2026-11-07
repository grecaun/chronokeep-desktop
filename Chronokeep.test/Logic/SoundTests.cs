using Chronokeep.Helpers;
using NAudio.Wave;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Chronokeep.Test.Logic
{
    /// <summary>
    /// Loading and reading alert sounds. Nothing here plays a sound, so no audio device is needed.
    /// </summary>
    public class SoundTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "chronokeep-tests", Guid.NewGuid().ToString("N"));

        public SoundTests()
        {
            Directory.CreateDirectory(directory);
        }

        public void Dispose()
        {
            try { Directory.Delete(directory, true); } catch { }
        }

        /// <summary>Writes a mono 16-bit WAV file with the given samples.</summary>
        private string Wav(params short[] samples)
        {
            string path = Path.Combine(directory, $"{Guid.NewGuid():N}.wav");
            using WaveFileWriter writer = new(path, new WaveFormat(8000, 16, 1));
            writer.WriteSamples(samples, 0, samples.Length);
            return path;
        }

        [Fact]
        public void CachedSound_ReadsTheWholeFile()
        {
            short[] samples = [.. Enumerable.Range(0, 20000).Select(i => (short)(i % 2 == 0 ? 16384 : -16384))];
            CachedSound sound = new(Wav(samples));

            Assert.Equal(8000, sound.WaveFormat.SampleRate);
            Assert.Equal(1, sound.WaveFormat.Channels);
            Assert.Equal(samples.Length, sound.AudioData.Length);
            Assert.Equal(0.5f, sound.AudioData[0], 3);
            Assert.Equal(-0.5f, sound.AudioData[1], 3);
        }

        [Fact]
        public void CachedSoundSampleProvider_ReadsInPiecesThenStops()
        {
            CachedSound sound = new(Wav(1000, 2000, 3000, 4000, 5000));
            CachedSoundSampleProvider provider = new(sound);
            float[] buffer = new float[10];

            Assert.Equal(3, provider.Read(buffer, 0, 3));
            Assert.Equal(2, provider.Read(buffer, 3, 3));
            Assert.Equal(0, provider.Read(buffer, 5, 3));
            Assert.Equal(sound.AudioData, buffer[..5]);
            Assert.Same(sound.WaveFormat, provider.WaveFormat);
        }

        [Fact]
        public void CachedSoundSampleProvider_EachOnePlaysFromTheStart()
        {
            CachedSound sound = new(Wav(1000, 2000));
            float[] buffer = new float[2];
            new CachedSoundSampleProvider(sound).Read(buffer, 0, 2);
            Assert.Equal(2, new CachedSoundSampleProvider(sound).Read(buffer, 0, 2));
        }

        [Fact]
        public void AutoDisposeFileReader_ReadsThenClosesTheFile()
        {
            string path = Wav(1000, 2000, 3000);
            AutoDisposeFileReader reader = new(new AudioFileReader(path));
            float[] buffer = new float[10];

            Assert.Equal(3, reader.Read(buffer, 0, 10));
            Assert.Equal(0, reader.Read(buffer, 0, 10));
            Assert.Equal(0, reader.Read(buffer, 0, 10));
            // The file was closed, so it can be deleted.
            File.Delete(path);
            Assert.False(File.Exists(path));
        }

        private static readonly MethodInfo LoadCachedSound =
            typeof(AudioPlaybackEngine).GetMethod("LoadCachedSound", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static CachedSound CurrentAlert()
            => (CachedSound)typeof(AudioPlaybackEngine).GetField("alert", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        /// <summary>The sounds in the settings page's alarm sound list, in order.</summary>
        public static TheoryData<int> AlarmSounds() => [.. Enumerable.Range(0, 11)];

        [Theory]
        [MemberData(nameof(AlarmSounds))]
        public void EveryAlarmSound_Loads(int index)
        {
            RuntimeHelpers.RunClassConstructor(typeof(AudioPlaybackEngine).TypeHandle);
            LoadCachedSound.Invoke(null, [index]);
            Assert.True(CurrentAlert().AudioData.Length > 0);
        }

        [Fact]
        public void AlarmSounds_LoadWhateverTheCurrentFolder()
        {
            // Started from a shortcut, a file association or the macOS Finder, the current
            // folder usually isn't the program's folder.
            RuntimeHelpers.RunClassConstructor(typeof(AudioPlaybackEngine).TypeHandle);
            string old = Environment.CurrentDirectory;
            Environment.CurrentDirectory = directory;
            try
            {
                LoadCachedSound.Invoke(null, [3]);
            }
            finally
            {
                Environment.CurrentDirectory = old;
            }
            Assert.True(CurrentAlert().AudioData.Length > 0);
        }
    }
}
