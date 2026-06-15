using Domain.ValueObjects;

namespace Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }

    public PersonName Name { get; private set; } = null!;

    public PasswordHash PasswordHash { get; private set; } = null!;

    private User()
    {
    }

    public static User Create(Guid id, string name, string passwordHash)
    {
        if (id == Guid.Empty)
        {
            throw new Exceptions.DomainValidationException("User id cannot be empty.");
        }

        return new User
        {
            Id = id,
            Name = PersonName.Create(name),
            PasswordHash = PasswordHash.Create(passwordHash)
        };
    }

    public static User Restore(Guid id, string name, string passwordHash)
    {
        return new User
        {
            Id = id,
            Name = PersonName.FromPersistence(name),
            PasswordHash = PasswordHash.FromPersistence(passwordHash)
        };
    }

    public void UpdateName(string name)
    {
        Name = PersonName.Create(name);
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = PasswordHash.Create(passwordHash);
    }
}
