using Application.Rag;
using FluentAssertions;

namespace Tests.Application.Rag;

public sealed class CosineSimilarityTests
{
    [Fact]
    public void Compute_returns_one_for_identical_vectors()
    {
        float[] vector = [1f, 2f, 3f];

        var similarity = CosineSimilarity.Compute(vector, vector);

        similarity.Should().BeApproximately(1f, 0.0001f);
    }

    [Fact]
    public void Compute_returns_zero_for_orthogonal_vectors()
    {
        float[] left = [1f, 0f, 0f];
        float[] right = [0f, 1f, 0f];

        var similarity = CosineSimilarity.Compute(left, right);

        similarity.Should().Be(0f);
    }

    [Fact]
    public void Compute_returns_zero_for_zero_vectors()
    {
        float[] left = [0f, 0f, 0f];
        float[] right = [1f, 2f, 3f];

        var similarity = CosineSimilarity.Compute(left, right);

        similarity.Should().Be(0f);
    }

    [Fact]
    public void Compute_returns_zero_for_empty_vectors()
    {
        var similarity = CosineSimilarity.Compute([], []);

        similarity.Should().Be(0f);
    }

    [Fact]
    public void Compute_throws_when_dimensions_do_not_match()
    {
        float[] left = [1f, 2f];
        float[] right = [1f, 2f, 3f];

        var act = () => CosineSimilarity.Compute(left, right);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*same dimension*")
            .WithParameterName("right");
    }

    [Fact]
    public void Compute_throws_when_left_vector_is_null()
    {
        var act = () => CosineSimilarity.Compute(null!, [1f]);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("left");
    }

    [Fact]
    public void Compute_throws_when_right_vector_is_null()
    {
        var act = () => CosineSimilarity.Compute([1f], null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("right");
    }
}
