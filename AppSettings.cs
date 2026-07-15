using System.Text.Json;

namespace LuaDecompilerDesktop;

internal sealed class AppSettings
{
    public int Version { get; set; }
    public string OutputPath { get; set; } = DefaultOutputPath();
    public bool UseCustomOutputPath { get; set; }
    public int UiSpacing { get; set; } = 12;
    public int SplitterDistance { get; set; } = 520;
    public double SplitterRatio { get; set; } = 0.45;

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LuaDecompiler");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return NewDefaults();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
            if (settings.Version < 5)
            {
                settings.Version = 5;
                settings.OutputPath = DefaultOutputPath();
                settings.UseCustomOutputPath = false;
                settings.SplitterDistance = 520;
                settings.SplitterRatio = 0.45;
            }
            return settings;
        }
        catch
        {
            return NewDefaults();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string DefaultOutputPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Decompiled");
    }

    public string ResolveOutputPath()
    {
        return UseCustomOutputPath && !string.IsNullOrWhiteSpace(OutputPath)
            ? OutputPath
            : DefaultOutputPath();
    }

    public static bool IsLikelyRunningFromArchiveTemporaryDirectory()
    {
        return IsLikelyArchiveTemporaryDirectory(AppContext.BaseDirectory, Path.GetTempPath());
    }

    internal static bool IsLikelyArchiveTemporaryDirectory(string baseDirectory, string tempDirectory)
    {
        var fullBaseDirectory = Path.GetFullPath(baseDirectory);
        var fullTempDirectory = Path.GetFullPath(tempDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullBaseDirectory.StartsWith(fullTempDirectory, StringComparison.OrdinalIgnoreCase)
            && fullBaseDirectory.Contains(".zip.", StringComparison.OrdinalIgnoreCase);
    }

    private static AppSettings NewDefaults() => new() { Version = 5 };
}
