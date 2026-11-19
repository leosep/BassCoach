using BassCoach.Core.Audio;
using BassCoach.Core.Dsp;
using BassCoach.Core.Storage;
using BassCoach.Core.Theory;

namespace BassCoach.Core.Practice;

public enum SessionPhase { CountIn, Playing, Finished }

public sealed record PracticeSnapshot(
    SessionPhase Phase, double Beat, int Loop, int BeatInBar, Chord CurrentChord, Chord NextChord,
    bool UsesFlats, int CountInBeatsLeft, NoteResult? LastResult, double RunningAccuracy, double InputLevel, PitchEstimate Pitch);

/// <summary>
/// One practice run. The audio layer calls <see cref="FeedInput"/> with microphone/line samples and plays
/// <see cref="Backing"/>; the UI polls <see cref="GetSnapshot"/>.
/// </summary>
public sealed class PracticeSession
{
    private const double FinishPaddingSeconds = 0.6;
    private readonly BackingSynth _synth;
    private readonly NoteTracker _tracker;
    private readonly PerformanceEvaluator _evaluator;
    private readonly object _gate = new();
    private NoteResult? _lastResult;

    public PracticeSession(PracticePlan plan, AppSettings settings, int inputSampleRate, int outputSampleRate)
    {
        Plan = plan;
        _synth = new BackingSynth(new BackingOptions(
            outputSampleRate, plan.Bpm, plan.Style, plan.Chart, plan.Loops, plan.CountInBars,
            BassGuide: settings.BassGuide, Volume: settings.BackingVolume));
        var tuning = BassTuning.ForStringCount(settings.StringCount);
        _tracker = new NoteTracker(inputSampleRate, tuning.MinDetectFrequency, settings.Gate);
        _evaluator = new PerformanceEvaluator(plan, settings.LatencyMs);
        _tracker.NoteDetected += OnNote;
    }

    public PracticePlan Plan { get; }

    public ISampleSource Backing => _synth;

    public void FeedInput(ReadOnlySpan<float> mono) => _tracker.Process(mono);

    private void OnNote(NoteEvent note)
    {
        var result = _evaluator.Evaluate(note);
        if (result is null) return;
        lock (_gate) _lastResult = result;
    }

    public PracticeSnapshot GetSnapshot()
    {
        double beat = _synth.CurrentBeat;
        _evaluator.AdvanceTo(beat);
        var chart = Plan.Chart;
        double finishBeat = Plan.SongBeats + FinishPaddingSeconds * Plan.Bpm / 60.0;

        SessionPhase phase;
        int loop = 0, beatInBar = 0, bar = 0, countIn = 0;
        if (beat < 0)
        {
            phase = SessionPhase.CountIn;
            countIn = (int)Math.Ceiling(-beat);
        }
        else if (beat >= finishBeat)
        {
            phase = SessionPhase.Finished;
            loop = Plan.Loops - 1;
            bar = chart.BarCount - 1;
        }
        else
        {
            phase = SessionPhase.Playing;
            double songBeat = Math.Min(beat, Plan.SongBeats - 1e-6);
            loop = (int)(songBeat / chart.TotalBeats);
            double inLoop = songBeat % chart.TotalBeats;
            bar = (int)(inLoop / ChordChart.BeatsPerBar);
            beatInBar = (int)(inLoop % ChordChart.BeatsPerBar);
        }

        NoteResult? last;
        lock (_gate) last = _lastResult;
        return new PracticeSnapshot(phase, beat, loop + 1, beatInBar, chart.Bars[bar], chart.Bars[(bar + 1) % chart.BarCount],
            chart.UsesFlats, countIn, last, _evaluator.RunningAccuracy, _tracker.CurrentLevel, _tracker.LastPitch);
    }

    public SessionSummary Summarize() => _evaluator.BuildSummary();
}
