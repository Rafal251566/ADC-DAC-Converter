using NAudio.Wave;
using System;
using System.IO;
using System.Text;

public class DacConverter : IDisposable
{
    private WaveOutEvent waveOut;
    private AudioFileReader audioFileReader;
    private VariableBitWaveProvider variableBitProvider;

    public PlaybackState PlaybackState => waveOut?.PlaybackState ?? PlaybackState.Stopped;

    public TimeSpan CurrentTime
    {
        get
        {
            if (audioFileReader != null) return audioFileReader.CurrentTime;
            if (variableBitProvider != null) return variableBitProvider.CurrentTime;
            return TimeSpan.Zero;
        }
    }

    public TimeSpan TotalTime
    {
        get
        {
            if (audioFileReader != null) return audioFileReader.TotalTime;
            if (variableBitProvider != null) return variableBitProvider.TotalTime;
            return TimeSpan.Zero;
        }
    }

    public void PlaySound(string filePath)
    {
        Dispose();

        var fmt = GetWaveFormat(filePath);
        if (fmt == null)
        {
            Console.WriteLine("Nie można odczytać formatu pliku WAV.");
            return;
        }

        //odtwarzacz z NAudio
        if (fmt.BitsPerSample == 8 || fmt.BitsPerSample == 16 || fmt.BitsPerSample == 24 || fmt.BitsPerSample == 32)
        {
            Console.WriteLine($"Odtwarzam plik {fmt.BitsPerSample}-bitowy za pomocą standardowego AudioFileReader.");
            audioFileReader = new AudioFileReader(filePath);
            waveOut = new WaveOutEvent();
            waveOut.Init(audioFileReader);
        }
        else //niestandardowy odtwarzacz
        {
            Console.WriteLine($"Odtwarzam niestandardowy {fmt.BitsPerSample}-bitowy plik za pomocą VariableBitWaveProvider.");
            variableBitProvider = new VariableBitWaveProvider(filePath, fmt.SampleRate, fmt.Channels, fmt.BitsPerSample);
            waveOut = new WaveOutEvent();
            waveOut.Init(variableBitProvider);
        }

        waveOut.Play();
    }

    public void StopPlaying()
    {
        waveOut?.Stop();
        Dispose();
    }

    private WaveFormat GetWaveFormat(string path)
    {
        try
        {
            using var br = new BinaryReader(File.OpenRead(path));
            br.ReadBytes(12);

            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                string chunkId = new string(br.ReadChars(4));
                int chunkSize = br.ReadInt32();

                if (chunkId == "fmt ")
                {
                    short audioFormat = br.ReadInt16();
                    if (audioFormat != 1)
                    {
                        Console.WriteLine($"\nOstrzeżenie: Plik nie jest w formacie PCM (AudioFormat={audioFormat}). Odtwarzanie może być nieprawidłowe.");
                    }
                    short channels = br.ReadInt16();
                    int sampleRate = br.ReadInt32();
                    br.ReadInt32();
                    br.ReadInt16();
                    short bits = br.ReadInt16();
                    return new WaveFormat(sampleRate, bits, channels);
                }
                else
                {
                    br.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                }
            }
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine($"\nBłąd: Niepełny lub uszkodzony plik WAV: {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nBłąd podczas odczytu nagłówka WAV dla {path}: {ex.Message}");
        }
        return null;
    }

    public void Dispose()
    {
        if (waveOut != null)
        {
            waveOut.Dispose();
            waveOut = null;
        }
        if (audioFileReader != null)
        {
            audioFileReader.Dispose();
            audioFileReader = null;
        }
        if (variableBitProvider != null)
        {
            variableBitProvider.Dispose();
            variableBitProvider = null;
        }
    }
}