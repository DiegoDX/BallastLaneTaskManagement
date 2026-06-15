using Domain.Exceptions;

namespace Domain.ValueObjects;

public sealed class PersonName : IEquatable<PersonName>
{
    public const int MaxLength = 256;

    public string Value { get; }

    private PersonName(string value)
    {
        Value = value;
    }

    public static PersonName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Name cannot be empty.");
        }

        if (value.Length is > MaxLength)
        {
            throw new DomainValidationException("Name must be at most {MaxLength} characters long.");
        }

        return new PersonName(value.Trim());
    }

    internal static PersonName FromPersistence(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Name cannot be empty.");
        }

        if (value.Length is > MaxLength)
        {
            throw new DomainValidationException("Name must be at most {MaxLength} characters long.");
        }

        return new PersonName(value);
    }

    public bool Equals(PersonName? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as PersonName);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;

    public static implicit operator string(PersonName name) => name.Value;
}
