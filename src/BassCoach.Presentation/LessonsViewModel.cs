using System.Collections.ObjectModel;
using System.Windows.Input;
using BassCoach.Core.Practice;

namespace BassCoach.Presentation;

public sealed class LessonItem : ViewModelBase
{
    public LessonItem(Lesson lesson) => Lesson = lesson;

    public Lesson Lesson { get; }
    public string Number => Lesson.Id[1..].TrimStart('0');
    public string Title => Lesson.Title;
    public string Goal => Lesson.Goal;

    private int _stars;
    private bool _locked;
    private bool _isNext;

    public int Stars { get => _stars; set { if (Set(ref _stars, value)) { Raise(nameof(StarsText)); } } }
    public string StarsText => _locked ? "Locked" : FeedbackFormatter.Stars(_stars);
    public bool IsLocked { get => _locked; set { if (Set(ref _locked, value)) { Raise(nameof(StarsText)); Raise(nameof(IsUnlocked)); } } }
    public bool IsUnlocked => !_locked;
    public bool IsNext { get => _isNext; set => Set(ref _isNext, value); }
}

public sealed class LessonsViewModel : ViewModelBase
{
    private readonly AppServices _app;

    public LessonsViewModel(AppServices app, Action<Lesson> start, Action freePractice)
    {
        _app = app;
        foreach (var lesson in Curriculum.All) Items.Add(new LessonItem(lesson));
        StartCommand = new RelayCommand(p => { if (p is LessonItem { IsUnlocked: true } item) start(item.Lesson); }, p => p is LessonItem { IsUnlocked: true });
        FreePracticeCommand = new RelayCommand(freePractice);
        Refresh();
    }

    public ObservableCollection<LessonItem> Items { get; } = new();
    public ICommand StartCommand { get; }
    public ICommand FreePracticeCommand { get; }

    public string Summary
    {
        get
        {
            int passed = Curriculum.All.Count(l => _app.Stats.IsPassed(l.Id));
            return $"{passed} of {Curriculum.All.Count} lessons completed";
        }
    }

    public void Refresh()
    {
        var next = _app.Stats.NextLesson();
        foreach (var item in Items)
        {
            item.IsLocked = !_app.Stats.IsUnlocked(item.Lesson);
            item.Stars = _app.Stats.BestStars(item.Lesson.Id);
            item.IsNext = next?.Id == item.Lesson.Id;
        }
        Raise(nameof(Summary));
    }
}
