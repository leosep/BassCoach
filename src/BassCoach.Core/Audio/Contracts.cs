namespace BassCoach.Core.Audio;

/// <summary>A pull-based mono audio source (the backing band).</summary>
public interface ISampleSource
{
    int SampleRate { get; }

    /// <summary>Fills <paramref name="destination"/> with mono samples and returns how many were written.</summary>
    int Read(Span<float> destination);
}

public sealed record AudioDevice(string Id, string Name);

public interface IAudioInput : IDisposable
{
    /// <summary>Sample rate of the mono samples delivered by <see cref="SamplesAvailable"/>.</summary>
    int SampleRate { get; }

    /// <summary>Raised on an audio thread with mono float samples (buffer, count).</summary>
    event Action<float[], int>? SamplesAvailable;

    void Start();

    void Stop();
}

public interface IAudioOutput : IDisposable
{
    int SampleRate { get; }

    void Play(ISampleSource source);

    void Stop();
}

/// <summary>Abstraction over the operating system audio API so the logic can be tested without hardware.</summary>
public interface IAudioService
{
    IReadOnlyList<AudioDevice> GetInputDevices();

    IReadOnlyList<AudioDevice> GetOutputDevices();

    IAudioInput OpenInput(string? deviceId);

    IAudioOutput OpenOutput(string? deviceId);
}
