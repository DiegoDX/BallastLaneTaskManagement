using Application.Interfaces;
using Application.Rag;
using FluentAssertions;
using Moq;

namespace Tests.Application.Rag;

public sealed class RagRetrieverTests
{
    private readonly Mock<IEmbeddingClient> _embeddingClientMock = new();
    private readonly Mock<IVectorStore> _vectorStoreMock = new();
    private readonly RagRetriever _sut;

    public RagRetrieverTests()
    {
        _sut = new RagRetriever(_embeddingClientMock.Object, _vectorStoreMock.Object);
    }

    [Fact]
    public async Task RetrieveAsync_embeds_question_and_returns_top_k_chunks()
    {
        const string question = "How does authentication work?";
        var queryEmbedding = new[] { 1f, 0f, 0f };
        var expectedChunks = new[]
        {
            CreateChunk("1", "README.md", 0, queryEmbedding),
            CreateChunk("2", "Requirements.docx", 1, queryEmbedding)
        };

        _embeddingClientMock
            .Setup(client => client.EmbedAsync(question, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorStoreMock
            .Setup(store => store.SearchAsync(queryEmbedding, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChunks);

        var result = await _sut.RetrieveAsync(question, topK: 2);

        result.Should().BeEquivalentTo(expectedChunks);

        _embeddingClientMock.Verify(
            client => client.EmbedAsync(question, It.IsAny<CancellationToken>()),
            Times.Once);

        _vectorStoreMock.Verify(
            store => store.SearchAsync(queryEmbedding, 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RetrieveAsync_throws_when_question_is_blank(string? question)
    {
        var act = () => _sut.RetrieveAsync(question!, topK: 4);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static DocumentChunk CreateChunk(string id, string sourceFile, int chunkIndex, float[] embedding) =>
        new(id, sourceFile, chunkIndex, $"content-{id}", embedding);
}
