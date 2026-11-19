using System.Collections.ObjectModel;
using System.Windows.Input;
using BassCoach.Core.Audio;
using BassCoach.Core.Dsp;
using BassCoach.Core.Theory;

namespace BassCoach.Presentation;

public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private static readonly AudioDevice DefaultDevice = new("", "System default");
    private readonly AppServices _app;
    private IAudioInput? _monitorInput;
    private NoteTracker? _monitorTracker;
    private AudioDevice _input = DefaultDevice;
    private AudioDevice _output = DefaultDevice;
    private int _strings;
    private double _latency;
    private double _sensitivity;
    private double _volume;
    private bool _bassGuide;
    private string _message = "";
    private double _level;
    private string _detected = "-";
    private double _cents;

    public SettingsViewModel(AppServices app)
    {
        _app = app;
        SaveCommand = new RelayCommand(Save);
        ToggleMonitorCommand = new RelayCommand(ToggleMonitor);
        Reload();
    }

    public ObservableCollection<AudioDevice> InputDevices { get; } = new();
    public ObservableCollection<AudioDevice> OutputDevices { get; } = new();
    public IReadOnlyList<int> StringOptions { get; } = new[] { 4, 5 };

    public AudioDevice SelectedInput { get => _input; set => Set(ref _input, value); }
    public AudioDevice SelectedOutput { get => _output; set => Set(ref _output, value); }
    public int StringCount { get => _strings; set => Set(ref _strings, value); }
    public double LatencyMs { get => _latency; set => Set(ref _latency, Math.Clamp(Math.Round(value), 0, 400)); }
    public double Sensitivity { get => _sensitivity; set { if (Set(ref _sensitivity, Math.Clamp(value, 0, 1)) && _monitorTracker is not null) _monitorTracker.Gate = Current().Gate; } }
    public double BackingVolume { get => _volume; set => Set(ref _volume, Math.Clamp(value, 0, 1)); }
    public bool BassGuide { get => _bassGuide; set => Set(ref _bassGuide, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public bool IsMonitoring => _monitorInput is not null;
    public string MonitorButtonText => IsMonitoring ? "Stop input check" : "Test my input";
    public double Level { get => _level; private set => Set(ref _level, value); }
    public string DetectedText { get => _detected; private set => Set(ref _detected, value); }
    public double Cents { get => _cents; private set => Set(ref _cents, value); }

    public ICommand SaveCommand { get; }
    public ICommand ToggleMonitorCommand { get; }

    /// <summary>Reads devices and values from the saved settings (also used to discard edits).</summary>
    public void Reload()
    {
        var s = _app.Settings;
        Fill(InputDevices, SafeList(_app.Audio.GetInputDevices));
        Fill(OutputDevices, SafeList(_app.Audio.GetOutputDevices));
        SelectedInput = InputDevices.FirstOrDefault(d => d.Id == (s.InputDeviceId ?? "")) ?? DefaultDevice;
        SelectedOutput = OutputDevices.FirstOrDefault(d => d.Id == (s.OutputDeviceId ?? "")) ?? DefaultDevice;
        StringCount = s.StringCount >= 5 ? 5 : 4;
        LatencyMs = s.LatencyMs;
        Sensitivity = s.Sensitivity;
        BackingVolume = s.BackingVolume;
        BassGuide = s.BassGuide;
    }

    private IReadOnlyList<AudioDevice> SafeList(Func<IReadOnlyList<AudioDevice>> source)
    {
        try { return source(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Message = $"Could not list audio devices: {ex.Message}";
            return Array.Empty<AudioDevice>();
        }
    }

    private static void Fill(ObservableCollection<AudioDevice> target, IReadOnlyList<AudioDevice> devices)
    {
        target.Clear();
        target.Add(DefaultDevice);
        foreach (var device in devices) target.Add(device);
    }

    private Core.Storage.AppSettings Current() => new()
    {
        InputDeviceId = _input.Id.Length == 0 ? null : _input.Id,
        OutputDeviceId = _output.Id.Length == 0 ? null : _output.Id,
        StringCount = _strings, LatencyMs = _latency, Sensitivity = _sensitivity, BackingVolume = _volume, BassGuide = _bassGuide,
    };

    private void Save()
    {
        var edited = Current();
        var s = _app.Settings;
        s.InputDeviceId = edited.InputDeviceId;
        s.OutputDeviceId = edited.OutputDeviceId;
        s.StringCount = edited.StringCount;
        s.LatencyMs = edited.LatencyMs;
        s.Sensitivity = edited.Sensitivity;
        s.BackingVolume = edited.BackingVolume;
        s.BassGuide = edited.BassGuide;
        try
        {
            _app.SaveSettings();
            Message = "Settings saved.";
        }
        catch (IOException ex) { Message = $"Could not save settings: {ex.Message}"; }
    }

    private void ToggleMonitor()
    {
        if (IsMonitoring) { StopMonitor(); return; }
        try
        {
            var input = _app.Audio.OpenInput(_input.Id.Length == 0 ? null : _input.Id);
            var tuning = BassTuning.ForStringCount(_strings);
            var tracker = new NoteTracker(input.SampleRate, tuning.MinDetectFrequency, Current().Gate);
            input.SamplesAvailable += (buffer, count) => tracker.Process(buffer.AsSpan(0, count));
            input.Start();
            _monitorInput = input;
            _monitorTracker = tracker;
            Message = "Play a note on your bass.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StopMonitor();
            Message = $"Could not open the input: {ex.Message}";
        }
        Raise(nameof(IsMonitoring));
        Raise(nameof(MonitorButtonText));
    }

    public void StopMonitor()
    {
        _monitorInput?.Dispose();
        _monitorInput = null;
        _monitorTracker = null;
        Level = 0;
        DetectedText = "-";
        Cents = 0;
        Raise(nameof(IsMonitoring));
        Raise(nameof(MonitorButtonText));
    }

    /// <summary>Called by the view about 30 times per second while this page is visible.</summary>
    public void Tick()
    {
        if (_monitorTracker is null) return;
        Level = Math.Clamp(_monitorTracker.CurrentLevel * 2, 0, 1);
        var pitch = _monitorTracker.LastPitch;
        if (pitch.IsVoiced && pitch.Confidence >= 0.6)
        {
            double midi = NoteMath.FrequencyToMidi(pitch.FrequencyHz);
            int rounded = (int)Math.Round(midi);
            DetectedText = NoteMath.NameWithOctave(rounded);
            Cents = Math.Clamp((midi - rounded) * 100, -50, 50);
        }
        else
        {
            DetectedText = "-";
            Cents = 0;
        }
    }

    public void Dispose() => StopMonitor();
}
