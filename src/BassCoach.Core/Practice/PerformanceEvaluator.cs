using BassCoach.Core.Dsp;
using BassCoach.Core.Theory;

namespace BassCoach.Core.Practice;

public enum NoteOutcome { Hit, WrongNote, Extra }

public enum TimingRating { Perfect, Good, Early, Late }

public sealed record NoteResult(
    NoteEvent Note, double Beat, Chord Chord, NoteRole Role,
    NoteOutcome Outcome, double TimingMs, TimingRating Rating);

public sealed record SessionSummary(
    double Accuracy, double AverageTimingMs, double BiasMs, int NotesPlayed,
    int TargetsHit, int TargetsTotal, int WrongNotes, int ExtraNotes, int MissedTargets,
    int Stars, bool Passed, IReadOnlyDictionary<NoteRole, int> NotesByRole);

/// <summary>Judges detected notes against the chord chart and the exercise targets.</summary>
public sealed class PerformanceEvaluator
{
    public const double PerfectWindowMs = 50;
    public const double GoodWindowMs = 110;
    private const double MaxMatchWindowMs = 250;
    private const double MissGraceMs = 300;

    private readonly object _gate = new();
    private readonly PracticePlan _plan;
    private readonly double _latencyMs;
    private readonly IReadOnlyList<Target> _targets;
    private readonly bool[] _hit;
    private readonly bool[] _missed;
    private readonly List<NoteResult> _results = new();
    private int _wrongNotes;

    public PerformanceEvaluator(PracticePlan plan, double latencyMs)
    {
        _plan = plan;
        _latencyMs = latencyMs;
        _targets = plan.BuildTargets();
        _hit = new bool[_targets.Count];
        _missed = new bool[_targets.Count];
    }

    private double MsPerBeat => 60000.0 / _plan.Bpm;

    private double MatchWindowMs => Math.Min(MsPerBeat * 0.5, MaxMatchWindowMs);

    /// <summary>Converts the time a note was heard on the input into a beat on the chart, compensating for latency.</summary>
    public double BeatOf(double onsetSeconds) =>
        (onsetSeconds * 1000.0 - _latencyMs) / MsPerBeat - _plan.CountInBars * ChordChart.BeatsPerBar;

    public NoteResult? Evaluate(NoteEvent note)
    {
        double beat = BeatOf(note.OnsetSeconds);
        if (beat < -0.5 || beat > _plan.SongBeats + 0.5) return null;

        lock (_gate)
        {
            int pc = NoteMath.PitchClass(note.Midi);
            NoteResult result = _plan.Kind == ExerciseKind.FreeGroove ? EvaluateFree(note, beat, pc) : EvaluateAgainstTargets(note, beat, pc);
            _results.Add(result);
            return result;
        }
    }

    private Chord ChordAt(double beat)
    {
        double clamped = Math.Clamp(beat, 0, _plan.SongBeats - 1e-6);
        return _plan.Chart.ChordAtBeat(clamped % _plan.Chart.TotalBeats);
    }

    private NoteResult EvaluateFree(NoteEvent note, double beat, int pc)
    {
        var chord = ChordAt(beat);
        var role = chord.RoleOf(pc);
        double timing = (beat - Math.Round(beat * 2) / 2) * MsPerBeat;
        var outcome = role == NoteRole.Outside ? NoteOutcome.WrongNote : NoteOutcome.Hit;
        if (outcome == NoteOutcome.WrongNote) _wrongNotes++;
        return new NoteResult(note, beat, chord, role, outcome, timing, Rate(timing));
    }

    private NoteResult EvaluateAgainstTargets(NoteEvent note, double beat, int pc)
    {
        int index = -1;
        double best = double.MaxValue;
        for (int i = 0; i < _targets.Count; i++)
        {
            if (_hit[i]) continue;
            double distance = Math.Abs(beat - _targets[i].Beat) * MsPerBeat;
            if (distance <= MatchWindowMs && distance < best) { best = distance; index = i; }
        }

        if (index < 0)
        {
            var chord = ChordAt(beat);
            double gridTiming = (beat - Math.Round(beat * 2) / 2) * MsPerBeat;
            return new NoteResult(note, beat, chord, chord.RoleOf(pc), NoteOutcome.Extra, gridTiming, Rate(gridTiming));
        }

        var target = _targets[index];
        double timing = (beat - target.Beat) * MsPerBeat;
        var role = target.Chord.RoleOf(pc);
        if (target.AllowedPitchClasses.Contains(pc))
        {
            _hit[index] = true;
            return new NoteResult(note, beat, target.Chord, role, NoteOutcome.Hit, timing, Rate(timing));
        }

        _wrongNotes++;
        return new NoteResult(note, beat, target.Chord, role, NoteOutcome.WrongNote, timing, Rate(timing));
    }

    private static TimingRating Rate(double timingMs)
    {
        double abs = Math.Abs(timingMs);
        if (abs <= PerfectWindowMs) return TimingRating.Perfect;
        if (abs <= GoodWindowMs) return TimingRating.Good;
        return timingMs < 0 ? TimingRating.Early : TimingRating.Late;
    }

    /// <summary>Marks targets whose time has passed (plus a grace period for detection delay) as missed.</summary>
    public IReadOnlyList<Target> AdvanceTo(double currentBeat)
    {
        var newlyMissed = new List<Target>();
        lock (_gate)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_hit[i] || _missed[i]) continue;
                if ((currentBeat - _targets[i].Beat) * MsPerBeat > MatchWindowMs + MissGraceMs)
                {
                    _missed[i] = true;
                    newlyMissed.Add(_targets[i]);
                }
            }
        }
        return newlyMissed;
    }

    public double RunningAccuracy
    {
        get
        {
            lock (_gate)
            {
                if (_plan.Kind == ExerciseKind.FreeGroove)
                    return _results.Count == 0 ? 1.0 : _results.Count(r => r.Outcome == NoteOutcome.Hit) / (double)_results.Count;
                int resolved = _hit.Count(h => h) + _missed.Count(m => m);
                return resolved == 0 ? 1.0 : _hit.Count(h => h) / (double)resolved;
            }
        }
    }

    public SessionSummary BuildSummary()
    {
        lock (_gate)
        {
            bool free = _plan.Kind == ExerciseKind.FreeGroove;
            var scored = _results.Where(r => r.Outcome == NoteOutcome.Hit).ToList();
            int hits = free ? scored.Count : _hit.Count(h => h);
            int total = free ? _results.Count : _targets.Count;
            double accuracy = total == 0 ? 0 : hits / (double)total;
            double averageTiming = scored.Count == 0 ? 0 : scored.Average(r => Math.Abs(r.TimingMs));
            double bias = scored.Count == 0 ? 0 : scored.Average(r => r.TimingMs);
            int extras = _results.Count(r => r.Outcome == NoteOutcome.Extra);
            int missed = free ? 0 : _targets.Count - hits;
            var byRole = _results.GroupBy(r => r.Role).ToDictionary(g => g.Key, g => g.Count());

            int stars = 0;
            bool enoughNotes = !free || _results.Count >= 8;
            if (enoughNotes)
            {
                if (accuracy >= 0.9 && averageTiming <= 60) stars = 3;
                else if (accuracy >= 0.8 && averageTiming <= 100) stars = 2;
                else if (accuracy >= 0.6) stars = 1;
            }

            return new SessionSummary(accuracy, averageTiming, bias, _results.Count, hits, total,
                _wrongNotes, extras, missed, stars, stars >= 2, byRole);
        }
    }

    public IReadOnlyList<NoteResult> Results
    {
        get { lock (_gate) return _results.ToList(); }
    }
}
