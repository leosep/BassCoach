namespace BassCoach.Core.Practice;

/// <summary>Turns a session summary into short, concrete coaching tips.</summary>
public static class PracticeAdvisor
{
    public static IReadOnlyList<string> Tips(SessionSummary s, ExerciseKind kind)
    {
        var tips = new List<string>();
        if (s.NotesPlayed == 0)
        {
            tips.Add("I did not hear any notes. Check your input device and level in Settings, then try again.");
            return tips;
        }

        if (s.BiasMs > 35)
            tips.Add($"You tend to play late (about {s.BiasMs:0} ms). Lock in with the kick drum and aim to land slightly earlier.");
        else if (s.BiasMs < -35)
            tips.Add($"You tend to rush (about {-s.BiasMs:0} ms early). Relax and let the beat come to you.");

        double wrongRatio = s.NotesPlayed == 0 ? 0 : s.WrongNotes / (double)s.NotesPlayed;
        if (wrongRatio > 0.2)
            tips.Add("Several notes did not fit the chord. Lower the tempo by 10 BPM and use the fretboard map to find the chord tones before each change.");

        if (kind != ExerciseKind.FreeGroove && s.TargetsTotal > 0 && s.MissedTargets / (double)s.TargetsTotal > 0.25)
            tips.Add("You missed several notes. Watch the next-chord display and get ready one beat early.");

        if (s.ExtraNotes > s.TargetsHit && kind != ExerciseKind.FreeGroove)
            tips.Add("You played many extra notes. Keep it simple: land the target note cleanly first.");

        if (s.Stars >= 2)
            tips.Add(s.Stars == 3 ? "Excellent. Try this lesson again 10 BPM faster, or move on." : "Good work. The next lesson is unlocked.");
        else if (tips.Count == 0)
            tips.Add("Close. One more pass at the same tempo should do it.");

        return tips;
    }
}
