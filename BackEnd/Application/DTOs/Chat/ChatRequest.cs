namespace Application.DTOs.Chat;

public sealed record ChatRequest(IReadOnlyList<ChatMessageDto> Messages);
