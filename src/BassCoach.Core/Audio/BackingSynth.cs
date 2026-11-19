using BassCoach.Core.Theory;

namespace BassCoach.Core.Audio;

public sealed record BackingOptions(
    int SampleRate, double Bpm, Style Style, ChordChart Chart, int Loops, int CountInBars = 1,
    bool Drums = true, bool Chords = true, bool BassGuide = false, double Volume = 0.8);

/// <summary>
/// Deterministic software band: a count-in, then drums and chords looping over a chord chart.
/// Beat position is derived from the number of samples rendered, so it never drifts.
/// </summary>
public sealed class BackingSynth : ISampleSource
{
    private enum Kind { Kick, Snare, Hat, Click, Perc, Tone }

    private sealed class Voice
    {
        public Kind Kind;
        public double Age;
        public double Phase;
        public double Frequency;
        public double Level;
        public double Decay;
        public bool Accent;
        public double Previous;
        public bool Done;
    }

    private const int StepsPerBar = 16;
    private readonly BackingOptions _options;
    private readonly StylePattern _pattern;
    private readonly List<Voice> _voices = new();
    private readonly Random _noise = new(7);
    private readonly double _stepSamples;
    private readonly double _dt;
    private readonly int _countInSteps;
    private readonly int _chartSteps;
    private readonly int _totalSteps;
    private readonly long _endSample;
    private long _position;
    private long _nextStepSample;
    private int _stepIndex;

    public BackingSynth(BackingOptions options)
    {
        if (options.Bpm is < 30 or > 300) throw new ArgumentOutOfRangeException(nameof(options), "BPM must be between 30 and 300.");
        _options = options;
        _pattern = StyleCatalog.Get(options.Style);
        _dt = 1.0 / options.SampleRate;
        _stepSamples = options.SampleRate * 60.0 / options.Bpm / 4.0;
        _countInSteps = options.CountInBars * StepsPerBar;
        _chartSteps = options.Chart.BarCount * StepsPerBar;
        _totalSteps = _countInSteps + _chartSteps * options.Loops;
        _endSample = StepStart(_totalSteps) + options.SampleRate / 2;
    }

    public int SampleRate => _options.SampleRate;

    /// <summary>Beats since the first chord. Negative during the count-in.</summary>
    public double CurrentBeat => Volatile.Read(ref _position) / (_stepSamples * 4.0) - _options.CountInBars * ChordChart.BeatsPerBar;

    public bool Finished => Volatile.Read(ref _position) >= _endSample;

    private long StepStart(int step) =>
        (long)(step * _stepSamples + (step % 4 == 2 ? _pattern.Swing * (2.0 / 3.0) * _stepSamples : 0.0));

    public int Read(Span<float> destination)
    {
        for (int i = 0; i < destination.Length; i++)
        {
            long position = _position;
            while (_stepIndex < _totalSteps && _nextStepSample <= position)
            {
                Trigger(_stepIndex);
                _stepIndex++;
                _nextStepSample = StepStart(_stepIndex);
            }
            destination[i] = position >= _endSample ? 0f : Mix();
            Volatile.Write(ref _position, position + 1);
        }
        return destination.Length;
    }

    private static bool On(string pattern, int step) => pattern[step] == 'x';

    private void Trigger(int step)
    {
        if (step < _countInSteps)
        {
            if (step % 4 == 0) Add(Kind.Click, 0, 1.0, 0, accent: step % StepsPerBar == 0);
            return;
        }

        int songStep = step - _countInSteps;
        int inBar = songStep % StepsPerBar;
        var chord = _options.Chart.ChordAtBeat((songStep % _chartSteps) / 4.0);

        if (_options.Drums)
        {
            if (On(_pattern.Kick, inBar)) Add(Kind.Kick, 0, 0.9, 0);
            if (On(_pattern.Snare, inBar)) Add(Kind.Snare, 0, 0.7, 0);
            if (On(_pattern.Hat, inBar)) Add(Kind.Hat, 0, 0.8, 0, accent: inBar % 4 == 0);
            if (On(_pattern.Perc, inBar)) Add(Kind.Perc, 0, 0.8, 0);
        }

        if (_options.Chords && On(_pattern.Comp, inBar))
            foreach (int pc in chord.ChordTonePcs)
                Add(Kind.Tone, NoteMath.MidiToFrequency(55 + (((pc - 55) % 12) + 12) % 12), 0.11, _pattern.CompDecay);

        if (_options.BassGuide && On(_pattern.BassGuide, inBar))
            Add(Kind.Tone, NoteMath.MidiToFrequency(28 + (((chord.RootPc - 28) % 12) + 12) % 12), 0.32, 2.5);
    }

    private void Add(Kind kind, double frequency, double level, double decay, bool accent = false) =>
        _voices.Add(new Voice { Kind = kind, Frequency = frequency, Level = level, Decay = decay, Accent = accent });

    private float Mix()
    {
        double sum = 0;
        for (int v = _voices.Count - 1; v >= 0; v--)
        {
            var voice = _voices[v];
            sum += Render(voice);
            voice.Age += _dt;
            if (voice.Done) _voices.RemoveAt(v);
        }
        return (float)Math.Tanh(sum * _options.Volume);
    }

    private double Render(Voice v)
    {
        double t = v.Age;
        switch (v.Kind)
        {
            case Kind.Kick:
            {
                v.Phase += 2 * Math.PI * (45 + 95 * Math.Exp(-t * 35)) * _dt;
                v.Done = t > 0.5;
                return Math.Sin(v.Phase) * Math.Exp(-t * 10) * v.Level;
            }
            case Kind.Snare:
            {
                v.Done = t > 0.3;
                double n = _noise.NextDouble() * 2 - 1;
                return (n * 0.6 * Math.Exp(-t * 20) + Math.Sin(2 * Math.PI * 185 * t) * 0.35 * Math.Exp(-t * 28)) * v.Level;
            }
            case Kind.Hat:
            {
                v.Done = t > 0.12;
                double n = _noise.NextDouble() * 2 - 1;
                double highPass = n - v.Previous;
                v.Previous = n;
                return highPass * Math.Exp(-t * (v.Accent ? 60 : 110)) * 0.3 * v.Level;
            }
            case Kind.Click:
            {
                v.Done = t > 0.12;
                return Math.Sin(2 * Math.PI * (v.Accent ? 1600 : 1000) * t) * Math.Exp(-t * 70) * 0.5;
            }
            case Kind.Perc:
            {
                v.Done = t > 0.4;
                double square = Math.Sign(Math.Sin(2 * Math.PI * 560 * t)) + Math.Sign(Math.Sin(2 * Math.PI * 835 * t));
                return square * 0.5 * Math.Exp(-t * 16) * 0.15 * v.Level;
            }
            default:
            {
                double envelope = Math.Min(1.0, t / 0.004) * Math.Exp(-t * v.Decay);
                v.Done = t > 0.01 && envelope < 0.001;
                v.Phase += 2 * Math.PI * v.Frequency * _dt;
                return (Math.Sin(v.Phase) + 0.35 * Math.Sin(2 * v.Phase) + 0.15 * Math.Sin(3 * v.Phase)) * envelope * v.Level;
            }
        }
    }
}
