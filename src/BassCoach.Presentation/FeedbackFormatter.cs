using BassCoach.Core.Practice;
using BassCoach.Core.Theory;

namespace BassCoach.Presentation;

public enum FeedbackKind { None, Good, Warning, Bad }

/// <summary>Plain-English feedback for one detected note.</summary>
public static class FeedbackFormatter
{
    public static (string Text, FeedbackKind Kind) Describe(NoteResult r, bool flats)
    {
        string note = NoteMath.Name(NoteMath.PitchClass(r.Note.Midi), flats);
        string timing = r.Rating switch
        {
            TimingRating.Perfect => "right on the beat",
            TimingRating.Good => "close to the beat",
            TimingRating.Early => $"early by {Math.Abs(r.TimingMs):0} ms",
            _ => $"late by {Math.Abs(r.TimingMs):0} ms",
        };

        return r.Outcome switch
        {
            NoteOutcome.WrongNote => ($"{note}: not a note of {r.Chord.Symbol(flats)} here", FeedbackKind.Bad),
            NoteOutcome.Extra => ($"{note}: extra note ({RoleText(r.Role)})", FeedbackKind.None),
            _ => ($"{note}: {RoleText(r.Role)}, {timing}",
                  r.Rating is TimingRating.Perfect or TimingRating.Good ? FeedbackKind.Good : FeedbackKind.Warning),
        };
    }

    public static string RoleText(NoteRole role) => role switch
    {
        NoteRole.Root => "root",
        NoteRole.Third => "third",
        NoteRole.Fifth => "fifth",
        NoteRole.Seventh => "seventh",
        NoteRole.ScaleTone => "passing tone",
        _ => "outside the chord",
    };

    public static string Stars(int count) => new string('\u2605', count) + new string('\u2606', 3 - count);
}
