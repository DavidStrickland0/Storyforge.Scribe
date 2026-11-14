namespace StoryForge.Scribe.Services.Ai;

public static class AiProviders
{
    public const string Offline = "offline";
    public const string OpenAi = "openai";
    public const string Anthropic = "anthropic";
}

public sealed class AiSettings
{
    public string Provider { get; set; } = AiProviders.Offline;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
    public string ApiKey { get; set; } = "";
    public double Temperature { get; set; } = 0.85;
    public int MaxTokens { get; set; } = 900;

    /// <summary>
    /// Optional hint for OpenAI-compatible reasoning models (none, low, medium, high).
    /// Empty means the field is omitted so providers that don't know it are unaffected.
    /// </summary>
    public string ReasoningEffort { get; set; } = "";

    public bool IsOffline => string.Equals(Provider, AiProviders.Offline, StringComparison.OrdinalIgnoreCase);

    public AiSettings Clone() => (AiSettings)MemberwiseClone();

    public void Normalize()
    {
        Provider = (Provider ?? "").Trim().ToLowerInvariant();
        if (Provider is not (AiProviders.Offline or AiProviders.OpenAi or AiProviders.Anthropic))
        {
            Provider = AiProviders.OpenAi;
        }

        BaseUrl = (BaseUrl ?? "").Trim().TrimEnd('/');
        if (BaseUrl.Length == 0)
        {
            BaseUrl = Provider switch
            {
                AiProviders.Anthropic => "https://api.anthropic.com",
                _ => "https://api.openai.com/v1"
            };
        }

        Model = (Model ?? "").Trim();
        if (Model.Length == 0)
        {
            Model = Provider == AiProviders.Anthropic ? "claude-sonnet-4-5" : "gpt-4o-mini";
        }

        ApiKey = (ApiKey ?? "").Trim();
        Temperature = double.IsNaN(Temperature) ? 0.85 : Math.Clamp(Temperature, 0, 2);
        MaxTokens = Math.Clamp(MaxTokens <= 0 ? 900 : MaxTokens, 64, 8192);
        ReasoningEffort = (ReasoningEffort ?? "").Trim().ToLowerInvariant();
        if (ReasoningEffort is not ("" or "none" or "minimal" or "low" or "medium" or "high"))
        {
            ReasoningEffort = "";
        }
    }
}

public enum AiAction
{
    Continue,
    DraftBeat,
    BrainstormBeats,
    TightenProse,
    DescribeSetting,
    CritiqueScene,
    Freeform
}

public sealed record AiRequest(
    AiAction Action,
    string Subject,
    string Brief,
    string Pov,
    string System,
    string User);

public sealed class AiException(string message) : Exception(message);
