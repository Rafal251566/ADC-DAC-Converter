using NAudio.Wave;
using System;
using System.IO;
using System.Text;

public class VariableBitWaveProvider : IWaveProvider, IDisposable
{
    private readonly WaveFormat format;
    private readonly FileStream stream;
    private readonly int bitDepth;
    private long dataOffset;
    private long dataLength;
    private long currentDataPosition;

    public TimeSpan CurrentTime => TimeSpan.FromSeconds((double)currentDataPosition / (format.SampleRate * format.Channels * ((bitDepth + 7) / 8)));
    public TimeSpan TotalTime => TimeSpan.FromSeconds((double)dataLength / (format.SampleRate * format.Channels * ((bitDepth + 7) / 8)));


    public VariableBitWaveProvider(string path, int rate, int channels, int bits)
    {
        bitDepth = bits;
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
            stream.Seek(chunkSize + (chunkSize % 2), SeekOrigin.Current);
        }
        stream.Seek(dataOffset, SeekOrigin.Begin);
        currentDataPosition = 0;
    }

    public WaveFormat WaveFormat => format;

    public int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;
        long totalBitsToRead = (long)count * bitDepth;
        long totalBytesToReadFromStream = (totalBitsToRead + 7) / 8;

        byte[] rawInputBytes = new byte[totalBytesToReadFromStream];
        int actualBytesReadFromStream = stream.Read(rawInputBytes, 0, rawInputBytes.Length);

        if (actualBytesReadFromStream == 0) return 0;

        currentDataPosition += actualBytesReadFromStream;

        long bitsReadFromInput = 0;
        int outputBufferIndex = offset;

        int maxValInput = (1 << bitDepth) - 1;


        while (bitsReadFromInput + bitDepth <= actualBytesReadFromStream * 8 && outputBufferIndex < offset + count)
        {
            int currentSample = 0;
            for (int bit = 0; bit < bitDepth; bit++)
            {
                long globalBitIndex = bitsReadFromInput + bit;
                int byteIndex = (int)(globalBitIndex / 8);
                int bitInByte = (int)(globalBitIndex % 8);

                if (byteIndex < actualBytesReadFromStream)
                {
                    if (((rawInputBytes[byteIndex] >> bitInByte) & 1) == 1)
                    {
                        currentSample |= (1 << bit);
                    }
                }
                else
                {
                    break;
                }
            }

            float normalizedSample = (float)currentSample / maxValInput;

            buffer[outputBufferIndex] = (byte)(normalizedSample * 255);

            bitsReadFromInput += bitDepth;
            outputBufferIndex++;
            bytesRead++;
        }
        return bytesRead;
    }

    public void Dispose()
    {
        stream?.Dispose();
    }
}