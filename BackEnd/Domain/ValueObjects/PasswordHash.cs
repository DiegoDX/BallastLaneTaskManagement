using Domain.Exceptions;

namespace Domain.ValueObjects;

public sealed class PasswordHash : IEquatable<PasswordHash>
{
    public string Value { get; }

    private PasswordHash(string value)
    {
        Value = value;
    }

    public static PasswordHash Create(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new DomainValidationException("Password hash cannot be empty.");
        }

        return new PasswordHash(value);
    }

    internal static PasswordHash FromPersistence(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new DomainValidationException("Password hash cannot be empty.");
        }

        return new PasswordHash(value);
    }

    public bool Equals(PasswordHash? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as PasswordHash);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;

    public static implicit operator string(PasswordHash passwordHash) => passwordHash.Value;
}
