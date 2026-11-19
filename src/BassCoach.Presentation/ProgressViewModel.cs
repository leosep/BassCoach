using System.Collections.ObjectModel;

namespace BassCoach.Presentation;

public sealed record SessionRow(string When, string Title, string Accuracy, string Timing, string Stars);

public sealed class ProgressViewModel : ViewModelBase
{
    private readonly AppServices _app;
    private string _headline = "";

    public ProgressViewModel(AppServices app)
    {
        _app = app;
        Refresh();
    }

    public ObservableCollection<SessionRow> Sessions { get; } = new();

    public string Headline { get => _headline; private set => Set(ref _headline, value); }

    public bool IsEmpty => Sessions.Count == 0;

    public void Refresh()
    {
        var all = _app.Stats.Stats.Sessions;
        Sessions.Clear();
        foreach (var s in all.OrderByDescending(x => x.Utc).Take(50))
            Sessions.Add(new SessionRow(s.Utc.ToLocalTime().ToString("g"), s.Title, s.Accuracy.ToString("P0"),
                s.AverageTimingMs <= 0 ? "-" : $"{s.AverageTimingMs:0} ms", FeedbackFormatter.Stars(s.Stars)));

        Headline = all.Count == 0
            ? "No practice sessions yet."
            : $"{all.Count} {(all.Count == 1 ? "session" : "sessions")} - best accuracy {all.Max(x => x.Accuracy):P0} - {all.Sum(x => x.Notes)} notes played";
        Raise(nameof(IsEmpty));
    }
}
