namespace BassCoach.Core.Theory;

/// <summary>Open-string MIDI notes, lowest string first.</summary>
public sealed record BassTuning(string Name, IReadOnlyList<int> OpenStringMidi)
{
    public static BassTuning Standard4 { get; } = new("4-string (E A D G)", new[] { 28, 33, 38, 43 });
    public static BassTuning Standard5 { get; } = new("5-string (B E A D G)", new[] { 23, 28, 33, 38, 43 });

    public int StringCount => OpenStringMidi.Count;

    /// <summary>Lowest frequency the pitch detector should look for (a semitone below the lowest open string).</summary>
    public double MinDetectFrequency => NoteMath.MidiToFrequency(OpenStringMidi[0] - 1.0);

    public static BassTuning ForStringCount(int strings) => strings >= 5 ? Standard5 : Standard4;
}

public sealed record FretMarker(int String, int Fret, int PitchClass, NoteRole Role);

public static class Fretboard
{
    /// <summary>All positions of the chord tones up to <paramref name="maxFret"/>. String 0 is the lowest string.</summary>
    public static IReadOnlyList<FretMarker> ChordToneMarkers(BassTuning tuning, Chord chord, int maxFret = 12)
    {
        var markers = new List<FretMarker>();
        for (int s = 0; s < tuning.StringCount; s++)
            for (int fret = 0; fret <= maxFret; fret++)
            {
                int pc = NoteMath.PitchClass(tuning.OpenStringMidi[s] + fret);
                var role = chord.RoleOf(pc);
                if (role is NoteRole.Root or NoteRole.Third or NoteRole.Fifth or NoteRole.Seventh)
                    markers.Add(new FretMarker(s, fret, pc, role));
            }
        return markers;
    }
}
