namespace BassCoach.Core.Theory;

public enum ChordQuality { Major, Minor, Dominant7, Minor7, Major7, Diminished }

public enum NoteRole { Root, Third, Fifth, Seventh, ScaleTone, Outside }

/// <summary>A chord described by its root pitch class and quality.</summary>
public sealed record Chord(int RootPc, ChordQuality Quality)
{
    private static readonly int[] Ionian = { 0, 2, 4, 5, 7, 9, 11 };
    private static readonly int[] Aeolian = { 0, 2, 3, 5, 7, 8, 10 };
    private static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
    private static readonly int[] Mixolydian = { 0, 2, 4, 5, 7, 9, 10 };
    private static readonly int[] Locrian = { 0, 1, 3, 5, 6, 8, 10 };

    public int ThirdInterval => Quality is ChordQuality.Minor or ChordQuality.Minor7 or ChordQuality.Diminished ? 3 : 4;

    public int FifthInterval => Quality == ChordQuality.Diminished ? 6 : 7;

    public int? SeventhInterval => Quality switch
    {
        ChordQuality.Dominant7 => 10,
        ChordQuality.Minor7 => 10,
        ChordQuality.Major7 => 11,
        _ => null,
    };

    private int[] ScaleIntervals => Quality switch
    {
        ChordQuality.Major => Ionian,
        ChordQuality.Major7 => Ionian,
        ChordQuality.Minor => Aeolian,
        ChordQuality.Minor7 => Dorian,
        ChordQuality.Dominant7 => Mixolydian,
        _ => Locrian,
    };

    /// <summary>Pitch classes of the chord tones (root, third, fifth and seventh when present).</summary>
    public IReadOnlyList<int> ChordTonePcs
    {
        get
        {
            var tones = new List<int> { RootPc, (RootPc + ThirdInterval) % 12, (RootPc + FifthInterval) % 12 };
            if (SeventhInterval is int seventh) tones.Add((RootPc + seventh) % 12);
            return tones;
        }
    }

    public NoteRole RoleOf(int pitchClass)
    {
        int interval = (((pitchClass - RootPc) % 12) + 12) % 12;
        if (interval == 0) return NoteRole.Root;
        if (interval == ThirdInterval) return NoteRole.Third;
        if (interval == FifthInterval) return NoteRole.Fifth;
        if (SeventhInterval == interval) return NoteRole.Seventh;
        return Array.IndexOf(ScaleIntervals, interval) >= 0 ? NoteRole.ScaleTone : NoteRole.Outside;
    }

    public string Symbol(bool flats = false)
    {
        string suffix = Quality switch
        {
            ChordQuality.Major => "",
            ChordQuality.Minor => "m",
            ChordQuality.Dominant7 => "7",
            ChordQuality.Minor7 => "m7",
            ChordQuality.Major7 => "maj7",
            _ => "dim",
        };
        return NoteMath.Name(RootPc, flats) + suffix;
    }
}
