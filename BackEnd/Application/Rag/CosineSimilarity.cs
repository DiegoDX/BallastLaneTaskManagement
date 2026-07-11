namespace Application.Rag;

public static class CosineSimilarity
{
    public static float Compute(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Count != right.Count)
        {
            throw new ArgumentException(
                $"Vectors must have the same dimension. Left: {left.Count}, Right: {right.Count}.",
                nameof(right));
        }

        if (left.Count == 0)
        {
            return 0f;
        }

        double dotProduct = 0;
        double leftMagnitudeSquared = 0;
        double rightMagnitudeSquared = 0;

        for (var index = 0; index < left.Count; index++)
        {
            var leftValue = left[index];
            var rightValue = right[index];
            dotProduct += leftValue * rightValue;
            leftMagnitudeSquared += leftValue * leftValue;
            rightMagnitudeSquared += rightValue * rightValue;
        }

        if (leftMagnitudeSquared == 0 || rightMagnitudeSquared == 0)
        {
            return 0f;
        }

        return (float)(dotProduct / (Math.Sqrt(leftMagnitudeSquared) * Math.Sqrt(rightMagnitudeSquared)));
    }
}
