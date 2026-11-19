using System.IO;
using System.Windows;
using System.Windows.Threading;
using BassCoach.Core.Storage;
using BassCoach.Desktop.Audio;
using BassCoach.Presentation;

namespace BassCoach.Desktop;

public partial class App : Application
{
    private MainViewModel? _main;
    private DispatcherTimer? _timer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception);
            MessageBox.Show(
                $"Something unexpected happened, but Bass Coach is still running.\n\nIf it keeps happening, send this file to the developer:\n{Log.FilePath}",
                "Bass Coach", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error(args.ExceptionObject);

        string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BassCoach");
        var services = new AppServices(
            new NAudioService(),
            new JsonFileStore<AppSettings>(Path.Combine(dataDirectory, "settings.json")),
            new JsonFileStore<PracticeStats>(Path.Combine(dataDirectory, "stats.json")));

        _main = new MainViewModel(services);
        new MainWindow { DataContext = _main }.Show();

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => _main.Tick();
        _timer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        _main?.Dispose();
        base.OnExit(e);
    }
}
