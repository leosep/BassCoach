using System.Collections.ObjectModel;
using System.Windows.Input;
using BassCoach.Core.Audio;
using BassCoach.Core.Practice;
using BassCoach.Core.Theory;

namespace BassCoach.Presentation;

public enum PracticeState { Setup, Running, Finished }

public sealed record KeyOption(string Name, int Pc);

public sealed record ExerciseOption(ExerciseKind Kind, string Title);

public sealed class BarItem : ViewModelBase
{
    private bool _isCurrent;
    public BarItem(string symbol) => Symbol = symbol;
    public string Symbol { get; }
    public bool IsCurrent { get => _isCurrent; set => Set(ref _isCurrent, value); }
}

public sealed class BeatDot : ViewModelBase
{
    private bool _isActive;
    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }
}

public sealed class PracticeViewModel : ViewModelBase, IDisposable
{
    public const string GeneratedName = "Generated progression";
    private readonly AppServices _app;
    private PracticeSession? _session;
    private PracticePlan? _plan;
    private IAudioInput? _input;
    private IAudioOutput? _output;
    private Lesson? _lesson;
    private SessionSummary? _lastSummary;
    private NoteResult? _shownResult;
    private string _shownChord = "";
    private bool _loading;

    private PracticeState _state;
    private Style _style = Style.Rock;
    private string _progression = "";
    private KeyOption _key;
    private ExerciseOption _exercise;
    private int _bpm = 80;
    private int _loops = 3;
    private bool _generatedMinor;
    private string _errorMessage = "";
    private string _chordText = "-";
    private string _nextChordText = "";
    private string _statusText = "";
    private string _feedbackText = "Play along when the music starts.";
    private FeedbackKind _feedbackKind;
    private string _accuracyText = "";
    private string _detectedText = "-";
    private double _cents;
    private double _level;
    private IReadOnlyList<FretMarker> _markers = Array.Empty<FretMarker>();
    private string _resultTitle = "";
    private string _starsText = "";
    private string _resultDetails = "";
    private Lesson? _nextLesson;

    public PracticeViewModel(AppServices app)
    {
        _app = app;
        Keys = new[]
        {
            new KeyOption("C", 0), new KeyOption("Db", 1), new KeyOption("D", 2), new KeyOption("Eb", 3), new KeyOption("E", 4), new KeyOption("F", 5),
            new KeyOption("F#", 6), new KeyOption("G", 7), new KeyOption("Ab", 8), new KeyOption("A", 9), new KeyOption("Bb", 10), new KeyOption("B", 11),
        };
        Exercises = Enum.GetValues<ExerciseKind>().Select(k => new ExerciseOption(k, ExerciseInfo.Title(k))).ToArray();
        _key = Keys[4];
        _exercise = Exercises[0];
        SelectStyle(Style.Rock);
        Bpm = StyleCatalog.Get(Style.Rock).DefaultBpm;

        StartCommand = new RelayCommand(Start);
        StopCommand = new RelayCommand(Stop);
        BackToSetupCommand = new RelayCommand(Stop);
        RetryCommand = new RelayCommand(Start);
        NextLessonCommand = new RelayCommand(() => { if (_nextLesson is not null) NextLessonRequested?.Invoke(_nextLesson); }, () => _nextLesson is not null);
        CalibrateCommand = new RelayCommand(CalibrateLatency, () => _lastSummary is { NotesPlayed: > 5 });
    }

    public event Action? SessionCompleted;
    public event Action<Lesson>? NextLessonRequested;

    // ---- setup ----
    public IReadOnlyList<Style> Styles => StyleCatalog.All;
    public IReadOnlyList<KeyOption> Keys { get; }
    public IReadOnlyList<ExerciseOption> Exercises { get; }
    public ObservableCollection<string> Progressions { get; } = new();
    public string StyleName(Style style) => StyleCatalog.Get(style).DisplayName;

