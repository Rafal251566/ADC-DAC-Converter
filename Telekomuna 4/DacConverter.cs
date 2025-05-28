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

        if (fmt.BitsPerSample == 8 || fmt.BitsPerSample == 16 || fmt.BitsPerSample == 24 || fmt.BitsPerSample == 32)
        {
            Console.WriteLine($"Odtwarzam plik {fmt.BitsPerSample}-bitowy za pomocą standardowego AudioFileReader.");
            audioFileReader = new AudioFileReader(filePath);
            waveOut = new WaveOutEvent();
            waveOut.Init(audioFileReader);
        }
        else
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

    public WaveFormat GetWaveFormat(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Plik nie istnieje: {filePath}");
            return null;
        }

        try
        {
            using (var stream = File.OpenRead(filePath))
            using (var reader = new BinaryReader(stream))
            {
                string riffId = new string(reader.ReadChars(4));
                int fileSize = reader.ReadInt32();
                string waveId = new string(reader.ReadChars(4)); 

                if (riffId != "RIFF" || waveId != "WAVE")
                {
                    Console.WriteLine($"Nieprawidłowy nagłówek RIFF/WAVE w pliku: {filePath}");
                    return null;
                }

                string chunkId;
                int chunkSize;
                short audioFormat = 0;
                short numChannels = 0;
                int sampleRate = 0;
                int byteRate = 0;
                short blockAlign = 0;
                short bitsPerSample = 0;

                while (stream.Position < stream.Length)
                {
                    chunkId = new string(reader.ReadChars(4));
                    chunkSize = reader.ReadInt32();

                    if (chunkId == "fmt ")
                    {
                        audioFormat = reader.ReadInt16();
                        numChannels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        byteRate = reader.ReadInt32();
                        blockAlign = reader.ReadInt16();
                        bitsPerSample = reader.ReadInt16();

                        if (chunkSize > 16)
                        {
                            reader.ReadBytes(chunkSize - 16);
                        }
                    }
                    else if (chunkId == "data")
                    {
                        stream.Seek(chunkSize, SeekOrigin.Current); 
                    }
                    else
                    {
                        stream.Seek(chunkSize, SeekOrigin.Current);
                    }

                    if (chunkSize % 2 != 0 && stream.Position < stream.Length)
                    {
                        stream.ReadByte();
                    }

                    if (audioFormat != 0 && sampleRate != 0) 
                        break;
                }

                if (audioFormat == 1)
                {
                    return new WaveFormat(sampleRate, bitsPerSample, numChannels);
                }
                else
                {
                    Console.WriteLine($"Nieobsługiwany format audio (nie PCM): {audioFormat} w pliku: {filePath}");
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas odczytu formatu WAV z pliku {filePath}: {ex.Message}");
            return null;
        }
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