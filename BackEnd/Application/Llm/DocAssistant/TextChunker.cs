using System.Text;
using Application.DTOs.DocAssistant;

namespace Application.Llm.DocAssistant;

public static class TextChunker
{
    public static IReadOnlyList<string> Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var paragraphs = SplitParagraphs(Normalize(text));
        if (paragraphs.Count == 0)
        {
            return [];
        }

        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            AppendParagraph(current, paragraph, chunks);
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }

    private static void AppendParagraph(StringBuilder current, string paragraph, List<string> chunks)
    {
        if (paragraph.Length > DocAssistantLimits.MaxChunkChars)
        {
            if (current.Length > 0)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }

            chunks.AddRange(SplitLongText(paragraph));
            return;
        }

        if (current.Length == 0)
        {
            current.Append(paragraph);
            return;
        }

        var combinedLength = current.Length + "\n\n".Length + paragraph.Length;
        if (combinedLength <= DocAssistantLimits.MaxChunkChars)
        {
            current.Append("\n\n").Append(paragraph);
            return;
        }

        var finished = current.ToString();
        chunks.Add(finished);
        current.Clear();

        var overlap = GetOverlap(finished);
        var nextWithOverlap = string.IsNullOrEmpty(overlap)
            ? paragraph
            : overlap + "\n\n" + paragraph;

        if (nextWithOverlap.Length <= DocAssistantLimits.MaxChunkChars)
        {
            current.Append(nextWithOverlap);
            return;
        }

        current.Append(paragraph);
    }

    private static string GetOverlap(string text)
    {
        if (text.Length <= DocAssistantLimits.ChunkOverlap)
        {
            return text;
        }

        return text[^DocAssistantLimits.ChunkOverlap..];
    }

    private static IEnumerable<string> SplitLongText(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(DocAssistantLimits.MaxChunkChars, text.Length - start);
            yield return text.Substring(start, length);

            if (start + length >= text.Length)
            {
                break;
            }

            start += length - DocAssistantLimits.ChunkOverlap;
        }
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

    private static List<string> SplitParagraphs(string text) =>
        text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(paragraph => paragraph.Length > 0)
            .ToList();
}
