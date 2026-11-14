namespace StoryForge.Scribe.Domain;

/// <summary>
/// Where a beat lives. A beat can be loose (no scene), inside a scene that is not yet in a
/// chapter, or fully placed. Indices are -1 for levels that do not exist.
/// </summary>
public readonly record struct BeatLocation(
    Chapter? Chapter,
    Scene? Scene,
    Beat Beat,
    int ChapterIndex,
    int SceneIndex,
    int BeatIndex)
{
    public bool IsLoose => Scene is null;

    public bool IsInUnassignedScene => Scene is not null && Chapter is null;
}

/// <summary>Pure helpers for walking, counting, and rearranging the manuscript.</summary>
public static class Outline
{
    /// <summary>
    /// Walks beats in reading order: chapter material first, then scenes waiting for a chapter,
    /// then loose beats.
    /// </summary>
    public static IEnumerable<BeatLocation> Locations(Book book)
    {
        for (var c = 0; c < book.Chapters.Count; c++)
        {
            var chapter = book.Chapters[c];
            for (var s = 0; s < chapter.Scenes.Count; s++)
            {
                var scene = chapter.Scenes[s];
                for (var b = 0; b < scene.Beats.Count; b++)
                {
                    yield return new BeatLocation(chapter, scene, scene.Beats[b], c, s, b);
                }
            }
        }

        for (var s = 0; s < book.Scenes.Count; s++)
        {
            var scene = book.Scenes[s];
            for (var b = 0; b < scene.Beats.Count; b++)
            {
                yield return new BeatLocation(null, scene, scene.Beats[b], -1, s, b);
            }
        }

        for (var b = 0; b < book.Beats.Count; b++)
        {
            yield return new BeatLocation(null, null, book.Beats[b], -1, -1, b);
        }
    }

    /// <summary>Every scene in the book, paired with its chapter when it has one.</summary>
    public static IEnumerable<(Chapter? Chapter, Scene Scene)> AllScenes(Book book)
    {
        foreach (var chapter in book.Chapters)
        {
            foreach (var scene in chapter.Scenes)
            {
                yield return (chapter, scene);
            }
        }

        foreach (var scene in book.Scenes)
        {
            yield return (null, scene);
        }
    }

    public static BeatLocation? Find(Book book, Guid beatId)
    {
        foreach (var location in Locations(book))
        {
            if (location.Beat.Id == beatId)
            {
                return location;
            }
        }

        return null;
    }

    public static Scene? FindScene(Book book, Guid sceneId) =>
        AllScenes(book).Select(pair => pair.Scene).FirstOrDefault(scene => scene.Id == sceneId);

    public static BeatLocation? Neighbour(Book book, Guid beatId, int delta)
    {
        var all = Locations(book).ToList();
        var index = all.FindIndex(l => l.Beat.Id == beatId);
        if (index < 0)
        {
            return null;
        }

        var target = index + delta;
        return target >= 0 && target < all.Count ? all[target] : null;
    }

    public static BeatLocation? Next(Book book, Guid beatId) => Neighbour(book, beatId, 1);
    public static BeatLocation? Previous(Book book, Guid beatId) => Neighbour(book, beatId, -1);

    public static IList<Beat>? BeatContainer(Book book, Beat beat)
    {
        if (book.Beats.Contains(beat))
        {
            return book.Beats;
        }

        foreach (var (_, scene) in AllScenes(book))
        {
            if (scene.Beats.Contains(beat))
            {
                return scene.Beats;
            }
        }

        return null;
    }

    public static IList<Scene>? SceneContainer(Book book, Scene scene)
    {
        if (book.Scenes.Contains(scene))
        {
            return book.Scenes;
        }

        foreach (var chapter in book.Chapters)
        {
            if (chapter.Scenes.Contains(scene))
            {
                return chapter.Scenes;
            }
        }

        return null;
    }

    /// <summary>Moves a beat into a scene, or back to the loose pool when <paramref name="target"/> is null.</summary>
    public static bool PlaceBeat(Book book, Beat beat, Scene? target)
    {
        var source = BeatContainer(book, beat);
        if (source is null)
        {
            return false;
        }

        if (target is not null && ReferenceEquals(source, target.Beats))
        {
            return false;
        }

        source.Remove(beat);
        (target?.Beats ?? book.Beats).Add(beat);
        return true;
    }

    /// <summary>Moves a scene into a chapter, or back to the unassigned pool when <paramref name="target"/> is null.</summary>
    public static bool PlaceScene(Book book, Scene scene, Chapter? target)
    {
        var source = SceneContainer(book, scene);
        if (source is null)
        {
            return false;
        }

        if (target is not null && ReferenceEquals(source, target.Scenes))
        {
            return false;
        }

        source.Remove(scene);
        (target?.Scenes ?? book.Scenes).Add(scene);
        return true;
    }

    public static int BeatWords(Beat beat) => WritingStats.CountWords(beat.Content);

    public static int SceneWords(Scene scene) => scene.Beats.Sum(BeatWords);

    public static int ChapterWords(Chapter chapter) => chapter.Scenes.Sum(SceneWords);

    public static int LooseBeatWords(Book book) => book.Beats.Sum(BeatWords);

    public static int BookWords(Book book) =>
        book.Chapters.Sum(ChapterWords) + book.Scenes.Sum(SceneWords) + LooseBeatWords(book);

    public static int WrittenBeats(Book book) =>
        Locations(book).Count(l => !string.IsNullOrWhiteSpace(l.Beat.Content));

    public static int TotalBeats(Book book) => Locations(book).Count();

    public static bool Move<T>(IList<T> list, T item, int delta)
    {
        var index = list.IndexOf(item);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= list.Count)
        {
            return false;
        }

        list.RemoveAt(index);
        list.Insert(target, item);
        return true;
    }

    public static bool MoveBeat(Book book, Beat beat, int delta)
    {
        var container = BeatContainer(book, beat);
        return container is not null && Move(container, beat, delta);
    }

    public static bool MoveScene(Book book, Scene scene, int delta)
    {
        var container = SceneContainer(book, scene);
        return container is not null && Move(container, scene, delta);
    }

    public static bool MoveChapter(Book book, Chapter chapter, int delta) => Move(book.Chapters, chapter, delta);

    /// <summary>The last few characters of a beat's prose, for AI continuity.</summary>
    public static string Tail(string? text, int maxChars)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        if (text.Length <= maxChars)
        {
            return text;
        }

        var start = text.Length - maxChars;
        var newline = text.IndexOf('\n', start);
        return newline >= 0 && newline < text.Length - 1
            ? text[(newline + 1)..]
            : text[start..];
    }
}
