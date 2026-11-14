namespace StoryForge.Scribe.Domain;

public static class WritingStats
{
    private const double WordsPerMinute = 238d;

    /// <summary>
    /// Counts words without being fooled by punctuation, markdown or em dashes.
    /// Apostrophes and hyphens keep a word together ("don't", "well-known").
    /// </summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var count = 0;
        var inWord = false;

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }
            else if (c is '\'' or '\u2019' or '-' or '\u2011')
            {
                // Keep the word open; "well-known" and "don't" are single words.
            }
            else
            {
                inWord = false;
            }
        }

        return count;
    }

    /// <summary>Estimated reading time in minutes, rounded up.</summary>
    public static int ReadingMinutes(int words) =>
        words <= 0 ? 0 : (int)Math.Ceiling(words / WordsPerMinute);

    public static string FormatWords(int words) => words switch
    {
        < 1_000 => words.ToString("N0"),
        < 1_000_000 => (words / 1_000d).ToString("0.#") + "k",
        _ => (words / 1_000_000d).ToString("0.#") + "m"
    };
}
