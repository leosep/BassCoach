using BassCoach.Core.Practice;

namespace BassCoach.Core.Storage;

public sealed class SessionRecord
{
    public DateTime Utc { get; set; }
    public string? LessonId { get; set; }
    public string Title { get; set; } = "";
    public double Accuracy { get; set; }
    public double AverageTimingMs { get; set; }
    public double BiasMs { get; set; }
    public int Notes { get; set; }
    public int Stars { get; set; }
    public int Bpm { get; set; }
}

public sealed class LessonRecord
{
    public int Attempts { get; set; }
    public double BestAccuracy { get; set; }
    public int BestStars { get; set; }
}

public sealed class PracticeStats
{
    public List<SessionRecord> Sessions { get; set; } = new();
    public Dictionary<string, LessonRecord> Lessons { get; set; } = new();
}

/// <summary>Business rules around saved practice results.</summary>
public sealed class StatsService
{
    private const int MaxSessionsKept = 500;

    public StatsService(PracticeStats stats) => Stats = stats;

    public PracticeStats Stats { get; }

    public void Record(PracticePlan plan, SessionSummary summary, DateTime utcNow)
    {
        Stats.Sessions.Add(new SessionRecord
        {
            Utc = utcNow, LessonId = plan.LessonId, Title = plan.Title, Accuracy = summary.Accuracy,
            AverageTimingMs = summary.AverageTimingMs, BiasMs = summary.BiasMs, Notes = summary.NotesPlayed,
            Stars = summary.Stars, Bpm = plan.Bpm,
        });
        if (Stats.Sessions.Count > MaxSessionsKept) Stats.Sessions.RemoveRange(0, Stats.Sessions.Count - MaxSessionsKept);

        if (plan.LessonId is null) return;
        if (!Stats.Lessons.TryGetValue(plan.LessonId, out var record)) Stats.Lessons[plan.LessonId] = record = new LessonRecord();
        record.Attempts++;
        record.BestAccuracy = Math.Max(record.BestAccuracy, summary.Accuracy);
        record.BestStars = Math.Max(record.BestStars, summary.Stars);
    }

    public int BestStars(string lessonId) => Stats.Lessons.TryGetValue(lessonId, out var r) ? r.BestStars : 0;

    public bool IsPassed(string lessonId) => BestStars(lessonId) >= 2;

    public bool IsUnlocked(Lesson lesson)
    {
        int index = Curriculum.All.ToList().FindIndex(l => l.Id == lesson.Id);
        return index <= 0 || IsPassed(Curriculum.All[index - 1].Id);
    }

    public Lesson? NextLesson() => Curriculum.All.FirstOrDefault(l => !IsPassed(l.Id));
}
