using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.IO;
using System.Text;
using NAudio.MediaFoundation;

public class AdcConverter
{
    private WaveInEvent waveIn;
    private MemoryStream buffer;
    private int initialBitDepth;
    private int initialChannels;
    private int initialSampleRate;

    private bool saveImmediately = true;
    private string outputFilePath;

    public void StartRecording(string filePath, int sampleRate, int bitDepth, int channels, int deviceNumber = 0, bool saveAudioImmediately = true)
    {
        this.outputFilePath = filePath;
        this.initialSampleRate = sampleRate;
        this.initialBitDepth = bitDepth;
        this.initialChannels = channels;
        this.saveImmediately = saveAudioImmediately;

        waveIn = new WaveInEvent()
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(sampleRate, 16, channels) 
        };

        buffer = new MemoryStream();
        waveIn.DataAvailable += (s, a) =>
        {
            buffer.Write(a.Buffer, 0, a.BytesRecorded);

            float max = 0;
            for (int i = 0; i < a.BytesRecorded; i += 2)
            {
                short sample = BitConverter.ToInt16(a.Buffer, i);
                float absSample = Math.Abs(sample / 32768f);
                if (absSample > max) max = absSample;
            }
            Console.Write($"\rPoziom nagrywania: {max:P0}        ");
        };

        waveIn.RecordingStopped += (s, a) =>
        {
            if (saveImmediately)
            {
                File.WriteAllBytes(outputFilePath, BuildWav(buffer.ToArray(), initialSampleRate, initialChannels, initialBitDepth));
                Console.WriteLine("\n");
            }
            waveIn.Dispose();
        };

        waveIn.StartRecording();
    }

    public void StopRecording()
    {
        waveIn?.StopRecording();
    }

    public byte[] StopRecordingAndReturnData()
    {
        waveIn?.StopRecording();
        System.Threading.Thread.Sleep(200);

        byte[] rawData = buffer.ToArray();
        waveIn?.Dispose();
        Console.WriteLine("\n");
        return rawData;
    }

    public byte[] ProcessAndBuildWav(byte[] rawInput16BitData, int targetSampleRate, int targetBitDepth, int targetChannels)
    {
        byte[] resampledData = Resample16BitPcm(rawInput16BitData, initialSampleRate, targetSampleRate, targetChannels);
        byte[] processedData = ProcessAudioBuffer(resampledData, resampledData.Length, targetBitDepth, targetChannels);
        return BuildWav(processedData, targetSampleRate, targetChannels, targetBitDepth);
    }

    private byte[] ProcessAudioBuffer(byte[] inputBuffer16Bit, int bytesRecorded, int targetBitDepth, int channels)
    {
        int bytesPerInputSample = 2;
        int numSamples = bytesRecorded / bytesPerInputSample;

        if (targetBitDepth == 16)
        {
            return inputBuffer16Bit;
        }
        else if (targetBitDepth == 24) 
        {
            byte[] outputBuffer = new byte[numSamples * 3];
            for (int i = 0; i < numSamples; i++)
            {
                short sample16Bit = BitConverter.ToInt16(inputBuffer16Bit, i * bytesPerInputSample);
                int sample24Bit = sample16Bit << 8;

                outputBuffer[i * 3] = (byte)(sample24Bit & 0xFF);
                outputBuffer[i * 3 + 1] = (byte)((sample24Bit >> 8) & 0xFF);
                outputBuffer[i * 3 + 2] = (byte)((sample24Bit >> 16) & 0xFF);
            }
            return outputBuffer;
        }
        else if (targetBitDepth == 8)
        {
            byte[] outputBuffer = new byte[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                short sample16Bit = BitConverter.ToInt16(inputBuffer16Bit, i * bytesPerInputSample);
                outputBuffer[i] = (byte)((sample16Bit + 32768) / 256);
            }
            return outputBuffer;
        }
        else if (targetBitDepth == 4)
        {
            int numOutputBytes = (numSamples + 1) / 2;
            byte[] outputBuffer = new byte[numOutputBytes];

            for (int i = 0; i < numSamples; i++)
            {
                short sample16Bit = BitConverter.ToInt16(inputBuffer16Bit, i * bytesPerInputSample);
                float normalizedSample = (sample16Bit + 32768f) / 65535f;
                byte quantizedSample = (byte)(normalizedSample * ((1 << 4) - 1));

                if (i % 2 == 0)
                {
                    outputBuffer[i / 2] = (byte)(quantizedSample << 4);
                }
                else
                {
                    outputBuffer[i / 2] |= quantizedSample;
                }
            }
            return outputBuffer;
        }
        else if (targetBitDepth == 2)
        {
            int numOutputBytes = (numSamples + 3) / 4;
            byte[] outputBuffer = new byte[numOutputBytes];

            for (int i = 0; i < numSamples; i++)
            {
                short sample16Bit = BitConverter.ToInt16(inputBuffer16Bit, i * bytesPerInputSample);
                float normalizedSample = (sample16Bit + 32768f) / 65535f; 
                byte quantizedSample = (byte)(normalizedSample * ((1 << 2) - 1)); 

                int byteIndex = i / 4;
                int samplePositionInByte = i % 4; // 0, 1, 2, 3

                outputBuffer[byteIndex] |= (byte)(quantizedSample << (samplePositionInByte * 2));
            }
            return outputBuffer;
        }
        else
        {
            Console.WriteLine($"\nOstrzeżenie: Nieobsługiwana docelowa głębia bitowa do przetwarzania: {targetBitDepth}. Zwracam puste dane.");
            return new byte[0];
        }
    }

    private byte[] Resample16BitPcm(byte[] inputData, int originalSampleRate, int targetSampleRate, int channels)
    {
        using var inputStream = new RawSourceWaveStream(new MemoryStream(inputData), new WaveFormat(originalSampleRate, 16, channels));
        using var resampler = new MediaFoundationResampler(inputStream, new WaveFormat(targetSampleRate, 16, channels))
        {
            ResamplerQuality = 60
        };

        using var ms = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(ms, resampler);
        var wavData = ms.ToArray();

        using var reader = new WaveFileReader(new MemoryStream(wavData));
        using var pcmStream = new MemoryStream();
        reader.CopyTo(pcmStream);
        return pcmStream.ToArray();
    }


    private byte[] BuildWav(byte[] data, int sampleRate, int channels, int bitDepth)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        int bytesPerSampleOutput = (bitDepth + 7) / 8;

        if (bitDepth == 1) bytesPerSampleOutput = 1;
        if (bitDepth == 2) bytesPerSampleOutput = 1;
        if (bitDepth == 4) bytesPerSampleOutput = 1;


        int dataSize = data.Length;

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bytesPerSampleOutput);
        writer.Write((short)(channels * bytesPerSampleOutput));
        writer.Write((short)bitDepth);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        writer.Write(data);
        return ms.ToArray();
    }
}