    public Style SelectedStyle
    {
        get => _style;
        set { if (_style != value) { SelectStyle(value); if (!_loading) { _lesson = null; Bpm = StyleCatalog.Get(value).DefaultBpm; Raise(nameof(HeaderTitle)); } } }
    }

    public string SelectedProgression
    {
        get => _progression;
        set { if (value is null) return; if (Set(ref _progression, value)) { Raise(nameof(ShowMinorOption)); Detach(); } }
    }

    public KeyOption SelectedKey { get => _key; set { if (value is not null && Set(ref _key, value)) Detach(); } }

    public ExerciseOption SelectedExercise
    {
        get => _exercise;
        set { if (value is not null && Set(ref _exercise, value)) { Raise(nameof(ExerciseHint)); Detach(); } }
    }

    public int Bpm { get => _bpm; set { if (Set(ref _bpm, Math.Clamp(value, 40, 200))) Detach(); } }

    public int Loops { get => _loops; set { if (Set(ref _loops, Math.Clamp(value, 1, 8))) Detach(); } }

    public bool GeneratedMinor { get => _generatedMinor; set { if (Set(ref _generatedMinor, value)) Detach(); } }

    public bool ShowMinorOption => _progression == GeneratedName;

    public string ExerciseHint => ExerciseInfo.Instruction(_exercise.Kind);

    public string HeaderTitle => _lesson is null ? "Free practice" : $"Lesson: {_lesson.Title}";

    public string LessonGoal => _lesson?.Goal ?? "Pick a style, key and exercise, then press Start.";

    public string ErrorMessage { get => _errorMessage; private set { Set(ref _errorMessage, value); Raise(nameof(HasError)); } }

    public bool HasError => _errorMessage.Length > 0;

    // ---- running ----
    public PracticeState State
    {
        get => _state;
        private set { if (Set(ref _state, value)) { Raise(nameof(IsSetup)); Raise(nameof(IsRunning)); Raise(nameof(IsFinished)); } }
    }

    public bool IsSetup => _state == PracticeState.Setup;
    public bool IsRunning => _state == PracticeState.Running;
    public bool IsFinished => _state == PracticeState.Finished;

