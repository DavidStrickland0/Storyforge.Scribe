using System.Text;
using StoryForge.Scribe.Domain;

namespace StoryForge.Scribe.Services.Ai;

/// <summary>
/// Turns a beat, its scene, and the author's request into a focused prompt. Context is kept
/// tight so the model spends its budget on prose rather than repeated background.
/// </summary>
public static class AiPrompts
{
    public const string SystemPrompt = """
        You are a seasoned fiction editor and ghostwriter collaborating with the author of an original novel. Match the author's voice, tense, and point of view. Write vivid, concrete prose; avoid clichés, summary, and purple prose. Maintain continuity with the supplied context. Never mention being an AI, never explain what you are doing, and never wrap prose in quotation marks or markdown fences. When asked for prose, return prose only. When asked for notes, use short, specific bullet points.
        """;

    public static AiRequest Build(
        AiAction action,
        BeatLocation location,
        Book book,
        string? selection = null,
        string? instruction = null)
    {
        var beat = location.Beat;
        var scene = location.Scene;
        var subject = string.IsNullOrWhiteSpace(beat.Title) ? "Untitled beat" : beat.Title.Trim();
        var brief = beat.Brief?.Trim() ?? "";
        var pov = !string.IsNullOrWhiteSpace(beat.Pov) ? beat.Pov.Trim()
            : !string.IsNullOrWhiteSpace(scene?.Pov) ? scene.Pov.Trim()
            : "the viewpoint character";

        var context = BuildContext(location, book);

        var user = action switch
        {
            AiAction.Continue => BuildContinue(context, beat),
            AiAction.DraftBeat => BuildDraft(context, beat, brief),
            AiAction.BrainstormBeats => BuildBrainstorm(context, beat, scene),
            AiAction.TightenProse => BuildTighten(context, beat, selection),
            AiAction.DescribeSetting => BuildSetting(context, scene, pov),
            AiAction.CritiqueScene => BuildCritique(context, beat, scene),
            _ => BuildFreeform(context, beat, selection, instruction)
        };

        return new AiRequest(action, subject, brief, pov, SystemPrompt, user);
    }

    private static string BuildContext(BeatLocation location, Book book)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Story: {Clean(book.Title)}");

        if (location.Chapter is { } chapter)
        {
            sb.AppendLine($"Chapter {location.ChapterIndex + 1}: {Clean(chapter.Title)}");
            if (!string.IsNullOrWhiteSpace(chapter.Summary))
            {
                sb.AppendLine($"Chapter summary: {chapter.Summary.Trim()}");
            }
        }

