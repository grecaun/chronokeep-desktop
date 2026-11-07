/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Chronokeep.Helpers
{
    internal class AudioPlaybackEngine : IDisposable
    {
        private readonly WaveOutEvent outputDevice = new();
        private readonly MixingSampleProvider mixer;

        private static AudioPlaybackEngine? instance;
        private static int currentIndex;
        private static CachedSound alert = new(SoundPath("alert-1.wav"));

        /// <summary>
        /// The full path to a file in the program's sounds folder. The current folder isn't always the
        /// program's folder (shortcuts, file associations, the macOS Finder), so don't rely on it.
        /// </summary>
        private static string SoundPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "sounds", fileName);

        private AudioPlaybackEngine(int sampleRate = 44100, int channelCount = 2)
        {
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount))
            {
                ReadFully = true
            };
            outputDevice.Init(mixer);
            outputDevice.Play();
        }

        public static void PlaySound(string fileName)
        {
            instance ??= new AudioPlaybackEngine(44100, 1);
            AudioFileReader input = new AudioFileReader(fileName);
            instance.AddMixerInput(new AutoDisposeFileReader(input));
        }

        public static void PlaySound(int index)
        {
            if (index != currentIndex)
            {
                LoadCachedSound(index);
            }
            instance ??= new AudioPlaybackEngine(44100, 1);
            instance.AddMixerInput(new CachedSoundSampleProvider(alert));
        }

        private static void LoadCachedSound(int index)
        {
            currentIndex = index;
            string soundFile = SoundPath("alert-1.wav");
            switch (index)
            {
                case 1:
                    soundFile = SoundPath("alert-2.wav");
                    break;
                case 2:
                    soundFile = SoundPath("alert-3.wav");
                    break;
                case 3:
                    soundFile = SoundPath("alert-4.wav");
                    break;
                case 4:
                    soundFile = SoundPath("alert-5.wav");
                    break;
                case 5:
                    soundFile = SoundPath("emily-runner-here.wav");
                    break;
                case 6:
                    soundFile = SoundPath("emily-runner-arrived.wav");
                    break;
                case 7:
                    soundFile = SoundPath("emily-alert-runner-here.wav");
                    break;
                case 8:
                    soundFile = SoundPath("michael-runner-here.wav");
                    break;
                case 9:
                    soundFile = SoundPath("michael-runner-arrived.wav");
                    break;
                case 10:
                    soundFile = SoundPath("michael-alert-runner-here.wav");
                    break;
            }
            alert = new CachedSound(soundFile);
        }

        private void AddMixerInput(ISampleProvider input)
        {
            if (input.WaveFormat.Channels == mixer.WaveFormat.Channels)
            {
                mixer.AddMixerInput(input);
            }
            else switch (input.WaveFormat.Channels)
            {
                case 1 when mixer.WaveFormat.Channels == 2:
                    mixer.AddMixerInput(new MonoToStereoSampleProvider(input));
                    break;
                case 2 when mixer.WaveFormat.Channels == 1:
                    mixer.AddMixerInput(new StereoToMonoSampleProvider(input));
                    break;
            }
        }

        public void Dispose()
        {
            outputDevice.Dispose();
        }
    }

    internal class CachedSoundSampleProvider(CachedSound cachedSound) : ISampleProvider
    {
        private long position;

        public int Read(float[] buffer, int offset, int count)
        {
            long availableSamples = cachedSound.AudioData.Length - position;
            long samplesToCopy = Math.Min(availableSamples, count);
            Array.Copy(cachedSound.AudioData, position, buffer, offset, samplesToCopy);
            position += samplesToCopy;
            return (int)samplesToCopy;
        }

        public WaveFormat WaveFormat => cachedSound.WaveFormat;
    }

    internal class AutoDisposeFileReader(AudioFileReader reader) : ISampleProvider
    {
        private bool isDisposed;

        public WaveFormat WaveFormat { get; } = reader.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            if (isDisposed)
            {
                return 0;
            }
            int read = reader.Read(buffer, offset, count);
            if (read != 0) return read;
            reader.Dispose();
            isDisposed = true;
            return read;
        }
    }

    internal class CachedSound
    {
        public float[] AudioData { get; }
        public WaveFormat WaveFormat { get; }

        public CachedSound(string audioFileName)
        {
            using AudioFileReader audioFileReader = new(audioFileName);
            WaveFormat = audioFileReader.WaveFormat;
            List<float> wholeFile = new((int)(audioFileReader.Length / 4));
            float[] readBuffer = new float[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];
            int samplesRead;
            while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                wholeFile.AddRange(readBuffer.Take(samplesRead));
            }
            AudioData = [.. wholeFile];
        }
    }
}

