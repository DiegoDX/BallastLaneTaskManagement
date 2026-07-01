using Application.DTOs.Llm;
using Application.DTOs.Tasks;
using Application.Llm;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Application.Llm;

public sealed class TaskSuggestionPromptBuilderTests
{
    [Fact]
    public void BuildChatRequest_includes_system_and_user_messages()
    {
        // Arrange
        var request = new TaskSuggestionRequest("Prepare Q2 financial report before month end");

        // Act
        var chatRequest = TaskSuggestionPromptBuilder.BuildChatRequest(request);

        // Assert
        chatRequest.Messages.Should().HaveCount(2);
        chatRequest.Messages[0].Role.Should().Be(LlmMessageRole.System);
        chatRequest.Messages[1].Role.Should().Be(LlmMessageRole.User);
        chatRequest.Messages[1].Content.Should().Be("Prepare Q2 financial report before month end");
    }

    [Fact]
    public void BuildChatRequest_system_message_enforces_title_max_length()
    {
        // Arrange
        var request = new TaskSuggestionRequest("Draft release notes");

        // Act
        var chatRequest = TaskSuggestionPromptBuilder.BuildChatRequest(request);
        var systemMessage = chatRequest.Messages[0].Content;

        // Assert
        systemMessage.Should().Contain($"at most {TaskTitle.MaxLength} characters");
        systemMessage.Should().Contain("{\"title\":\"...\",\"description\":\"...\"}");
        systemMessage.Should().Contain("JSON only");
    }

    [Fact]
    public void BuildChatRequest_trims_user_prompt()
    {
        // Arrange
        var request = new TaskSuggestionRequest("  Schedule team standup  ");

        // Act
        var chatRequest = TaskSuggestionPromptBuilder.BuildChatRequest(request);

        // Assert
        chatRequest.Messages[1].Content.Should().Be("Schedule team standup");
    }

    [Fact]
    public void BuildChatRequest_uses_default_temperature()
    {
        // Arrange
        var request = new TaskSuggestionRequest("Review pull requests");

        // Act
        var chatRequest = TaskSuggestionPromptBuilder.BuildChatRequest(request);

        // Assert
        chatRequest.Temperature.Should().Be(0.3);
    }

    [Fact]
    public void BuildChatRequest_throws_when_request_is_null()
    {
        // Act
        var act = () => TaskSuggestionPromptBuilder.BuildChatRequest(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
