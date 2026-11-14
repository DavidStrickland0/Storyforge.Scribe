namespace StoryForge.Scribe.Components.Workspace;

public enum OutlineActionKind
{
    SelectBeat,
    AddChapter,
    AddScene,
    AddLooseScene,
    AddBeat,
    ChooseBeatScene,
    ChooseSceneChapter,
    EditChapter,
    EditScene,
    DeleteChapter,
    DeleteScene,
    DeleteBeat,
    MoveChapter,
    MoveScene,
    MoveBeat
}

public sealed record OutlineAction(
    OutlineActionKind Kind,
    Guid? ChapterId = null,
    Guid? SceneId = null,
    Guid? BeatId = null,
    int Delta = 0);
