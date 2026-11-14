using StoryForge.Scribe.Services.Ai;

namespace StoryForge.Scribe.Components.Dialogs;

public enum EntityKind
{
    Chapter,
    Scene
}

public sealed record BookDraft(string Title, string Subtitle, string Author, int WordGoal);

public sealed record EntityDraft(string Title, string Summary, string Pov, string Setting);

public sealed record EntityEditorRequest(
    EntityKind Kind,
    Guid ChapterId,
    Guid? SceneId,
    string Title,
    string Summary,
    string Pov,
    string Setting);

public sealed record AiRunRequest(AiAction Action, string? Instruction = null);

public sealed record TextSelection(int Start, int End, string Text);

public sealed record MoveOption(Guid Id, string Label, string Note = "");
