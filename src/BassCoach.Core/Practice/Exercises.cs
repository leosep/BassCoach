using BassCoach.Core.Theory;

namespace BassCoach.Core.Practice;

public enum ExerciseKind { Roots, RootAndFifth, ChordTones, FreeGroove }

public static class ExerciseInfo
{
    public static string Title(ExerciseKind kind) => kind switch
    {
        ExerciseKind.Roots => "Roots on beat 1",
        ExerciseKind.RootAndFifth => "Root and fifth",
        ExerciseKind.ChordTones => "Chord tones on every beat",
        _ => "Free groove",
    };

    public static string Instruction(ExerciseKind kind) => kind switch
    {
        ExerciseKind.Roots => "Play the root of each chord on beat 1 of the bar.",
        ExerciseKind.RootAndFifth => "Play the root on beat 1 and the fifth on beat 3.",
        ExerciseKind.ChordTones => "Play the root on beat 1, then any chord tone on beats 2, 3 and 4.",
        _ => "Play any groove you like. Notes that fit the chord or its scale score; others do not.",
    };
}

/// <summary>A note the player is expected to play at a given beat.</summary>
public sealed record Target(double Beat, Chord Chord, IReadOnlyList<int> AllowedPitchClasses, string Label);

public sealed record PracticePlan(
    Style Style, ChordChart Chart, ExerciseKind Kind, int Bpm, int Loops,
    int CountInBars = 1, string? LessonId = null, string Title = "Free practice")
{
    public int SongBeats => Chart.TotalBeats * Loops;

    public IReadOnlyList<Target> BuildTargets()
    {
        var targets = new List<Target>();
        if (Kind == ExerciseKind.FreeGroove) return targets;

        for (int loop = 0; loop < Loops; loop++)
            for (int bar = 0; bar < Chart.BarCount; bar++)
            {
                double start = (loop * Chart.BarCount + bar) * ChordChart.BeatsPerBar;
                var chord = Chart.Bars[bar];
                var root = new[] { chord.RootPc };
                switch (Kind)
                {
                    case ExerciseKind.Roots:
                        targets.Add(new Target(start, chord, root, "root"));
                        break;
                    case ExerciseKind.RootAndFifth:
                        targets.Add(new Target(start, chord, root, "root"));
                        targets.Add(new Target(start + 2, chord, new[] { (chord.RootPc + chord.FifthInterval) % 12 }, "fifth"));
                        break;
                    case ExerciseKind.ChordTones:
                        targets.Add(new Target(start, chord, root, "root"));
                        for (int beat = 1; beat < ChordChart.BeatsPerBar; beat++)
                            targets.Add(new Target(start + beat, chord, chord.ChordTonePcs.ToArray(), "chord tone"));
                        break;
                }
            }
        return targets;
    }
}
