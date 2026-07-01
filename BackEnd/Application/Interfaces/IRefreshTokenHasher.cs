namespace Application.Interfaces;

public interface IRefreshTokenHasher
{
    string Hash(string plainToken);
}
