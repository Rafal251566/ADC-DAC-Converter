using System;
using System.IO;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.Generic;
using NAudio.Wave.SampleProviders;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Witaj w programie do konwersji A/C i C/A!");
        Console.WriteLine("-----------------------------------------");

        string choice;
        do
        {
            Console.WriteLine("\nDostępne opcje:");
            Console.WriteLine("1. Nagrywanie dźwięku (A/C)");
            Console.WriteLine("2. Odtwarzanie dźwięku (C/A)");
            Console.WriteLine("3. Nagrywanie porównawcze (wiele formatów z jednego nagrania)");
            Console.WriteLine("4. Wyjście");

            Console.Write("\nWybierz opcję: ");
            choice = Console.ReadLine();

            switch (choice)
            {
                case "1": PerformRecording(); break;
                case "2": PerformPlayback(); break;
                case "3": PerformComparativeRecording(); break;
                case "4": Console.WriteLine("Do zobaczenia!"); break;
                default: Console.WriteLine("Niepoprawny wybór, spróbuj ponownie."); break;
            }
        } while (choice != "4");
    }

    static void PerformRecording()
    {
        Console.WriteLine("\n--- NAGRYWANIE DŹWIĘKU ---");

        var deviceEnum = new MMDeviceEnumerator();
        var devices = deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

        if (devices.Count == 0)
        {
            Console.WriteLine("Brak dostępnych urządzeń wejściowych audio. Upewnij się, że mikrofon jest podłączony i aktywny.");
            return;
        }

        Console.WriteLine("Dostępne urządzenia wejściowe:");
        int deviceIndex = 0;
        foreach (var device in devices)
        {
            Console.WriteLine($"{deviceIndex}: {device.FriendlyName}");
            deviceIndex++;
        }

        int selectedDeviceIndex = 0;
        Console.Write($"Wybierz numer urządzenia (domyślnie 0 - {devices[0].FriendlyName}): ");
        if (!int.TryParse(Console.ReadLine(), out selectedDeviceIndex) || selectedDeviceIndex < 0 || selectedDeviceIndex >= devices.Count)
        {
            selectedDeviceIndex = 0;
        }

        Console.Write("Podaj nazwę pliku wyjściowego WAV (np. nagranie.wav): ");
        string filePath = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            filePath = "nagranie.wav";
        }

        Console.Write("Podaj częstotliwość próbkowania (np. 44100): ");
        int sampleRate;
        if (!int.TryParse(Console.ReadLine(), out sampleRate) || sampleRate <= 0) sampleRate = 44100;

        Console.Write("Podaj liczbę kanałów (1=mono, 2=stereo): ");
        int channels;
        if (!int.TryParse(Console.ReadLine(), out channels) || (channels != 1 && channels != 2)) channels = 1;

        Console.Write("Podaj docelową głębię bitową (2, 4, 8, 16, 24): "); 
        int bitDepth;
        if (!int.TryParse(Console.ReadLine(), out bitDepth)) bitDepth = 16;

        List<int> supportedBitDepths = new List<int> { 2, 4, 8, 16, 24 };
        if (!supportedBitDepths.Contains(bitDepth))
        {
            Console.WriteLine("Wybrano nieobsługiwaną głębię bitową. Używam domyślnej 16 bitów.");
            bitDepth = 16;
        }
        else if (bitDepth < 8)
        {
            Console.WriteLine($"\n--- WAŻNE: Głębia bitowa {bitDepth}-bitowa jest niestandardowa dla formatu WAV PCM. ---");
            Console.WriteLine("Pliki takie mogą nie być odtwarzane poprawnie przez standardowe odtwarzacze.");
            Console.WriteLine("Do odtwarzania tych plików użyj opcji '2. Odtwarzanie dźwięku' w tym programie.");
        }


        var recorder = new AdcConverter();
        try
        {
            recorder.StartRecording(filePath, sampleRate, bitDepth, channels, selectedDeviceIndex, true);
            Console.WriteLine("Nagrywanie... Wciśnij dowolny klawisz, by zakończyć.");
            Console.ReadKey();
            recorder.StopRecording();
            Console.WriteLine($"Nagrywanie zakończone. Plik zapisany jako: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nWystąpił błąd podczas nagrywania: {ex.Message}");
            Console.WriteLine("Upewnij się, że wybrane urządzenie jest dostępne i program ma uprawnienia dostępu do mikrofonu.");
        }
    }

    static void PerformComparativeRecording()
    {
        Console.WriteLine("\n--- NAGRYWANIE PORÓWNAWCZE ---");

        var deviceEnum = new MMDeviceEnumerator();
        var devices = deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

        if (devices.Count == 0)
        {
            Console.WriteLine("Brak dostępnych urządzeń wejściowych audio. Upewnij się, że mikrofon jest podłączony i aktywny.");
            return;
        }

        Console.WriteLine("Dostępne urządzenia wejściowe:");
        int deviceIndex = 0;
        foreach (var device in devices)
        {
            Console.WriteLine($"{deviceIndex}: {device.FriendlyName}");
            deviceIndex++;
        }

        int selectedDeviceIndex = 0;
        Console.Write($"Wybierz numer urządzenia (domyślnie 0 - {devices[0].FriendlyName}): ");
        if (!int.TryParse(Console.ReadLine(), out selectedDeviceIndex) || selectedDeviceIndex < 0 || selectedDeviceIndex >= devices.Count)
        {
            selectedDeviceIndex = 0;
        }

        Console.Write("Podaj bazową nazwę pliku wyjściowego (np. 'porownanie', pliki będą miały nazwy 'porownanie_44100_16bit.wav'): ");
        string baseFileName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(baseFileName))
        {
            baseFileName = "porownanie";
        }

        var sampleRates = new int[] { 2000, 4000, 8000, 16000, 22050, 44100 };
        var bitDepths = new int[] { 2, 4, 8, 16, 24 }; 

        int baseChannels = 1;
        int referenceSampleRate = 44100;
        int referenceBitDepth = 24;

        Console.WriteLine("\n--- WAŻNE: Pliki z głębią bitową 2-bit lub 4-bit są niestandardowe dla formatu WAV PCM. ---");
        Console.WriteLine("Mogą nie być odtwarzane poprawnie przez standardowe odtwarzacze.");
        Console.WriteLine("Do odtwarzania tych plików użyj opcji '2. Odtwarzanie dźwięku' w tym programie.");
        Thread.Sleep(2000);

        var recorder = new AdcConverter();
        byte[] rawRecordedData = null;
        string referenceFilePath = null;

        try
        {
            recorder.StartRecording("temp_raw_recording.wav", referenceSampleRate, 16, baseChannels, selectedDeviceIndex, false);
            Console.WriteLine("Rozpoczynam nagrywanie bazowe... Wciśnij dowolny klawisz, by zakończyć.");
            Console.ReadKey();
            rawRecordedData = recorder.StopRecordingAndReturnData();
            Console.WriteLine("Nagrywanie bazowe zakończone.");

            if (rawRecordedData != null && rawRecordedData.Length > 0)
            {
                referenceFilePath = $"{baseFileName}_REF_{referenceSampleRate}Hz_{referenceBitDepth}bit.wav";
                Console.Write($"Generuję plik REFERENCYJNY: {referenceFilePath}... ");
                byte[] referenceWavData = recorder.ProcessAndBuildWav(rawRecordedData, referenceSampleRate, referenceBitDepth, baseChannels);
                File.WriteAllBytes(referenceFilePath, referenceWavData);
                Console.WriteLine("Zapisano.");

                Console.WriteLine("\nRozpoczynam generowanie plików z różnymi formatami i obliczanie SNR:");
                foreach (var sr in sampleRates)
                {
                    foreach (var bd in bitDepths)
                    {
                        string outputFileName = $"{baseFileName}_{sr}Hz_{bd}bit.wav";
                        Console.Write($"Generuję plik: {outputFileName}... ");

                        try
                        {
                            byte[] wavData = recorder.ProcessAndBuildWav(rawRecordedData, sr, bd, baseChannels);
                            File.WriteAllBytes(outputFileName, wavData);

                            Console.WriteLine("Zapisano.");

                            if (outputFileName != referenceFilePath)
                            {
                                try
                                {
                                    double snr = CalculateSNR(referenceFilePath, outputFileName);
                                    Console.WriteLine($"   SNR dla {outputFileName}: {snr:F2} dB");
                                }
                                catch (Exception snrEx)
                                {
                                    Console.WriteLine($"   Błąd podczas obliczania SNR dla {outputFileName}: {snrEx.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Błąd podczas generowania {outputFileName}: {ex.Message}");
                        }
                    }
                }
                Console.WriteLine("\nGenerowanie wszystkich plików porównawczych i obliczanie SNR zakończone.");
            }
            else
            {
                Console.WriteLine("Brak danych do przetworzenia po nagraniu bazowym.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nWystąpił błąd podczas nagrywania porównawczego: {ex.Message}");
            Console.WriteLine("Upewnij się, że wybrane urządzenie jest dostępne i program ma uprawnienia dostępu do mikrofonu.");
        }
    }

    static void PerformPlayback()
    {
        Console.WriteLine("\n--- ODTWARZANIE DŹWIĘKU ---");
        Console.Write("Podaj nazwę pliku WAV do odtworzenia: ");
        string filePath = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine("Plik nie istnieje lub nazwa jest nieprawidłowa.");
            return;
        }

        using (var dac = new DacConverter())
        {
            try
            {
                dac.PlaySound(filePath);
                Console.WriteLine("Odtwarzanie... Wciśnij 's', aby zatrzymać.");

                while (dac.PlaybackState == PlaybackState.Playing)
                {
                    Console.Write($"\rPostęp: {dac.CurrentTime.ToString(@"mm\:ss")} / {dac.TotalTime.ToString(@"mm\:ss")}        ");
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.S)
                    {
                        dac.StopPlaying();
                        break;
                    }
                    Thread.Sleep(100);
                }
                Console.WriteLine("\nOdtwarzanie zakończone lub zatrzymane.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nWystąpił błąd podczas odtwarzania: {ex.Message}");
                Console.WriteLine("Sprawdź, czy plik WAV jest poprawny i nie jest uszkodzony.");
            }
        }
    }

    static double CalculateSNR(string referenceFilePath, string degradedFilePath)
    {
        if (!File.Exists(referenceFilePath)) throw new FileNotFoundException($"Plik referencyjny nie znaleziony: {referenceFilePath}");
        if (!File.Exists(degradedFilePath)) throw new FileNotFoundException($"Plik zdegradowany nie znaleziony: {degradedFilePath}");

        WaveFormat refFormat = null;
        WaveFormat degFormat = null;

        using (var dacTemp = new DacConverter())
        {
            refFormat = dacTemp.GetWaveFormat(referenceFilePath);
            degFormat = dacTemp.GetWaveFormat(degradedFilePath);
        }

        if (refFormat == null || degFormat == null)
        {
            throw new InvalidDataException("Nie można odczytać nagłówka WAV dla jednego z plików.");
        }

        if (refFormat.Channels != degFormat.Channels)
        {
            throw new ArgumentException("Pliki muszą mieć tę samą liczbę kanałów do obliczenia SNR.");
        }

        ISampleProvider referenceProvider = null;
        ISampleProvider degradedProvider = null;

        if (refFormat.BitsPerSample == 8 || refFormat.BitsPerSample == 16 || refFormat.BitsPerSample == 24 || refFormat.BitsPerSample == 32)
        {
            referenceProvider = new AudioFileReader(referenceFilePath).ToSampleProvider();
        }
        else
        {
            referenceProvider = new SampleProvider8BitToFloat(new VariableBitWaveProvider(referenceFilePath, refFormat.SampleRate, refFormat.Channels, refFormat.BitsPerSample));
        }

        if (degFormat.BitsPerSample == 8 || degFormat.BitsPerSample == 16 || degFormat.BitsPerSample == 24 || degFormat.BitsPerSample == 32)
        {
            degradedProvider = new AudioFileReader(degradedFilePath).ToSampleProvider();
        }
        else
        {
            degradedProvider = new SampleProvider8BitToFloat(new VariableBitWaveProvider(degradedFilePath, degFormat.SampleRate, degFormat.Channels, degFormat.BitsPerSample));
        }


        using (referenceProvider as IDisposable)
        using (degradedProvider as IDisposable)
        {
            if (referenceProvider.WaveFormat.SampleRate != degradedProvider.WaveFormat.SampleRate)
            {
                var resampler = new WdlResamplingSampleProvider(degradedProvider, referenceProvider.WaveFormat.SampleRate);
                degradedProvider = resampler;
            }

            int bufferSize = 1024 * referenceProvider.WaveFormat.Channels;
            float[] refBuffer = new float[bufferSize];
            float[] degBuffer = new float[bufferSize];

            double signalPower = 0;
            double noisePower = 0;
            long totalSamples = 0;

            int refSamplesRead;
            int degSamplesRead;

            do
            {
                refSamplesRead = referenceProvider.Read(refBuffer, 0, bufferSize);
                degSamplesRead = degradedProvider.Read(degBuffer, 0, bufferSize);

                int samplesToProcess = Math.Min(refSamplesRead, degSamplesRead);

                for (int i = 0; i < samplesToProcess; i++)
                {
                    signalPower += refBuffer[i] * refBuffer[i];

                    float noiseSample = refBuffer[i] - degBuffer[i];
                    noisePower += noiseSample * noiseSample;
                }
                totalSamples += samplesToProcess;

            } while (refSamplesRead > 0 && degSamplesRead > 0);

            if (totalSamples == 0 || noisePower == 0)
            {
                if (totalSamples == 0) return double.NegativeInfinity;
                return double.PositiveInfinity;
            }

            double snr = 10 * Math.Log10(signalPower / noisePower);
            return snr;
        }
    }

    public class SampleProvider8BitToFloat : ISampleProvider
    {
        private readonly IWaveProvider sourceProvider;
        private readonly WaveFormat floatFormat;
        private readonly byte[] eightBitBuffer;

        public SampleProvider8BitToFloat(IWaveProvider sourceProvider)
        {
            if (sourceProvider.WaveFormat.BitsPerSample != 8)
            {
                throw new ArgumentException("SampleProvider8BitToFloat oczekuje 8-bitowego IWaveProvider.");
            }
            this.sourceProvider = sourceProvider;
            this.floatFormat = WaveFormat.CreateIeeeFloatWaveFormat(sourceProvider.WaveFormat.SampleRate, sourceProvider.WaveFormat.Channels);
            this.eightBitBuffer = new byte[sourceProvider.WaveFormat.AverageBytesPerSecond];
        }

        public WaveFormat WaveFormat => floatFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int bytesToRead = count;

            if (bytesToRead > eightBitBuffer.Length)
            {
                bytesToRead = eightBitBuffer.Length;
            }

            int bytesRead = sourceProvider.Read(eightBitBuffer, 0, bytesToRead);
            int samplesRead = bytesRead;

            for (int i = 0; i < samplesRead; i++)
            {
                buffer[offset + i] = (eightBitBuffer[i] - 128) / 128f;
            }
            return samplesRead;
        }
    }
}