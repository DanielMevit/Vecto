using System.IO;
using System.Text.Json;

namespace Vecto.App;

/// <summary>Last-used options, persisted to %APPDATA%\Vecto\settings.json between runs.</summary>
public sealed class AppSettings
{
    public bool AutoColors { get; set; } = true;
    public int ColorCount { get; set; } = 12;
    public int StyleIndex { get; set; }
    public int DetailIndex { get; set; } = 1;
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 780;
    public bool WindowMaximized { get; set; }

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vecto", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch
        {
            // corrupt or unreadable settings must never block startup
        }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best-effort; a read-only profile must not crash shutdown
        }
    }
}
