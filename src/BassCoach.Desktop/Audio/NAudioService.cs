using BassCoach.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BassCoach.Desktop.Audio;

/// <summary>Windows audio (WASAPI, shared mode) through NAudio.</summary>
internal sealed class NAudioService : IAudioService
{
    public IReadOnlyList<AudioDevice> GetInputDevices() => List(DataFlow.Capture);

    public IReadOnlyList<AudioDevice> GetOutputDevices() => List(DataFlow.Render);

    public IAudioInput OpenInput(string? deviceId) => new NAudioInput(deviceId);

    public IAudioOutput OpenOutput(string? deviceId) => new NAudioOutput(deviceId);

    private static IReadOnlyList<AudioDevice> List(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active)
            .Select(d => new AudioDevice(d.ID, d.FriendlyName))
            .ToList();
    }

    internal static MMDevice GetDevice(MMDeviceEnumerator enumerator, DataFlow flow, string? id) =>
        string.IsNullOrEmpty(id) ? enumerator.GetDefaultAudioEndpoint(flow, Role.Console) : enumerator.GetDevice(id);
}

/// <summary>Captures the device's first channel as mono float. Plug the bass into input 1.</summary>
internal sealed class NAudioInput : IAudioInput
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDevice _device;
    private readonly WasapiCapture _capture;
    private readonly int _channels;
    private readonly int _bytesPerSample;
    private float[] _mono = new float[4096];
    private bool _disposed;

    public NAudioInput(string? deviceId)
    {
        _device = NAudioService.GetDevice(_enumerator, DataFlow.Capture, deviceId);
        _capture = new WasapiCapture(_device, true, 20);
        var format = _capture.WaveFormat;
        SampleRate = format.SampleRate;
        _channels = Math.Max(1, format.Channels);
        _bytesPerSample = format.BitsPerSample / 8;
        _capture.DataAvailable += OnData;
    }

    public int SampleRate { get; }

    public event Action<float[], int>? SamplesAvailable;

    public void Start() => _capture.StartRecording();

    public void Stop()
    {
        if (!_disposed) _capture.StopRecording();
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        int frames = e.BytesRecorded / (_bytesPerSample * _channels);
        if (frames == 0) return;
        if (_mono.Length < frames) _mono = new float[frames];
        for (int frame = 0; frame < frames; frame++)
            _mono[frame] = ReadSample(e.Buffer, frame * _channels * _bytesPerSample);
        SamplesAvailable?.Invoke(_mono, frames);
    }

    // WASAPI shared mode almost always delivers 32-bit float; 16 and 24-bit integer are handled as a fallback.
    private float ReadSample(byte[] buffer, int offset) => _bytesPerSample switch
    {
        4 => BitConverter.ToSingle(buffer, offset),
        2 => BitConverter.ToInt16(buffer, offset) / 32768f,
        3 => ((buffer[offset] << 8 | buffer[offset + 1] << 16 | buffer[offset + 2] << 24) >> 8) / 8388608f,
        _ => 0f,
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _capture.DataAvailable -= OnData;
        try { _capture.StopRecording(); } catch (Exception ex) { Log.Error(ex); }
        _capture.Dispose();
        _device.Dispose();
        _enumerator.Dispose();
    }
}

internal sealed class NAudioOutput : IAudioOutput
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDevice _device;
    private WasapiOut? _out;
    private bool _disposed;

    public NAudioOutput(string? deviceId)
    {
        _device = NAudioService.GetDevice(_enumerator, DataFlow.Render, deviceId);
        SampleRate = _device.AudioClient.MixFormat.SampleRate;
    }

    public int SampleRate { get; }

    public void Play(ISampleSource source)
    {
        var provider = new MonoToStereoProvider(source);
        _out = new WasapiOut(_device, AudioClientShareMode.Shared, true, 40);
        _out.Init(new SampleToWaveProvider(provider));
        _out.Play();
    }

    public void Stop() => _out?.Stop();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _out?.Stop(); } catch (Exception ex) { Log.Error(ex); }
        _out?.Dispose();
        _device.Dispose();
        _enumerator.Dispose();
    }

    private sealed class MonoToStereoProvider : ISampleProvider
    {
        private readonly ISampleSource _source;
        private float[] _mono = new float[2048];

        public MonoToStereoProvider(ISampleSource source)
        {
            _source = source;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.SampleRate, 2);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / 2;
            if (_mono.Length < frames) _mono = new float[frames];
            _source.Read(_mono.AsSpan(0, frames));
            for (int i = 0; i < frames; i++)
            {
                buffer[offset + 2 * i] = _mono[i];
                buffer[offset + 2 * i + 1] = _mono[i];
            }
            return frames * 2;
        }
    }
}
