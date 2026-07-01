namespace Application.DTOs.Auth;

public sealed record LoginSessionResult(
    LoginResponse Response,
    string RefreshTokenPlain,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record RefreshSessionResult(
    RefreshResponse Response,
    string RefreshTokenPlain,
    DateTime RefreshTokenExpiresAtUtc);
