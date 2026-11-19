using System.IO;

namespace BassCoach.Desktop;

/// <summary>Minimal file logger. Never throws: logging must not be able to crash the app.</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BassCoach", "logs", "bass-coach.log");

    public static void Error(object error)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {error}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Nothing sensible left to do.
        }
    }
}
