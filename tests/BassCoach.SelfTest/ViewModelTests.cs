using BassCoach.Core.Audio;
using BassCoach.Core.Practice;
using BassCoach.Core.Storage;
using BassCoach.Presentation;

namespace BassCoach.SelfTest;

internal sealed class FakeInput : IAudioInput
{
    public FakeInput(int sampleRate) => SampleRate = sampleRate;
    public int SampleRate { get; }
    public bool Started { get; private set; }
    public bool Disposed { get; private set; }
    public event Action<float[], int>? SamplesAvailable;
    public void Emit(float[] buffer, int count) => SamplesAvailable?.Invoke(buffer, count);
    public void Start() => Started = true;
    public void Stop() { }
    public void Dispose() => Disposed = true;
}

internal sealed class FakeOutput : IAudioOutput
{
    public int SampleRate => 48000;
    public ISampleSource? Source { get; private set; }
    public bool Disposed { get; private set; }
    public void Play(ISampleSource source) => Source = source;
    public void Stop() { }
    public void Dispose() => Disposed = true;
}

internal sealed class FakeAudio : IAudioService
{
    public FakeInput? LastInput { get; private set; }
    public FakeOutput? LastOutput { get; private set; }
    public bool FailInput { get; set; }
    public IReadOnlyList<AudioDevice> GetInputDevices() => new[] { new AudioDevice("in1", "Bass interface") };
    public IReadOnlyList<AudioDevice> GetOutputDevices() => new[] { new AudioDevice("out1", "Headphones") };

    public IAudioInput OpenInput(string? deviceId)
    {
        if (FailInput) throw new InvalidOperationException("No input device is available.");
        return LastInput = new FakeInput(48000);
    }

    public IAudioOutput OpenOutput(string? deviceId) => LastOutput = new FakeOutput();
}

