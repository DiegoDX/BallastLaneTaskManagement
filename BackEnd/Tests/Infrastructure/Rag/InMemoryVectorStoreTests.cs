using Application.Rag;
using FluentAssertions;
using Infrastructure.Rag;

namespace Tests.Infrastructure.Rag;

public sealed class InMemoryVectorStoreTests
{
    [Fact]
    public async Task UpsertAsync_replaces_entire_collection()
    {
        var store = new InMemoryVectorStore();
        var initialChunks = new[]
        {
            CreateChunk("1", "README.md", 0, [1f, 0f, 0f]),
            CreateChunk("2", "README.md", 1, [0f, 1f, 0f])
        };
        var replacementChunks = new[]
        {
            CreateChunk("3", "Requirements.docx", 0, [0f, 0f, 1f])
        };

        await store.UpsertAsync(initialChunks);
        await store.UpsertAsync(replacementChunks);

        var results = await store.SearchAsync([0f, 0f, 1f], topK: 10);

        results.Should().ContainSingle()
            .Which.Id.Should().Be("3");
    }

    [Fact]
    public async Task SearchAsync_returns_top_k_chunks_ordered_by_similarity_score()
    {
        var store = new InMemoryVectorStore();
        var chunks = new[]
        {
            CreateChunk("orthogonal", "README.md", 0, [0f, 1f, 0f]),
            CreateChunk("closest", "README.md", 1, [1f, 0f, 0f]),
            CreateChunk("partial", "README.md", 2, [0.7f, 0.7f, 0f])
        };
        await store.UpsertAsync(chunks);

        var results = await store.SearchAsync([1f, 0f, 0f], topK: 2);

        results.Should().HaveCount(2);
        results[0].Id.Should().Be("closest");
        results[1].Id.Should().Be("partial");
    }

    [Fact]
    public async Task SearchAsync_returns_empty_when_store_is_empty()
    {
        var store = new InMemoryVectorStore();

        var results = await store.SearchAsync([1f, 0f, 0f], topK: 4);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_returns_empty_when_top_k_is_zero()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync([CreateChunk("1", "README.md", 0, [1f, 0f, 0f])]);

        var results = await store.SearchAsync([1f, 0f, 0f], topK: 0);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_throws_when_chunks_is_null()
    {
        var store = new InMemoryVectorStore();

        var act = () => store.UpsertAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("chunks");
    }

    [Fact]
    public async Task SearchAsync_throws_when_query_embedding_is_null()
    {
        var store = new InMemoryVectorStore();

        var act = () => store.SearchAsync(null!, topK: 1);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("queryEmbedding");
    }

    [Fact]
    public async Task SearchAsync_throws_when_top_k_is_negative()
    {
        var store = new InMemoryVectorStore();

        var act = () => store.SearchAsync([1f], topK: -1);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("topK");
    }

    private static DocumentChunk CreateChunk(string id, string sourceFile, int chunkIndex, float[] embedding) =>
        new(id, sourceFile, chunkIndex, $"content-{id}", embedding);
}
