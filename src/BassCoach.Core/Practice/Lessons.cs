using BassCoach.Core.Theory;

namespace BassCoach.Core.Practice;

public sealed record Lesson(
    string Id, string Title, string Goal, ExerciseKind Kind, Style Style,
    string Progression, int KeyPc, int Bpm, int Loops)
{
    public PracticePlan ToPlan()
    {
        var chart = ProgressionLibrary.Find(Progression).Build(KeyPc);
        return new PracticePlan(Style, chart, Kind, Bpm, Loops, LessonId: Id, Title: Title);
    }
}

/// <summary>The built-in course: each lesson unlocks after the previous one is passed (2+ stars).</summary>
public static class Curriculum
{
    private const int C = 0, D = 2, E = 4, F = 5, G = 7, A = 9;

    public static IReadOnlyList<Lesson> All { get; } = new[]
    {
        new Lesson("l01", "Meet the root", "Play the root of each chord on beat 1. Two chords, slow tempo.", ExerciseKind.Roots, Style.Rock, "Vamp I-IV", E, 60, 4),
        new Lesson("l02", "Roots in rock", "Follow a four-chord rock loop with roots on beat 1.", ExerciseKind.Roots, Style.Rock, "Rock I-IV-V-IV", A, 70, 3),
        new Lesson("l03", "Twelve-bar roots", "Roots through a full 12-bar blues shuffle.", ExerciseKind.Roots, Style.Blues, "12-bar blues", E, 70, 1),
        new Lesson("l04", "Root and fifth", "Root on beat 1, fifth on beat 3: the classic bass move.", ExerciseKind.RootAndFifth, Style.Pop, "Pop I-V-vi-IV", G, 75, 3),
        new Lesson("l05", "Chord tones", "Root on 1, then any chord tone on 2, 3 and 4.", ExerciseKind.ChordTones, Style.Pop, "Pop I-V-vi-IV", C, 75, 3),
        new Lesson("l06", "Bachata roots", "Roots over a simplified bachata groove (vi-IV-I-V).", ExerciseKind.Roots, Style.Bachata, "Bachata vi-IV-I-V", C, 100, 4),
        new Lesson("l07", "Merengue drive", "Root and fifth over a simplified merengue groove.", ExerciseKind.RootAndFifth, Style.Merengue, "Merengue I-IV-I-V", G, 100, 4),
        new Lesson("l08", "Salsa feel", "Chord tones through a minor salsa progression.", ExerciseKind.ChordTones, Style.Salsa, "Salsa i-iv-V7-i", A, 90, 3),
        new Lesson("l09", "Jazz ii-V-I", "Outline the changes with chord tones, including sevenths.", ExerciseKind.ChordTones, Style.Jazz, "Jazz ii-V-I", F, 80, 4),
        new Lesson("l10", "Free groove", "Improvise over a blues in A. Stay inside the chords and the scale.", ExerciseKind.FreeGroove, Style.Blues, "12-bar blues", A, 85, 2),
    };
}
