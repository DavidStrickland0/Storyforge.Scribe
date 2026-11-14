namespace StoryForge.Scribe.Services;

/// <summary>Where the manuscript and its backups live.</summary>
public sealed class AppPaths
{
    public AppPaths(IWebHostEnvironment environment, IConfiguration configuration)
        : this(Path.Combine(environment.ContentRootPath, configuration["Storage:DataDirectory"] ?? "App_Data"))
    {
    }

    public AppPaths(string dataDirectory)
    {
        DataDirectory = Path.GetFullPath(dataDirectory);
        BackupsDirectory = Path.Combine(DataDirectory, "backups");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }

    public string DataDirectory { get; }
    public string BackupsDirectory { get; }
    public string BookFile => Path.Combine(DataDirectory, "book.json");
    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public string LogFile => Path.Combine(DataDirectory, "scribe.log");
}

public sealed class StorageOptions
{
    public string DataDirectory { get; set; } = "App_Data";
    public int AutosaveDebounceMs { get; set; } = 1200;
    public int BackupIntervalMinutes { get; set; } = 15;
    public int MaxBackups { get; set; } = 96;
}
