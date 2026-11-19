using BassCoach.Core.Audio;
using BassCoach.Core.Storage;

namespace BassCoach.Presentation;

/// <summary>Everything the view models share. Built once at startup (composition root).</summary>
public sealed class AppServices
{
    private readonly JsonFileStore<AppSettings> _settingsStore;
    private readonly JsonFileStore<PracticeStats> _statsStore;

    public AppServices(IAudioService audio, JsonFileStore<AppSettings> settingsStore, JsonFileStore<PracticeStats> statsStore,
        Func<DateTime>? clock = null, Random? random = null)
    {
        Audio = audio;
        _settingsStore = settingsStore;
        _statsStore = statsStore;
        Settings = settingsStore.Load();
        Stats = new StatsService(statsStore.Load());
        Clock = clock ?? (() => DateTime.UtcNow);
        Random = random ?? new Random();
    }

    public IAudioService Audio { get; }
    public AppSettings Settings { get; }
    public StatsService Stats { get; }
    public Func<DateTime> Clock { get; }
    public Random Random { get; }

    public void SaveSettings() => _settingsStore.Save(Settings);

    public void SaveStats() => _statsStore.Save(Stats.Stats);
}
