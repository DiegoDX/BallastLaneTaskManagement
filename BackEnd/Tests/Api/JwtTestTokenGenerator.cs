using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Tests.Api;

internal static class JwtTestTokenGenerator
{
    private const string TestSecret = "ReplaceWithAtLeast32CharacterDevelopmentSecretKey";//"IntegrationTestSecretKeyAtLeast32Chars!";
    private const string TestIssuer = "BallastLane";
    private const string TestAudience = "BallastLane";

    public static string CreateExpiredToken(Guid userId, string username)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(-10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string CreateInvalidSignatureToken(Guid userId, string username)
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("WrongSecretKeyThatDoesNotMatchApiSettings!"));

        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username)
        };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public const string MalformedToken = "not.a.valid.jwt.token";
}
