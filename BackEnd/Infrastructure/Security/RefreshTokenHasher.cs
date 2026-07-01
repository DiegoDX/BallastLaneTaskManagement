using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;

namespace Infrastructure.Security;

public sealed class RefreshTokenHasher : IRefreshTokenHasher
{
    public string Hash(string plainToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainToken);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(hashBytes);
    }
}