        if (location.Scene is { } scene)
        {
            sb.AppendLine($"Scene {location.SceneIndex + 1}: {Clean(scene.Title)}");
            if (!string.IsNullOrWhiteSpace(scene.Summary))
            {
                sb.AppendLine($"Scene summary: {scene.Summary.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(scene.Pov))
            {
                sb.AppendLine($"Scene POV: {scene.Pov.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(scene.Setting))
            {
                sb.AppendLine($"Setting: {scene.Setting.Trim()}");
            }

            if (location.Chapter is null)
            {
                sb.AppendLine("This scene is written but not yet placed in a chapter.");
            }
        }
        else
        {
            sb.AppendLine("This beat stands on its own; it is not yet part of a scene.");
        }

        sb.AppendLine($"Beat: {Clean(location.Beat.Title)}");
        if (!string.IsNullOrWhiteSpace(location.Beat.Pov) || !string.IsNullOrWhiteSpace(location.Scene?.Pov))
        {
            sb.AppendLine($"Beat POV: {(!string.IsNullOrWhiteSpace(location.Beat.Pov) ? location.Beat.Pov.Trim() : location.Scene!.Pov.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(location.Beat.Brief))
        {
            sb.AppendLine($"Beat brief: {location.Beat.Brief.Trim()}");
        }

        var previous = Outline.Previous(book, location.Beat.Id);
        if (previous is { } p && !string.IsNullOrWhiteSpace(p.Beat.Content))
        {
            sb.AppendLine();
            sb.AppendLine("Ending of the previous beat (for continuity and voice):");
            sb.AppendLine("<<<");
            sb.AppendLine(Outline.Tail(p.Beat.Content, 1_400));
            sb.AppendLine(">>>");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildContinue(string context, Beat beat)
    {
        var tail = Outline.Tail(beat.Content, 5_000);
        var instruction = tail.Length == 0
            ? "This beat is still blank. Write its opening 2–3 paragraphs (roughly 150–250 words), starting in motion."
            : "Continue the scene from the exact point where the text stops. Write the next 2–4 paragraphs (roughly 150–300 words). Do not repeat or summarize existing text.";

        return $"""
            {context}

            TEXT SO FAR IN THIS BEAT:
            <<<
            {(tail.Length == 0 ? "(blank)" : tail)}
            >>>

            {instruction} Keep the existing voice and point of view. Stop at a natural turn. Reply with prose only.
            """;
    }

    private static string BuildDraft(string context, Beat beat, string brief)
    {
        var existing = Outline.Tail(beat.Content, 2_500);
        var briefBlock = brief.Length == 0
            ? "No brief was written; infer the beat's job from the scene summary."
            : $"The author's brief for this beat: {brief}";

        return $"""
            {context}

            {briefBlock}

            {(existing.Length == 0 ? "" : $"EXISTING DRAFT (possibly partial; you may rework it):\n<<<\n{existing}\n>>>\n")}
            Write a complete first draft of this beat as finished prose, roughly 350–550 words. Dramatize the brief rather than checking it off. Start in motion with a concrete sensory detail, give the characters something they want in the moment, and end on a turn or discovery. Reply with prose only.
            """;
    }

    private static string BuildBrainstorm(string context, Beat beat, Scene? scene)
    {
        var existing = Outline.Tail(beat.Content, 1_800);
        var sceneState = existing.Length == 0
            ? "The beat is not written yet."
            : $"The beat so far:\n<<<\n{existing}\n>>>";
        var ask = scene is null
            ? "Propose 5–7 concrete beats that could follow this one"
            : "Propose 5–7 concrete beats that could carry this scene forward";

        return $"""
            {context}

            {sceneState}

            {ask}. Use this exact line format, one per line:
            - Beat title — one-sentence brief

            Vary the dramatic function: complication, reversal, quiet character beat, reveal, decision, consequence. Stay specific to these characters and this situation. No preamble and no wrap-up after the list.
            """;
    }

    private static string BuildTighten(string context, Beat beat, string? selection)
    {
        var passage = !string.IsNullOrWhiteSpace(selection) ? selection.Trim() : beat.Content.Trim();
        var label = !string.IsNullOrWhiteSpace(selection) ? "SELECTED PASSAGE" : "BEAT TEXT";

        return $"""
            {context}

            {label} TO LINE-EDIT:
            <<<
            {passage}
            >>>

            Line-edit the passage. Preserve the author's voice, tense, point of view, and all necessary information. Cut filter words, redundancies, and soft abstractions; sharpen verbs; vary sentence rhythm. Reduce the length by roughly 10–20%. Keep paragraph breaks intact. If a line is already strong, leave it alone. Return only the revised passage.
            """;
    }

    private static string BuildSetting(string context, Scene? scene, string pov)
    {
        var known = scene is null || string.IsNullOrWhiteSpace(scene.Setting)
            ? "The setting is not described yet."
            : "";
        var subject = scene is null ? "this beat's" : "this scene's";

        return $"""
            {context}

            {known}

            Write a 120–180 word sensory description of {subject} setting from {pov}'s close point of view. Use at least three senses, include one concrete detail that carries emotional weight, and avoid establishing-shot clichés. Do not use a list or heading. Reply with prose only.
            """;
    }

    private static string BuildCritique(string context, Beat beat, Scene? scene)
    {
        var text = Outline.Tail(beat.Content, 6_000);
        var scope = scene is null ? "this beat" : "this beat and its scene";

        return $"""
            {context}

            BEAT TEXT:
            <<<
            {(text.Length == 0 ? "(not written yet)" : text)}
            >>>

            Give the author 5–7 tight bullet notes on {scope}. Cover: what is working; where tension sags; point-of-view or dialogue opportunities; one concrete detail worth expanding. Quote a short phrase when it helps. Do not rewrite the passage. No preamble.
            """;
    }

    private static string BuildFreeform(string context, Beat beat, string? selection, string? instruction)
    {
        var ask = string.IsNullOrWhiteSpace(instruction)
            ? "Offer one concrete, craft-level next step for this beat."
            : instruction.Trim();
        var text = Outline.Tail(beat.Content, 3_500);
        var selected = string.IsNullOrWhiteSpace(selection) ? "" : $"\n\nSELECTED PASSAGE:\n<<<\n{selection.Trim()}\n>>>";

        return $"""
            {context}

            CURRENT BEAT TEXT:
            <<<
            {(text.Length == 0 ? "(blank)" : text)}
            >>>{selected}

            THE AUTHOR ASKS:
            {ask}

            Respond as their trusted writing partner. If they ask for prose, reply with prose only. If they ask for advice, be specific and concise.
            """;
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Untitled" : value.Trim();
}
