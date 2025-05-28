using System;
using System.IO;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Witaj w programie do konwersji A/C i C/A!");
        Console.WriteLine("-----------------------------------------");

        Console.WriteLine("Dostępne opcje:");
        Console.WriteLine("1. Nagrywanie dźwięku (A/C)");
        Console.WriteLine("2. Odtwarzanie dźwięku (C/A)");
        Console.WriteLine("3. Wyjście");

        string choice;
        do
        {
            Console.Write("\nWybierz opcję: ");
            choice = Console.ReadLine();

            switch (choice)
            {
                case "1": PerformRecording(); break;
                case "2": PerformPlayback(); break;
                case "3": Console.WriteLine("Do zobaczenia!"); break;
                default: Console.WriteLine("Niepoprawny wybór, spróbuj ponownie."); break;
            }
        } while (choice != "3");
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

        Console.Write("Podaj docelową głębię bitową (1, 2, 4, 8, 16, 24): ");
        int bitDepth;
        if (!int.TryParse(Console.ReadLine(), out bitDepth)) bitDepth = 16;
        if (bitDepth > 8  && bitDepth != 16 && bitDepth != 24)
        {
            Console.WriteLine("Wybrano nieobsługiwaną głębię bitową. Używam domyślnej 16 bitów.");
            bitDepth = 16;
        }


        var recorder = new AdcConverter();
        try
        {
            recorder.StartRecording(filePath, sampleRate, bitDepth, channels, selectedDeviceIndex);
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
                    Console.Write($"\rPostęp: {dac.CurrentTime.ToString(@"mm\:ss")} / {dac.TotalTime.ToString(@"mm\:ss")}       ");
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
}