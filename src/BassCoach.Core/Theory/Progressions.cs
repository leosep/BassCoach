namespace BassCoach.Core.Theory;

public enum Style { Rock, Pop, Blues, Jazz, Bachata, Merengue, Salsa }

public sealed record ProgressionStep(int Semitones, ChordQuality Quality, int Bars = 1);

/// <summary>A sequence of bars (always 4/4, one chord per bar) in a concrete key.</summary>
public sealed class ChordChart
{
    public const int BeatsPerBar = 4;

    public ChordChart(string title, int tonicPc, bool minor, IReadOnlyList<Chord> bars)
    {
        if (bars.Count == 0) throw new ArgumentException("A chart needs at least one bar.", nameof(bars));
        Title = title;
        TonicPc = tonicPc;
        Minor = minor;
        Bars = bars;
    }

    public string Title { get; }
    public int TonicPc { get; }
    public bool Minor { get; }
    public IReadOnlyList<Chord> Bars { get; }
    public int BarCount => Bars.Count;
    public int TotalBeats => Bars.Count * BeatsPerBar;
    public bool UsesFlats => NoteMath.KeyUsesFlats(TonicPc, Minor);

    public Chord ChordAtBeat(double beatInLoop)
    {
        int bar = (int)Math.Floor(beatInLoop / BeatsPerBar);
        return Bars[Math.Clamp(bar, 0, Bars.Count - 1)];
    }
}

public sealed record ProgressionTemplate(string Name, Style Style, bool Minor, IReadOnlyList<ProgressionStep> Steps)
{
    public ChordChart Build(int tonicPc)
    {
        var bars = new List<Chord>();
        foreach (var step in Steps)
            for (int i = 0; i < step.Bars; i++)
                bars.Add(new Chord((tonicPc + step.Semitones) % 12, step.Quality));
        return new ChordChart(Name, tonicPc, Minor, bars);
    }
}

public static class ProgressionLibrary
{
    private static ProgressionStep S(int semitones, ChordQuality q, int bars = 1) => new(semitones, q, bars);

    public static IReadOnlyList<ProgressionTemplate> All { get; } = new[]
    {
        new ProgressionTemplate("Vamp I-IV", Style.Rock, false, new[] { S(0, ChordQuality.Major), S(5, ChordQuality.Major) }),
        new ProgressionTemplate("Rock I-IV-V-IV", Style.Rock, false, new[] { S(0, ChordQuality.Major), S(5, ChordQuality.Major), S(7, ChordQuality.Major), S(5, ChordQuality.Major) }),
        new ProgressionTemplate("Pop I-V-vi-IV", Style.Pop, false, new[] { S(0, ChordQuality.Major), S(7, ChordQuality.Major), S(9, ChordQuality.Minor), S(5, ChordQuality.Major) }),
        new ProgressionTemplate("12-bar blues", Style.Blues, false, new[]
        {
            S(0, ChordQuality.Dominant7, 4), S(5, ChordQuality.Dominant7, 2), S(0, ChordQuality.Dominant7, 2),
            S(7, ChordQuality.Dominant7), S(5, ChordQuality.Dominant7), S(0, ChordQuality.Dominant7), S(7, ChordQuality.Dominant7),
        }),
        new ProgressionTemplate("Jazz ii-V-I", Style.Jazz, false, new[] { S(2, ChordQuality.Minor7), S(7, ChordQuality.Dominant7), S(0, ChordQuality.Major7, 2) }),
        new ProgressionTemplate("Bachata vi-IV-I-V", Style.Bachata, false, new[] { S(9, ChordQuality.Minor), S(5, ChordQuality.Major), S(0, ChordQuality.Major), S(7, ChordQuality.Major) }),
        new ProgressionTemplate("Merengue I-IV-I-V", Style.Merengue, false, new[] { S(0, ChordQuality.Major), S(5, ChordQuality.Major), S(0, ChordQuality.Major), S(7, ChordQuality.Major) }),
        new ProgressionTemplate("Salsa i-iv-V7-i", Style.Salsa, true, new[] { S(0, ChordQuality.Minor), S(5, ChordQuality.Minor), S(7, ChordQuality.Dominant7), S(0, ChordQuality.Minor) }),
    };

    public static ProgressionTemplate Find(string name) =>
        All.FirstOrDefault(t => t.Name == name) ?? throw new KeyNotFoundException($"Unknown progression '{name}'.");

    public static IEnumerable<ProgressionTemplate> For(Style style) => All.Where(t => t.Style == style);

    private static readonly Dictionary<int, (int Next, ChordQuality Quality)[]> MajorMoves = new()
    {
        [0] = new[] { (5, ChordQuality.Major), (7, ChordQuality.Major), (9, ChordQuality.Minor), (2, ChordQuality.Minor) },
        [5] = new[] { (7, ChordQuality.Major), (0, ChordQuality.Major), (2, ChordQuality.Minor) },
        [7] = new[] { (0, ChordQuality.Major), (9, ChordQuality.Minor), (5, ChordQuality.Major) },
        [9] = new[] { (5, ChordQuality.Major), (2, ChordQuality.Minor), (7, ChordQuality.Major) },
        [2] = new[] { (7, ChordQuality.Major), (5, ChordQuality.Major) },
    };

    private static readonly Dictionary<int, (int Next, ChordQuality Quality)[]> MinorMoves = new()
    {
        [0] = new[] { (5, ChordQuality.Minor), (8, ChordQuality.Major), (10, ChordQuality.Major), (7, ChordQuality.Major) },
        [5] = new[] { (10, ChordQuality.Major), (0, ChordQuality.Minor), (7, ChordQuality.Major) },
        [8] = new[] { (10, ChordQuality.Major), (3, ChordQuality.Major), (0, ChordQuality.Minor) },
        [10] = new[] { (0, ChordQuality.Minor), (8, ChordQuality.Major), (3, ChordQuality.Major) },
        [3] = new[] { (8, ChordQuality.Major), (10, ChordQuality.Major) },
        [7] = new[] { (0, ChordQuality.Minor) },
    };

    /// <summary>Builds a random but musically sensible progression that starts on the tonic and ends on the dominant.</summary>
    public static ChordChart Generate(Random rng, int tonicPc, bool minor, int bars = 4)
    {
        if (bars < 2) throw new ArgumentOutOfRangeException(nameof(bars));
        var moves = minor ? MinorMoves : MajorMoves;
        var tonic = new ProgressionStep(0, minor ? ChordQuality.Minor : ChordQuality.Major);
        var steps = new List<ProgressionStep> { tonic };
        while (steps.Count < bars - 1)
        {
            var options = moves[steps[^1].Semitones];
            var (next, quality) = options[rng.Next(options.Length)];
            steps.Add(new ProgressionStep(next, quality));
        }
        const int dominant = 7;
        bool alreadyDominant = steps[^1].Semitones == dominant;
        steps.Add(alreadyDominant ? tonic : new ProgressionStep(dominant, ChordQuality.Major));
        var chords = steps.Select(s => new Chord((tonicPc + s.Semitones) % 12, s.Quality)).ToList();
        return new ChordChart("Generated progression", tonicPc, minor, chords);
    }
}
