using BassCoach.Core.Theory;

namespace BassCoach.Core.Dsp;

/// <summary>A plucked note: when it started and which pitch it had.</summary>
public sealed record NoteEvent(double OnsetSeconds, int Midi, double Cents, double FrequencyHz, double Level);

/// <summary>
/// Turns a stream of mono audio into note events. Onsets come from the energy envelope (accurate timing);
/// the pitch is the median of YIN estimates taken shortly after the onset (accurate note).
/// </summary>
public sealed class NoteTracker
{
    private const int ChunkSize = 256;
    private const int PitchHop = 1024;
    private const int RingSize = 16384;
    private const double MinNoteGapSeconds = 0.09;
    private const double CollectStartSeconds = 0.080;
    private const double CollectEndSeconds = 0.170;
    private const double MinConfidence = 0.7;

    private sealed class Pending
    {
        public long OnsetSample;
        public double Peak;
        public readonly List<double> MidiEstimates = new();
    }

    private readonly int _sampleRate;
    private readonly PitchDetector _detector;
    private readonly float[] _ring = new float[RingSize];
    private readonly float[] _scratch;
    private long _total;
    private int _chunkFill;
    private double _envelope;
    private double _slowEnvelope;
    private double _previousSlow;
    private readonly double _release;
    private readonly double _slowRelease;
    private long _lastOnsetSample = long.MinValue / 2;
    private Pending? _pending;

    public NoteTracker(int sampleRate, double minFrequencyHz = 36, double gate = 0.01)
    {
        _sampleRate = sampleRate;
        _release = Math.Exp(-1.0 / (sampleRate * 0.03));
        _slowRelease = Math.Exp(-1.0 / (sampleRate * 0.25)); // long memory: ignores beating between two ringing strings
        _detector = new PitchDetector(sampleRate, minFrequencyHz);
        _scratch = new float[_detector.RequiredSamples];
        Gate = gate;
    }

    /// <summary>Peak level (0..1) below which input is treated as silence.</summary>
    public double Gate { get; set; }

    public double CurrentLevel { get; private set; }

    public PitchEstimate LastPitch { get; private set; }

    public long TotalSamples => _total;

    public event Action<NoteEvent>? NoteDetected;

    public void Process(ReadOnlySpan<float> samples)
    {
        foreach (float sample in samples)
        {
            _ring[(int)(_total & (RingSize - 1))] = sample;
            _total++;
            float magnitude = Math.Abs(sample);
            _envelope = magnitude > _envelope ? magnitude : _envelope * _release;
            _slowEnvelope = magnitude > _slowEnvelope ? magnitude : _slowEnvelope * _slowRelease;
            if (++_chunkFill == ChunkSize) EndChunk();
            if (_total % PitchHop == 0) RunPitch();
        }
    }

    private void EndChunk()
    {
        double rms = _envelope; // peak follower: stable for low notes where a 5 ms RMS window would ripple
        _chunkFill = 0;
        CurrentLevel = rms;

        long chunkStart = _total - ChunkSize;
        bool rising = rms > Gate && rms > _previousSlow * 1.4 + Gate * 0.5;
        bool spaced = chunkStart - _lastOnsetSample > MinNoteGapSeconds * _sampleRate;

        if (rising && spaced)
        {
            FinalizePending();
            _lastOnsetSample = chunkStart;
            _pending = new Pending { OnsetSample = chunkStart, Peak = rms };
        }
        else if (_pending is { } pending && chunkStart - pending.OnsetSample < 0.1 * _sampleRate)
        {
            pending.Peak = Math.Max(pending.Peak, rms);
        }

        _previousSlow = _slowEnvelope;
    }

    private void RunPitch()
    {
        if (_total < _scratch.Length) return;

        if (CurrentLevel < Gate * 0.5)
        {
            LastPitch = default;
        }
        else
        {
            long start = _total - _scratch.Length;
            for (int i = 0; i < _scratch.Length; i++) _scratch[i] = _ring[(int)((start + i) & (RingSize - 1))];
            LastPitch = _detector.Detect(_scratch);
        }

        if (_pending is not { } pending) return;
        double age = (_total - pending.OnsetSample) / (double)_sampleRate;
        if (age >= CollectStartSeconds && age <= CollectEndSeconds && LastPitch.IsVoiced && LastPitch.Confidence >= MinConfidence)
            pending.MidiEstimates.Add(NoteMath.FrequencyToMidi(LastPitch.FrequencyHz));
        if (age > CollectEndSeconds) FinalizePending();
    }

    private void FinalizePending()
    {
        if (_pending is not { } pending) return;
        _pending = null;
        if (pending.MidiEstimates.Count == 0) return;

        pending.MidiEstimates.Sort();
        double midiFloat = pending.MidiEstimates[pending.MidiEstimates.Count / 2];
        int midi = (int)Math.Round(midiFloat);
        if (midi is < 20 or > 84) return;

        NoteDetected?.Invoke(new NoteEvent(
            OnsetSeconds: pending.OnsetSample / (double)_sampleRate,
            Midi: midi,
            Cents: (midiFloat - midi) * 100.0,
            FrequencyHz: NoteMath.MidiToFrequency(midiFloat),
            Level: pending.Peak));
    }
}
