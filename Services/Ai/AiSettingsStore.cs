using System.Text;

namespace StoryForge.Scribe.Services.Ai;

/// <summary>
/// AI connection settings live beside the manuscript so they survive restarts. API keys can also
/// come from environment variables (STORYFORGE_AI_API_KEY, OPENAI_API_KEY, ANTHROPIC_API_KEY),
/// which always win over the file.
/// </summary>
public sealed class AiSettingsStore
{
    private readonly AppPaths _paths;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiSettingsStore> _logger;
    private readonly object _gate = new();
    private AiSettings _current;

    public AiSettingsStore(AppPaths paths, IConfiguration configuration, ILogger<AiSettingsStore> logger)
    {
        _paths = paths;
        _configuration = configuration;
        _logger = logger;
        _current = Load();
    }

    public event Action? Changed;

    public AiSettings Current
    {
        get { lock (_gate) { return _current.Clone(); } }
    }

    public bool HasEnvironmentKey => !string.IsNullOrWhiteSpace(EnvironmentKey());

    public string? EnvironmentKey()
    {
        var explicitKey = Environment.GetEnvironmentVariable("STORYFORGE_AI_API_KEY");
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return explicitKey.Trim();
        }

        var provider = Current.Provider;
        var name = provider == AiProviders.Anthropic ? "ANTHROPIC_API_KEY" : "OPENAI_API_KEY";
        var key = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    public string ResolveApiKey(AiSettings settings) =>
        EnvironmentKey() ?? settings.ApiKey;

    public void Save(AiSettings settings)
    {
        var copy = settings.Clone();
        copy.Normalize();

        try
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            var temp = _paths.SettingsFile + ".tmp";
            File.WriteAllText(temp, AppJson.Serialize(copy), new UTF8Encoding(false));
            File.Move(temp, _paths.SettingsFile, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save AI settings.");
            throw new InvalidOperationException("Could not save settings. Is the App_Data folder writable?", ex);
        }

        lock (_gate)
        {
            _current = copy;
        }

        Changed?.Invoke();
    }

    private AiSettings Load()
    {
        var defaults = new AiSettings();
        try
        {
            var section = _configuration.GetSection("Ai");
            if (section.Exists())
            {
                section.Bind(defaults);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the Ai configuration section.");
        }

        if (File.Exists(_paths.SettingsFile))
        {
            try
            {
                var saved = AppJson.Deserialize<AiSettings>(File.ReadAllText(_paths.SettingsFile));
                if (saved is not null)
                {
                    defaults = saved;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI settings could not be read; defaults are in use.");
            }
        }

        defaults.Normalize();
        return defaults;
    }
}
