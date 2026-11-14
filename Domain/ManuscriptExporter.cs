using System.Text;

namespace StoryForge.Scribe.Domain;

public static class ManuscriptExporter
{
    /// <summary>
    /// Produces a Markdown file in reading order. Chapters come first; scenes and beats that have
    /// not been organized yet are appended under clear headings so no prose is lost.
    /// </summary>
    public static string ToMarkdown(Book book)
    {
        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(Clean(book.Title));

        if (!string.IsNullOrWhiteSpace(book.Author))
        {
            sb.AppendLine().Append("by ").AppendLine(Clean(book.Author));
        }

        var wroteAnything = false;

        foreach (var chapter in book.Chapters)
        {
            var sceneProse = chapter.Scenes
                .Select(scene => scene.Beats
                    .Where(b => !string.IsNullOrWhiteSpace(b.Content))
                    .Select(b => b.Content.Trim())
                    .ToList())
                .Where(prose => prose.Count > 0)
                .ToList();

            if (sceneProse.Count == 0)
            {
                continue;
            }

            wroteAnything = true;
            sb.AppendLine();
            sb.AppendLine("## " + Clean(chapter.Title));
            sb.AppendLine();

            for (var i = 0; i < sceneProse.Count; i++)
            {
                if (i > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("* * *");
                    sb.AppendLine();
                }

                foreach (var text in sceneProse[i])
                {
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
            }
        }

        foreach (var scene in book.Scenes)
        {
            var prose = scene.Beats
                .Where(b => !string.IsNullOrWhiteSpace(b.Content))
                .Select(b => b.Content.Trim())
                .ToList();

            if (prose.Count == 0)
            {
                continue;
            }

            wroteAnything = true;
            sb.AppendLine();
            sb.AppendLine("## " + Clean(scene.Title));
            sb.AppendLine();

            foreach (var text in prose)
            {
                sb.AppendLine(text);
                sb.AppendLine();
            }
        }

        var looseBeats = book.Beats.Where(b => !string.IsNullOrWhiteSpace(b.Content)).ToList();
        if (looseBeats.Count > 0)
        {
            wroteAnything = true;
            sb.AppendLine();
            sb.AppendLine("## Unassigned beats");
            sb.AppendLine();

            foreach (var beat in looseBeats)
            {
                sb.AppendLine("### " + Clean(beat.Title));
                sb.AppendLine();
                sb.AppendLine(beat.Content.Trim());
                sb.AppendLine();
            }
        }

        if (!wroteAnything)
        {
            sb.AppendLine();
            sb.AppendLine("_No prose written yet._");
        }

        return sb.ToString().Replace("\r\n", "\n").TrimEnd() + "\n";
    }

    public static string Slug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "manuscript";
        }

        var sb = new StringBuilder();
        foreach (var c in value.Trim())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else if ((c is ' ' or '-' or '_') && sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }

        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "manuscript" : slug;
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Untitled" : value.Trim();
}
