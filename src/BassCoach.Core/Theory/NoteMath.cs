namespace BassCoach.Core.Theory;

/// <summary>Conversions between frequency, MIDI numbers and note names.</summary>
public static class NoteMath
{
    private static readonly string[] SharpNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private static readonly string[] FlatNames = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };
    private static readonly HashSet<int> FlatMajorKeys = new() { 1, 3, 5, 6, 8, 10 };
    private static readonly HashSet<int> FlatMinorKeys = new() { 0, 2, 3, 5, 7, 10 };

    public static double FrequencyToMidi(double hz) => 69.0 + 12.0 * Math.Log2(hz / 440.0);

    public static double MidiToFrequency(double midi) => 440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0);

    public static int PitchClass(int midi) => ((midi % 12) + 12) % 12;

    public static string Name(int pitchClass, bool flats = false) => (flats ? FlatNames : SharpNames)[PitchClass(pitchClass)];

    public static string NameWithOctave(int midi, bool flats = false) => $"{Name(midi, flats)}{(midi / 12) - 1}";

    public static bool KeyUsesFlats(int tonicPc, bool minor) => (minor ? FlatMinorKeys : FlatMajorKeys).Contains(PitchClass(tonicPc));
}
