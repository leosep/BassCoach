using System.Windows.Input;
using BassCoach.Core.Practice;

namespace BassCoach.Presentation;

public enum Page { Lessons, Practice, Progress, Settings }

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private ViewModelBase _current;
    private Page _page = Page.Lessons;

    public MainViewModel(AppServices app)
    {
        Practice = new PracticeViewModel(app);
        Lessons = new LessonsViewModel(app, StartLesson, StartFreePractice);
        Progress = new ProgressViewModel(app);
        Settings = new SettingsViewModel(app);
        _current = Lessons;

        Practice.SessionCompleted += () => { Lessons.Refresh(); Progress.Refresh(); };
        Practice.NextLessonRequested += StartLesson;
        NavigateCommand = new RelayCommand(p => { if (p is Page page) Navigate(page); });
    }

    public PracticeViewModel Practice { get; }
    public LessonsViewModel Lessons { get; }
    public ProgressViewModel Progress { get; }
    public SettingsViewModel Settings { get; }
    public ICommand NavigateCommand { get; }

    public ViewModelBase CurrentPage { get => _current; private set => Set(ref _current, value); }

    public Page CurrentPageKind { get => _page; private set { if (Set(ref _page, value)) { Raise(nameof(IsLessons)); Raise(nameof(IsPractice)); Raise(nameof(IsProgress)); Raise(nameof(IsSettings)); } } }

    public bool IsLessons => _page == Page.Lessons;
    public bool IsPractice => _page == Page.Practice;
    public bool IsProgress => _page == Page.Progress;
    public bool IsSettings => _page == Page.Settings;

    public void Navigate(Page page)
    {
        if (page != Page.Practice) Practice.Stop();
        if (page != Page.Settings) Settings.StopMonitor();
        if (page == Page.Progress) Progress.Refresh();
        if (page == Page.Lessons) Lessons.Refresh();
        if (page == Page.Settings) Settings.Reload();
        CurrentPageKind = page;
        CurrentPage = page switch
        {
            Page.Lessons => Lessons,
            Page.Practice => Practice,
            Page.Progress => Progress,
            _ => Settings,
        };
    }

    private void StartLesson(Lesson lesson)
    {
        Practice.LoadLesson(lesson);
        Navigate(Page.Practice);
    }

    private void StartFreePractice() => Navigate(Page.Practice);

    /// <summary>Forwarded from the view's UI timer (about 30 Hz).</summary>
    public void Tick()
    {
        Practice.Tick();
        Settings.Tick();
    }

    public void Dispose()
    {
        Practice.Dispose();
        Settings.Dispose();
    }
}
