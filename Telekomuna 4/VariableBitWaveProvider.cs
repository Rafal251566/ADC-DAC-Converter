using NAudio.Wave;
using System;
using System.IO;
using System.Text;

public class VariableBitWaveProvider : IWaveProvider, IDisposable
{
    private readonly WaveFormat format;
    private readonly FileStream stream;
    private readonly int actualBitDepth;
    private long dataOffset;
    private long dataLength;
    private long currentDataPosition;

    public TimeSpan CurrentTime => TimeSpan.FromSeconds((double)currentDataPosition / (format.SampleRate * format.Channels * ((actualBitDepth + 7) / 8)));
    public TimeSpan TotalTime => TimeSpan.FromSeconds((double)dataLength / (format.SampleRate * format.Channels * ((actualBitDepth + 7) / 8)));


    public VariableBitWaveProvider(string path, int rate, int channels, int bits)
    {
        actualBitDepth = bits;
        format = new WaveFormat(rate, 8, channels);
        stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);

        reader.ReadBytes(12);

        while (stream.Position < stream.Length)
        {
            string chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();
            if (chunkId == "data")
            {
                dataOffset = stream.Position;
                dataLength = chunkSize;
                break;
            }
            stream.Seek(chunkSize, SeekOrigin.Current);
        }
        stream.Seek(dataOffset, SeekOrigin.Begin);
        currentDataPosition = 0;
    }

    public WaveFormat WaveFormat => format;

    public int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;
        int samplesToRead = count;

        long totalInputBitsToRead = (long)count * actualBitDepth;
        int numInputBytesToRead = (int)((totalInputBitsToRead + 7) / 8);

        byte[] rawInputBytes = new byte[numInputBytesToRead];
        int actualBytesReadFromStream = stream.Read(rawInputBytes, 0, rawInputBytes.Length);

        if (actualBytesReadFromStream == 0) return 0;

        currentDataPosition += actualBytesReadFromStream;

        long bitsProcessedInInput = 0;

        float maxValInput = (float)((1 << actualBitDepth) - 1);

        for (int i = 0; i < samplesToRead; i++)
        {
            if (bitsProcessedInInput + actualBitDepth > actualBytesReadFromStream * 8)
            {
                break;
            }

            int currentSampleValue = 0;
            for (int bit = 0; bit < actualBitDepth; bit++)
            {
                long globalBitIndex = bitsProcessedInInput + bit;
                int byteIndex = (int)(globalBitIndex / 8);
                int bitInByte = (int)(globalBitIndex % 8);

                if (byteIndex < actualBytesReadFromStream)
                {
                    if (((rawInputBytes[byteIndex] >> bitInByte) & 1) == 1)
                    {
                        currentSampleValue |= (1 << bit);
                    }
                }
                else
                {
                    break;
                }
            }

            float normalizedSample = (maxValInput > 0) ? (float)currentSampleValue / maxValInput : 0f;
            buffer[offset + bytesRead] = (byte)(normalizedSample * 255);

            bitsProcessedInInput += actualBitDepth;
            bytesRead++;
        }

        return bytesRead;
    }

    public void Dispose()
    {
        stream?.Dispose();
    }
}