using Application.DTOs.DocAssistant;
using Application.Llm.DocAssistant;
using FluentAssertions;

namespace Tests.Application.Llm.DocAssistant;

public sealed class TextChunkerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n")]
    public void Split_returns_empty_for_blank_input(string? text)
    {
        var chunks = TextChunker.Split(text);

        chunks.Should().BeEmpty();
    }

    [Fact]
    public void Split_returns_single_chunk_for_short_text()
    {
        const string text = "Authentication uses JWT Bearer tokens.";

        var chunks = TextChunker.Split(text);

        chunks.Should().ContainSingle().Which.Should().Be(text);
    }

    [Fact]
    public void Split_keeps_paragraphs_together_when_under_max_size()
    {
        const string paragraphOne = "First paragraph about JWT authentication.";
        const string paragraphTwo = "Second paragraph about refresh tokens.";
        var text = $"{paragraphOne}\n\n{paragraphTwo}";

        var chunks = TextChunker.Split(text);

        chunks.Should().ContainSingle().Which.Should().Be(text);
    }

    [Fact]
    public void Split_splits_on_paragraph_boundaries_before_max_size()
    {
        var paragraphOne = new string('A', DocAssistantLimits.MaxChunkChars - 10);
        var paragraphTwo = new string('B', 50);
        var text = $"{paragraphOne}\n\n{paragraphTwo}";

        var chunks = TextChunker.Split(text);

        chunks.Should().HaveCount(2);
        chunks[0].Should().Be(paragraphOne);
        chunks[1].Should().Contain(paragraphTwo);
    }

    [Fact]
    public void Split_chunks_do_not_exceed_max_size()
    {
        var paragraphs = Enumerable.Range(0, 12)
            .Select(index => $"Paragraph {index}: {new string('x', 120)}")
            .ToArray();
        var text = string.Join("\n\n", paragraphs);

        var chunks = TextChunker.Split(text);

        chunks.Should().NotBeEmpty();
        chunks.Should().OnlyContain(chunk => chunk.Length <= DocAssistantLimits.MaxChunkChars);
    }

    [Fact]
    public void Split_applies_overlap_between_consecutive_chunks()
    {
        var paragraphOne = new string('A', DocAssistantLimits.MaxChunkChars - 10);
        var paragraphTwo = new string('B', 200);
        var text = $"{paragraphOne}\n\n{paragraphTwo}";

        var chunks = TextChunker.Split(text);

        chunks.Should().HaveCount(2);
        chunks[1].Should().StartWith(chunks[0][^DocAssistantLimits.ChunkOverlap..]);
    }

    [Fact]
    public void Split_splits_long_paragraph_with_overlap()
    {
        var text = new string('Z', DocAssistantLimits.MaxChunkChars + 250);

        var chunks = TextChunker.Split(text);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(chunk => chunk.Length <= DocAssistantLimits.MaxChunkChars);

        for (var index = 1; index < chunks.Count; index++)
        {
            chunks[index].Should().StartWith(chunks[index - 1][^DocAssistantLimits.ChunkOverlap..]);
        }
    }

    [Fact]
    public void Split_normalizes_windows_line_endings()
    {
        const string text = "Line one\r\n\r\nLine two";

        var chunks = TextChunker.Split(text);

        chunks.Should().ContainSingle().Which.Should().Be("Line one\n\nLine two");
    }

    [Fact]
    public void Split_trims_surrounding_whitespace()
    {
        const string text = "  \n\n  Important documentation.  \n\n  ";

        var chunks = TextChunker.Split(text);

        chunks.Should().ContainSingle().Which.Should().Be("Important documentation.");
    }
}
