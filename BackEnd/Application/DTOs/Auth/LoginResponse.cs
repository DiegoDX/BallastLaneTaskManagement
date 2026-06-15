namespace Application.DTOs.Auth;

public sealed record LoginResponse(Guid UserId, string Username, string Token);
