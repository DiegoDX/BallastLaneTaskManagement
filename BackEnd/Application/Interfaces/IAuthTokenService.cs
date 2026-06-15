namespace Application.Interfaces;

public interface IAuthTokenService
{
    string GenerateToken(Guid userId, string username);
}