    public ObservableCollection<BarItem> Bars { get; } = new();
    public ObservableCollection<BeatDot> BeatDots { get; } = new(new[] { new BeatDot(), new BeatDot(), new BeatDot(), new BeatDot() });
    public string ChordText { get => _chordText; private set => Set(ref _chordText, value); }
    public string NextChordText { get => _nextChordText; private set => Set(ref _nextChordText, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string FeedbackText { get => _feedbackText; private set => Set(ref _feedbackText, value); }
    public FeedbackKind FeedbackKind { get => _feedbackKind; private set => Set(ref _feedbackKind, value); }
    public string AccuracyText { get => _accuracyText; private set => Set(ref _accuracyText, value); }
    public string DetectedText { get => _detectedText; private set => Set(ref _detectedText, value); }
    public double Cents { get => _cents; private set => Set(ref _cents, value); }
    public double Level { get => _level; private set => Set(ref _level, value); }
    public IReadOnlyList<FretMarker> FretMarkers { get => _markers; private set => Set(ref _markers, value); }
    public int StringCount => _app.Settings.StringCount >= 5 ? 5 : 4;

    // ---- results ----
    public string ResultTitle { get => _resultTitle; private set => Set(ref _resultTitle, value); }
    public string StarsText { get => _starsText; private set => Set(ref _starsText, value); }
    public string ResultDetails { get => _resultDetails; private set => Set(ref _resultDetails, value); }
    public ObservableCollection<string> Tips { get; } = new();
    public bool HasNextLesson => _nextLesson is not null;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand BackToSetupCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand NextLessonCommand { get; }
    public ICommand CalibrateCommand { get; }

    private void SelectStyle(Style style)
    {
        _style = style;
        Progressions.Clear();
        foreach (var template in ProgressionLibrary.For(style)) Progressions.Add(template.Name);
        Progressions.Add(GeneratedName);
        _progression = Progressions[0];
        Raise(nameof(SelectedStyle));
        Raise(nameof(SelectedProgression));
        Raise(nameof(ShowMinorOption));
    }

    private void Detach()
    {
        if (_loading || _lesson is null) return;
        _lesson = null;
        Raise(nameof(HeaderTitle));
        Raise(nameof(LessonGoal));
    }

    public void LoadLesson(Lesson lesson)
    {
        Stop();
        _loading = true;
        try
        {
            SelectStyle(lesson.Style);
            SelectedProgression = lesson.Progression;
            SelectedKey = Keys.First(k => k.Pc == lesson.KeyPc);
            SelectedExercise = Exercises.First(e => e.Kind == lesson.Kind);
            Bpm = lesson.Bpm;
            Loops = lesson.Loops;
        }
        finally { _loading = false; }
        _lesson = lesson;
        Raise(nameof(HeaderTitle));
        Raise(nameof(LessonGoal));
    }

    public PracticePlan BuildPlan()
    {
        var chart = _progression == GeneratedName
            ? ProgressionLibrary.Generate(_app.Random, _key.Pc, _generatedMinor, 4)
            : ProgressionLibrary.Find(_progression).Build(_key.Pc);
        return new PracticePlan(_style, chart, _exercise.Kind, _bpm, _loops, LessonId: _lesson?.Id, Title: _lesson?.Title ?? "Free practice");
    }

    // ---- session control ----
    private void Start()
    {
        StopAudio();
        ErrorMessage = "";
        try
        {
            _plan = BuildPlan();
            _input = _app.Audio.OpenInput(_app.Settings.InputDeviceId);
            _output = _app.Audio.OpenOutput(_app.Settings.OutputDeviceId);
            var session = new PracticeSession(_plan, _app.Settings, _input.SampleRate, _output.SampleRate);
            _session = session;
            _input.SamplesAvailable += (buffer, count) => session.FeedInput(buffer.AsSpan(0, count));
            _input.Start();
            _output.Play(session.Backing);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StopAudio();
            ErrorMessage = $"Could not start audio: {ex.Message} Check your devices in Settings.";
            return;
        }

        Bars.Clear();
        foreach (var chord in _plan.Chart.Bars) Bars.Add(new BarItem(chord.Symbol(_plan.Chart.UsesFlats)));
        _shownResult = null;
        _shownChord = "";
        FeedbackText = "Count-in... get ready.";
        FeedbackKind = FeedbackKind.None;
        AccuracyText = "";
        State = PracticeState.Running;
    }

    /// <summary>Stops everything and returns to the setup screen without saving a result.</summary>
    public void Stop()
    {
        StopAudio();
        if (_state != PracticeState.Setup) State = PracticeState.Setup;
    }

    private void StopAudio()
    {
        _input?.Dispose();
        _output?.Dispose();
        _input = null;
        _output = null;
        _session = null;
    }

    /// <summary>Called by the view about 30 times per second on the UI thread.</summary>
    public void Tick()
    {
        if (_state != PracticeState.Running || _session is null || _plan is null) return;
        var snapshot = _session.GetSnapshot();
        bool flats = snapshot.UsesFlats;

        string symbol = snapshot.CurrentChord.Symbol(flats);
        ChordText = symbol;
        NextChordText = "Next: " + snapshot.NextChord.Symbol(flats);
        if (symbol != _shownChord)
        {
            _shownChord = symbol;
            FretMarkers = Fretboard.ChordToneMarkers(BassTuning.ForStringCount(_app.Settings.StringCount), snapshot.CurrentChord);
        }

        int currentBar = snapshot.Phase == SessionPhase.Playing ? CurrentBarIndex(snapshot) : -1;
        for (int i = 0; i < Bars.Count; i++) Bars[i].IsCurrent = i == currentBar;
        for (int i = 0; i < BeatDots.Count; i++) BeatDots[i].IsActive = snapshot.Phase == SessionPhase.Playing && i == snapshot.BeatInBar;

        StatusText = snapshot.Phase == SessionPhase.CountIn
            ? $"Count-in: {snapshot.CountInBeatsLeft}"
            : $"Pass {Math.Min(snapshot.Loop, _plan.Loops)} of {_plan.Loops}";
        AccuracyText = $"Accuracy {snapshot.RunningAccuracy:P0}";
        Level = Math.Clamp(snapshot.InputLevel * 2, 0, 1);

        if (snapshot.Pitch.IsVoiced && snapshot.Pitch.Confidence >= 0.6)
        {
            double midi = NoteMath.FrequencyToMidi(snapshot.Pitch.FrequencyHz);
            int rounded = (int)Math.Round(midi);
            DetectedText = NoteMath.NameWithOctave(rounded, flats);
            Cents = Math.Clamp((midi - rounded) * 100, -50, 50);
        }
        else
        {
            DetectedText = "-";
            Cents = 0;
        }

        if (snapshot.LastResult is { } result && !ReferenceEquals(result, _shownResult))
        {
            _shownResult = result;
            (FeedbackText, FeedbackKind) = FeedbackFormatter.Describe(result, flats);
        }

        if (snapshot.Phase == SessionPhase.Finished) Finish();
    }

    private int CurrentBarIndex(PracticeSnapshot snapshot)
    {
        double songBeat = Math.Max(0, snapshot.Beat);
        double inLoop = songBeat % _plan!.Chart.TotalBeats;
        return Math.Min((int)(inLoop / ChordChart.BeatsPerBar), _plan.Chart.BarCount - 1);
    }

    private void Finish()
    {
        if (_session is null || _plan is null) return;
        var summary = _session.Summarize();
        var plan = _plan;
        StopAudio();
        _lastSummary = summary;

        try
        {
            _app.Stats.Record(plan, summary, _app.Clock());
            _app.SaveStats();
        }
        catch (IOException ex)
        {
            ErrorMessage = $"Your result could not be saved: {ex.Message}";
        }

        ResultTitle = summary.Passed ? "Lesson passed!" : (summary.NotesPlayed == 0 ? "No notes heard" : "Keep practicing");
        StarsText = FeedbackFormatter.Stars(summary.Stars);
        string timing = summary.AverageTimingMs <= 0 ? "n/a" : $"{summary.AverageTimingMs:0} ms off the beat on average";
        ResultDetails = plan.Kind == ExerciseKind.FreeGroove
            ? $"{summary.TargetsHit} of {summary.NotesPlayed} notes fit the chords - {timing}"
            : $"{summary.TargetsHit} of {summary.TargetsTotal} target notes - {summary.WrongNotes} wrong - {timing}";
        Tips.Clear();
        foreach (string tip in PracticeAdvisor.Tips(summary, plan.Kind)) Tips.Add(tip);

        _nextLesson = null;
        if (summary.Passed && plan.LessonId is not null)
        {
            int index = Curriculum.All.ToList().FindIndex(l => l.Id == plan.LessonId);
            if (index >= 0 && index + 1 < Curriculum.All.Count) _nextLesson = Curriculum.All[index + 1];
        }
        Raise(nameof(HasNextLesson));
        ((RelayCommand)NextLessonCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CalibrateCommand).RaiseCanExecuteChanged();
        State = PracticeState.Finished;
        SessionCompleted?.Invoke();
    }

    private void CalibrateLatency()
    {
        if (_lastSummary is null) return;
        _app.Settings.LatencyMs = Math.Clamp(_app.Settings.LatencyMs + _lastSummary.BiasMs, 0, 400);
        try { _app.SaveSettings(); }
        catch (IOException ex) { ErrorMessage = $"Could not save settings: {ex.Message}"; return; }
        ResultDetails += $"  Latency compensation is now {_app.Settings.LatencyMs:0} ms.";
        _lastSummary = null;
        ((RelayCommand)CalibrateCommand).RaiseCanExecuteChanged();
    }

    public void Dispose() => StopAudio();
}
