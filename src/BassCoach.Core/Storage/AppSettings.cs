using System.Text.Json.Serialization;

namespace BassCoach.Core.Storage;

public sealed class AppSettings
{
    public string? InputDeviceId { get; set; }

    public string? OutputDeviceId { get; set; }

    /// <summary>4 or 5.</summary>
    public int StringCount { get; set; } = 4;

    /// <summary>Round-trip audio latency to subtract from note timing (ms). Tune with the Settings page.</summary>
    public double LatencyMs { get; set; } = 90;

    /// <summary>0 (needs a loud signal) to 1 (picks up soft notes).</summary>
    public double Sensitivity { get; set; } = 0.6;

    public double BackingVolume { get; set; } = 0.8;

    public bool BassGuide { get; set; } = true;

    [JsonIgnore]
    public double Gate => 0.003 * Math.Pow(20, 1 - Math.Clamp(Sensitivity, 0, 1));
}
