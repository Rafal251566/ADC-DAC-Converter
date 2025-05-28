using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.IO;
using System.Text; // Dodaj to

public class AdcConverter
{
    private WaveInEvent waveIn;
    private MemoryStream buffer;
    private int bitDepth;
    private int channels;
    private int sampleRate;

    public void StartRecording(string filePath, int sampleRate, int bitDepth, int channels, int deviceNumber = 0)
    {
        this.sampleRate = sampleRate;
        this.bitDepth = bitDepth;
        this.channels = channels;

        waveIn = new WaveInEvent()
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(sampleRate, 16, channels)
        };

        buffer = new MemoryStream();
        waveIn.DataAvailable += (s, a) =>
        {
            var processedData = ProcessAudioBuffer(a.Buffer, a.BytesRecorded, bitDepth);
            buffer.Write(processedData, 0, processedData.Length);

            float max = 0;
            for (int i = 0; i < a.BytesRecorded; i += 2)
            {
                short sample = BitConverter.ToInt16(a.Buffer, i);
                float absSample = Math.Abs(sample / 32768f);
                if (absSample > max) max = absSample;
            }
            Console.Write($"\rPoziom nagrywania: {max:P0}       ");
        };

        waveIn.RecordingStopped += (s, a) =>
        {
            File.WriteAllBytes(filePath, BuildWav(buffer.ToArray(), sampleRate, bitDepth, channels));
            waveIn.Dispose();
            Console.WriteLine("\n");
        };

        waveIn.StartRecording();
    }

    public void StopRecording() => waveIn?.StopRecording();

    private byte[] ProcessAudioBuffer(byte[] inputBuffer, int bytesRecorded, int targetBitDepth)
    {
        if (targetBitDepth <= 8)
        {
            int bytesPerInputSample = waveIn.WaveFormat.BitsPerSample / 8;
            int numSamples = bytesRecorded / bytesPerInputSample;
            byte[] outputBuffer = new byte[numSamples];
            int maxValOutput = (1 << targetBitDepth) - 1;

            for (int i = 0; i < numSamples; i++)
            {
                short sample16Bit = BitConverter.ToInt16(inputBuffer, i * bytesPerInputSample);
                float norm = (sample16Bit + 32768f) / 65535f;
                int quantizedVal = (int)(norm * maxValOutput);
                outputBuffer[i] = (byte)quantizedVal;
            }
            return outputBuffer;
        }
        else if (targetBitDepth == 16)
        {
            return inputBuffer;
        }
        else if (targetBitDepth == 24)
        {
            int bytesPerInputSample = waveIn.WaveFormat.BitsPerSample / 8;
            int numSamples = bytesRecorded / bytesPerInputSample;
            byte[] outputBuffer = new byte[numSamples * 3];

            for (int i = 0; i < numSamples; i++)
            {
                short sample16Bit = BitConverter.ToInt16(inputBuffer, i * bytesPerInputSample);
                int sample24Bit = sample16Bit << 8;

                outputBuffer[i * 3] = (byte)(sample24Bit & 0xFF);         
                outputBuffer[i * 3 + 1] = (byte)((sample24Bit >> 8) & 0xFF); 
                outputBuffer[i * 3 + 2] = (byte)((sample24Bit >> 16) & 0xFF);
            }
            return outputBuffer;
        }
        else
        {
            Console.WriteLine($"\nOstrzeżenie: Nieobsługiwana docelowa głębia bitowa: {targetBitDepth}. Dane mogą być nieprawidłowe.");
            return new byte[0];
        }
    }


    private byte[] BuildWav(byte[] data, int sampleRate, int bitDepth, int channels)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        int bytesPerSampleOutput = (bitDepth + 7) / 8;
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