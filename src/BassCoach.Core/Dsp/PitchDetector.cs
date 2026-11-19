namespace BassCoach.Core.Dsp;

public readonly record struct PitchEstimate(double FrequencyHz, double Confidence)
{
    public bool IsVoiced => FrequencyHz > 0;
}

/// <summary>Monophonic pitch detector based on the YIN algorithm, tuned for the bass range.</summary>
public sealed class PitchDetector
{
    private const int IntegrationWindow = 2048;
    private readonly int _sampleRate;
    private readonly int _minTau;
    private readonly int _maxTau;
    private readonly double _threshold;
    private readonly float[] _difference;
    private readonly float[] _normalized;

    public PitchDetector(int sampleRate, double minFrequencyHz = 36, double maxFrequencyHz = 500, double threshold = 0.15)
    {
        if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        _sampleRate = sampleRate;
        _threshold = threshold;
        _minTau = Math.Max(2, (int)(sampleRate / maxFrequencyHz));
        _maxTau = (int)(sampleRate / minFrequencyHz) + 2;
        _difference = new float[_maxTau + 1];
        _normalized = new float[_maxTau + 1];
        RequiredSamples = IntegrationWindow + _maxTau + 1;
    }

    /// <summary>Number of most-recent samples <see cref="Detect"/> needs.</summary>
    public int RequiredSamples { get; }

    public PitchEstimate Detect(ReadOnlySpan<float> frame)
    {
        if (frame.Length < RequiredSamples) throw new ArgumentException("Frame is too short.", nameof(frame));

        for (int tau = 1; tau <= _maxTau; tau++)
        {
            float sum = 0;
            for (int j = 0; j < IntegrationWindow; j++)
            {
                float d = frame[j] - frame[j + tau];
                sum += d * d;
            }
            _difference[tau] = sum;
        }

        _normalized[0] = 1f;
        float running = 0f;
        for (int tau = 1; tau <= _maxTau; tau++)
        {
            running += _difference[tau];
            _normalized[tau] = running > 0f ? _difference[tau] * tau / running : 1f;
        }

        int best = -1;
        for (int tau = _minTau; tau < _maxTau; tau++)
        {
            if (_normalized[tau] >= _threshold) continue;
            // Take the deepest point of the whole dip (small ripples must not stop the search early).
            best = tau;
            while (tau + 1 < _maxTau && _normalized[tau + 1] < _threshold)
            {
                tau++;
                if (_normalized[tau] < _normalized[best]) best = tau;
            }
            break;
        }

        if (best < 0)
        {
            // No clear dip: accept the global minimum only if it is reasonably deep.
            int minIndex = _minTau;
            for (int tau = _minTau; tau < _maxTau; tau++)
                if (_normalized[tau] < _normalized[minIndex]) minIndex = tau;
            if (_normalized[minIndex] > 0.35f) return default;
            best = minIndex;
        }

        double refined = RefineParabolic(best);
        return new PitchEstimate(_sampleRate / refined, Math.Clamp(1.0 - _normalized[best], 0.0, 1.0));
    }

    // Interpolates on the raw difference function: the tau-weighted normalized one is biased toward shorter periods.
    private double RefineParabolic(int tau)
    {
        if (tau <= 1 || tau >= _maxTau) return tau;
        double a = _difference[tau - 1], b = _difference[tau], c = _difference[tau + 1];
        double denominator = a - 2 * b + c;
        return Math.Abs(denominator) < 1e-12 ? tau : tau + 0.5 * (a - c) / denominator;
    }
}
