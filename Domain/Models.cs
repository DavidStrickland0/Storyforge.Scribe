namespace StoryForge.Scribe.Domain;

/// <summary>
/// A book is a collection of beats that the author gradually organizes. Beats are the atomic
/// writing unit and hold the actual prose. They can stay loose, join a scene, and the scene can
/// later be placed in a chapter.
/// </summary>
public sealed class Book
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled Book";
    public string Author { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public int WordGoal { get; set; } = 80_000;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Beats that exist and may be written but are not part of a scene yet.</summary>
    public List<Beat> Beats { get; set; } = [];

    /// <summary>Scenes that are not yet placed in a chapter.</summary>
    public List<Scene> Scenes { get; set; } = [];

    /// <summary>Scenes that have been gathered into chapters, in reading order.</summary>
    public List<Chapter> Chapters { get; set; } = [];
}

public sealed class Chapter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled chapter";
    public string Summary { get; set; } = "";
    public List<Scene> Scenes { get; set; } = [];
}

public sealed class Scene
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled scene";
    public string Summary { get; set; } = "";
    public string Pov { get; set; } = "";
    public string Setting { get; set; } = "";
    public List<Beat> Beats { get; set; } = [];
}

/// <summary>
/// The smallest unit of the book. A beat starts life as a brief and becomes prose once written.
/// </summary>
public sealed class Beat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Brief { get; set; } = "";
    public string Content { get; set; } = "";
    public BeatStatus Status { get; set; } = BeatStatus.Outline;
    public string Pov { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public enum BeatStatus
{
    Outline,
    Draft,
    Revised,
    Final
}