public static class ViewModelTests
{
    private static (AppServices App, FakeAudio Audio, string Dir) NewApp()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bc-vm-" + Guid.NewGuid().ToString("N"));
        var audio = new FakeAudio();
        var app = new AppServices(audio, new JsonFileStore<AppSettings>(Path.Combine(dir, "settings.json")),
            new JsonFileStore<PracticeStats>(Path.Combine(dir, "stats.json")), () => new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc), new Random(1));
        app.Settings.LatencyMs = 0;
        return (app, audio, dir);
    }

    /// <summary>Plays the fake session to the end, driving the UI timer like the WPF view does.</summary>
    private static void PlayThrough(MainViewModel vm, FakeAudio audio, Func<int, (int Midi, double OffsetMs)?> player)
    {
        var plan = vm.Practice.BuildPlan();
        double beat = 60.0 / plan.Bpm;
        var notes = new List<(double, int, double)>();
        for (int bar = 0; bar < plan.Chart.BarCount * plan.Loops; bar++)
            if (player(bar) is { } p) notes.Add(((plan.CountInBars * 4 + bar * 4) * beat + p.OffsetMs / 1000.0, p.Midi, beat * 0.9));
        var input = SynthBass.Mix(48000, (plan.CountInBars * 4 + plan.SongBeats + 4) * beat, notes);
        var block = new float[480];
        for (int i = 0, n = 0; i + 480 <= input.Length; i += 480, n++)
        {
            audio.LastOutput!.Source!.Read(block);
            audio.LastInput!.Emit(input[i..(i + 480)], 480);
            if (n % 16 == 0) vm.Tick();
            if (vm.Practice.State == PracticeState.Finished) break;
        }
        vm.Tick();
    }

    public static void RunAll()
    {
        Console.WriteLine("View models");
        T.Run("lesson flow: start, play, finish, save, unlock next", () =>
        {
            var (app, audio, dir) = NewApp();
            try
            {
                var vm = new MainViewModel(app);
                T.True(vm.Lessons.Items[0].IsUnlocked && vm.Lessons.Items[1].IsLocked, "only lesson 1 unlocked at start");
                vm.Lessons.StartCommand.Execute(vm.Lessons.Items[1]);
                T.Equal(Page.Lessons, vm.CurrentPageKind, "locked lesson does not start");
                vm.Lessons.StartCommand.Execute(vm.Lessons.Items[0]);
                T.Equal(Page.Practice, vm.CurrentPageKind, "navigated to practice");
                T.Equal("Lesson: Meet the root", vm.Practice.HeaderTitle, "lesson title");
                vm.Practice.StartCommand.Execute(null);
                T.Equal(PracticeState.Running, vm.Practice.State, "running");
                T.True(audio.LastInput!.Started && audio.LastOutput!.Source is not null, "audio wired");

                PlayThrough(vm, audio, bar => (bar % 2 == 0 ? 28 : 33, 20));

                T.Equal(PracticeState.Finished, vm.Practice.State, "finished");
                T.True(audio.LastInput.Disposed && audio.LastOutput!.Disposed, "audio released");
                T.True(vm.Practice.StarsText.Contains('\u2605'), "stars shown");
                T.Equal("Lesson passed!", vm.Practice.ResultTitle, "result title");
                T.True(vm.Practice.Tips.Count > 0, "tips");
                T.True(vm.Practice.HasNextLesson, "next lesson offered");
                T.True(vm.Lessons.Items[1].IsUnlocked, "lesson 2 unlocked");
                T.Equal(1, app.Stats.Stats.Sessions.Count, "session recorded");
                T.True(File.Exists(Path.Combine(dir, "stats.json")), "stats saved to disk");
                vm.Practice.NextLessonCommand.Execute(null);
                T.Equal("Lesson: Roots in rock", vm.Practice.HeaderTitle, "moved to lesson 2");
                T.Equal(PracticeState.Setup, vm.Practice.State, "setup screen for next lesson");
                vm.Progress.Refresh();
                T.Equal(1, vm.Progress.Sessions.Count, "progress lists the session");
                T.True(vm.Progress.Headline.StartsWith("1 session -"), "headline: " + vm.Progress.Headline);
                vm.Dispose();
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        });
        T.Run("audio failure shows a message and stays on setup", () =>
        {
            var (app, audio, dir) = NewApp();
            try
            {
                audio.FailInput = true;
                var vm = new MainViewModel(app);
                vm.Practice.StartCommand.Execute(null);
                T.Equal(PracticeState.Setup, vm.Practice.State, "still setup");
                T.True(vm.Practice.HasError && vm.Practice.ErrorMessage.Contains("No input device"), "error text: " + vm.Practice.ErrorMessage);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        });
        T.Run("free practice: editing a lesson detaches it, generated progressions work, leaving stops audio", () =>
        {
            var (app, audio, dir) = NewApp();
            try
            {
                var vm = new MainViewModel(app);
                vm.Practice.LoadLesson(Curriculum.All[0]);
                vm.Practice.Bpm = 90;
                T.Equal("Free practice", vm.Practice.HeaderTitle, "detached after editing");
                T.True(vm.Practice.BuildPlan().LessonId is null, "no lesson id");
                vm.Practice.SelectedStyle = BassCoach.Core.Theory.Style.Bachata;
                T.True(vm.Practice.Bpm == 125 && vm.Practice.Progressions.Contains("Bachata vi-IV-I-V"), "style defaults applied");
                vm.Practice.SelectedProgression = PracticeViewModel.GeneratedName;
                T.True(vm.Practice.ShowMinorOption, "minor option visible");
                T.Equal(4, vm.Practice.BuildPlan().Chart.BarCount, "generated chart");
                vm.Practice.Loops = 1;
                vm.Navigate(Page.Practice);
                vm.Practice.StartCommand.Execute(null);
                T.Equal(PracticeState.Running, vm.Practice.State, "running");
                vm.Tick();
                T.True(vm.Practice.StatusText.StartsWith("Count-in"), "count-in text: " + vm.Practice.StatusText);
                vm.Navigate(Page.Settings);
                T.Equal(PracticeState.Setup, vm.Practice.State, "stopped when leaving");
                T.True(audio.LastInput!.Disposed, "input closed");
                T.Equal(0, app.Stats.Stats.Sessions.Count, "stopped sessions are not recorded");
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        });
        T.Run("settings: save persists, monitor shows the played note, calibration adjusts latency", () =>
        {
            var (app, audio, dir) = NewApp();
            try
            {
                var vm = new MainViewModel(app);
                var settings = vm.Settings;
                T.True(settings.InputDevices.Count == 2 && settings.InputDevices[0].Id == "", "default + one device");
                settings.SelectedInput = settings.InputDevices[1];
                settings.StringCount = 5;
                settings.LatencyMs = 130.4;
                settings.SaveCommand.Execute(null);
                var reloaded = new JsonFileStore<AppSettings>(Path.Combine(dir, "settings.json")).Load();
                T.Equal("in1", reloaded.InputDeviceId, "device persisted");
                T.Equal(5, reloaded.StringCount, "strings persisted");
                T.Equal(130.0, reloaded.LatencyMs, "latency rounded");

                settings.ToggleMonitorCommand.Execute(null);
                T.True(settings.IsMonitoring, "monitoring");
                var tone = SynthBass.Note(48000, 41.2034, 0.6);
                for (int i = 0; i + 480 <= tone.Length; i += 480) audio.LastInput!.Emit(tone[i..(i + 480)], 480);
                settings.Tick();
                T.Equal("E1", settings.DetectedText, "note name");
                T.True(settings.Level > 0.05, "level meter moves");
                settings.ToggleMonitorCommand.Execute(null);
                T.True(!settings.IsMonitoring && audio.LastInput!.Disposed, "monitor closed");

                settings.LatencyMs = 999;
                T.Equal(400.0, settings.LatencyMs, "latency clamped");
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        });
        T.Run("feedback text is clear for hits, misses and wrong notes", () =>
        {
            var chord = new BassCoach.Core.Theory.Chord(4, BassCoach.Core.Theory.ChordQuality.Major);
            var note = new BassCoach.Core.Dsp.NoteEvent(1, 28, 0, 41.2, 0.5);
            var hit = FeedbackFormatter.Describe(new NoteResult(note, 0, chord, BassCoach.Core.Theory.NoteRole.Root, NoteOutcome.Hit, 12, TimingRating.Perfect), false);
            T.True(hit.Text == "E: root, right on the beat" && hit.Kind == FeedbackKind.Good, hit.Text);
            var late = FeedbackFormatter.Describe(new NoteResult(note, 0, chord, BassCoach.Core.Theory.NoteRole.Root, NoteOutcome.Hit, 140, TimingRating.Late), false);
            T.True(late.Text.Contains("late by 140 ms") && late.Kind == FeedbackKind.Warning, late.Text);
            var wrong = FeedbackFormatter.Describe(new NoteResult(note, 0, chord, BassCoach.Core.Theory.NoteRole.Outside, NoteOutcome.WrongNote, 0, TimingRating.Perfect), false);
            T.True(wrong.Kind == FeedbackKind.Bad && wrong.Text.Contains("not a note of E"), wrong.Text);
        });
    }
}
