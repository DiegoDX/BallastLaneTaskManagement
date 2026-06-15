using Domain.Exceptions;

namespace Domain.ValueObjects;

public sealed class DueDate : IEquatable<DueDate>
{
    public DateTime Value { get; }

    private DueDate(DateTime value)
    {
        Value = value;
    }

    public static DueDate Create(DateTime value)
    {
        if (value == default)
        {
            throw new DomainValidationException("Due date cannot be empty.");
        }

        if (value < DateTime.Today.Date)
        {
            throw new DomainValidationException("Due date must be in the future.");
        }

        return new DueDate(value);
    }

    internal static DueDate FromPersistence(DateTime value)
    {
        if (value == default)
        {
            throw new DomainValidationException("Due date cannot be empty.");
        }

        return new DueDate(value);
    }

    public bool Equals(DueDate? other)
    {
        if (other is null)
        {
            return false;
        }

        return Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as DueDate);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString("O");

    public static implicit operator DateTime(DueDate dueDate) => dueDate.Value;
}
