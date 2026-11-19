using BassCoach.Core.Theory;

namespace BassCoach.Core.Audio;

/// <summary>
/// One bar of 16 sixteenth-note steps per instrument ('x' = hit). These are simplified practice grooves,
/// not authentic transcriptions of each genre.
/// </summary>
public sealed record StylePattern(
    string DisplayName, int DefaultBpm, string Kick, string Snare, string Hat, string Perc,
    string Comp, double CompDecay, string BassGuide, double Swing);

public static class StyleCatalog
{
    private static readonly Dictionary<Style, StylePattern> Patterns = new()
    {
        [Style.Rock] = new("Rock", 100, "x.......x.x.....", "....x.......x...", "x.x.x.x.x.x.x.x.", "................", "x.......x.......", 2.5, "x.......x.......", 0),
        [Style.Pop] = new("Pop", 100, "x.......x.x.....", "....x.......x...", "x.x.x.x.x.x.x.x.", "................", "x.....x.x.......", 3.0, "x.......x.......", 0),
        [Style.Blues] = new("Blues shuffle", 90, "x.......x.......", "....x.......x...", "x.x.x.x.x.x.x.x.", "................", "x.......x.......", 2.5, "x.......x.......", 0.9),
        [Style.Jazz] = new("Jazz swing", 120, "x...........x...", "................", "x.....x.x.....x.", "................", "..x.......x.....", 4.0, "x.......x.......", 0.75),
        [Style.Bachata] = new("Bachata (simplified)", 125, "x...x...x...x...", "................", "x.x.x.x.x.x.x.x.", "..x...x...x...x.", "x.x...x.x.x...x.", 6.0, "x.......x.......", 0),
        [Style.Merengue] = new("Merengue (simplified)", 130, "x...x...x...x...", "..x...x...x...x.", "x.x.x.x.x.x.x.x.", "................", "x.x.x.x.x.x.x.x.", 9.0, "x...x...x...x...", 0),
        [Style.Salsa] = new("Salsa (simplified)", 110, "x.......x.......", "................", "x.x.x.x.x.x.x.x.", "x..x..x.x..x..x.", "..x...x...x...x.", 6.0, "x.......x.......", 0),
    };

    public static StylePattern Get(Style style) => Patterns[style];

    public static IReadOnlyList<Style> All { get; } = Enum.GetValues<Style>();
}
