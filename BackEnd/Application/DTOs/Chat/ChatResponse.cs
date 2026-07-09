namespace Application.DTOs.Chat;

public sealed record ChatResponse(string Content, string? Model = null);
