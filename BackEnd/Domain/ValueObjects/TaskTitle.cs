using Domain.Exceptions;

namespace Domain.ValueObjects;

public sealed class TaskTitle : IEquatable<TaskTitle>
{
    public const int MaxLength = 256;
    public string Value { get; }

    private TaskTitle(string value)
    {
        Value = value;
    }

    public static TaskTitle Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Title cannot be empty or whitespace.");
        }

        if (value.Length is > MaxLength)
        {
            throw new DomainValidationException("Title must be at most {MaxLength} characters long.");
        }

        return new TaskTitle(value.Trim());
    }

    internal static TaskTitle FromPersistence(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Title cannot be empty or whitespace.");
        }

        if (value.Length is > MaxLength)
        {
            throw new DomainValidationException("Title must be at most {MaxLength} characters long.");
        }

        return new TaskTitle(value);
    }

    public bool Equals(TaskTitle? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as TaskTitle);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;

    public static implicit operator string(TaskTitle title) => title.Value;
}
