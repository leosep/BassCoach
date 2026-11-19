namespace BassCoach.SelfTest;

/// <summary>Tiny dependency-free test runner (the build environment has no access to xUnit packages).</summary>
public static class T
{
    private static int _passed;
    private static readonly List<string> Failures = new();

    public static void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"  ok   {name}");
        }
        catch (Exception ex)
        {
            Failures.Add($"{name}: {ex.Message}");
            Console.WriteLine($"  FAIL {name}\n       {ex.Message}");
        }
    }

    public static void True(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Equal<TValue>(TValue expected, TValue actual, string what)
    {
        if (!EqualityComparer<TValue>.Default.Equals(expected, actual))
            throw new Exception($"{what}: expected {expected}, got {actual}");
    }

    public static void Near(double expected, double actual, double tolerance, string what)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new Exception($"{what}: expected {expected:0.###} ±{tolerance}, got {actual:0.###}");
    }

    public static int Finish()
    {
        Console.WriteLine($"\n{_passed} passed, {Failures.Count} failed");
        return Failures.Count == 0 ? 0 : 1;
    }
}

/// <summary>Synthetic bass guitar audio for tests.</summary>
public static class SynthBass
{
    public static float[] Note(int sampleRate, double hz, double seconds, double amplitude = 0.5, double decayPerSecond = 1.2)
    {
        var samples = new float[(int)(seconds * sampleRate)];
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)sampleRate;
            double envelope = Math.Min(1.0, t / 0.005) * Math.Exp(-t * decayPerSecond);
            double wave = Math.Sin(2 * Math.PI * hz * t) + 0.5 * Math.Sin(4 * Math.PI * hz * t) + 0.3 * Math.Sin(6 * Math.PI * hz * t) + 0.15 * Math.Sin(8 * Math.PI * hz * t);
            samples[i] = (float)(wave / 1.95 * envelope * amplitude);
        }
        return samples;
    }

    /// <summary>Mixes notes (start second, MIDI number, length) into one buffer.</summary>
    public static float[] Mix(int sampleRate, double totalSeconds, IEnumerable<(double Start, int Midi, double Length)> notes)
    {
        var buffer = new float[(int)(totalSeconds * sampleRate)];
        foreach (var (start, midi, length) in notes)
        {
            var note = Note(sampleRate, BassCoach.Core.Theory.NoteMath.MidiToFrequency(midi), length);
            int offset = (int)(start * sampleRate);
            for (int i = 0; i < note.Length && offset + i < buffer.Length; i++) buffer[offset + i] += note[i];
        }
        return buffer;
    }
}
