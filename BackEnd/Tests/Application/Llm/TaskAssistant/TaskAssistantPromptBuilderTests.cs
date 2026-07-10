using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.Llm.TaskAssistant;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Application.Llm.TaskAssistant;

public sealed class TaskAssistantPromptBuilderTests
{
    [Fact]
    public void BuildMessages_includes_system_message_and_user_history()
    {
        var messages = new List<TaskAssistantMessageDto>
        {
            new("user", "Create a task called Buy milk"),
            new("assistant", "Sure, I can help with that.")
        };

        var result = TaskAssistantPromptBuilder.BuildMessages(messages);

        result.Should().HaveCount(3);
        result[0].Role.Should().Be(LlmMessageRole.System);
        result[1].Role.Should().Be(LlmMessageRole.User);
        result[1].Content.Should().Be("Create a task called Buy milk");
        result[2].Role.Should().Be(LlmMessageRole.Assistant);
        result[2].Content.Should().Be("Sure, I can help with that.");
    }

    [Fact]
    public void BuildMessages_system_message_includes_task_assistant_rules()
    {
        var messages = new List<TaskAssistantMessageDto>
        {
            new("user", "Hello")
        };

        var result = TaskAssistantPromptBuilder.BuildMessages(messages);
        var systemMessage = result[0].Content;

        systemMessage.Should().Contain("task assistant");
        systemMessage.Should().Contain("create_task");
        systemMessage.Should().Contain("list_tasks");
        systemMessage.Should().Contain("get_task");
        systemMessage.Should().Contain("update_task");
        systemMessage.Should().Contain("delete_task");
        systemMessage.Should().Contain("YYYY-MM-DD");
        systemMessage.Should().Contain($"at most {TaskTitle.MaxLength} characters");
        systemMessage.Should().Contain("Do not invent task IDs");
        systemMessage.Should().Contain("explicitly confirmed");
        systemMessage.Should().Contain("Pending, InProgress, and Completed");
    }

    [Fact]
    public void BuildMessages_trims_message_content()
    {
        var messages = new List<TaskAssistantMessageDto>
        {
            new("user", "  Create a task  ")
        };

        var result = TaskAssistantPromptBuilder.BuildMessages(messages);

        result[1].Content.Should().Be("Create a task");
    }

    [Fact]
    public void BuildChatRequest_uses_default_temperature()
    {
        var messages = TaskAssistantPromptBuilder.BuildMessages(
        [
            new TaskAssistantMessageDto("user", "Hello")
        ]);

        var chatRequest = TaskAssistantPromptBuilder.BuildChatRequest(messages);

        chatRequest.Temperature.Should().Be(0.3);
    }

    [Fact]
    public void BuildMessages_throws_when_messages_is_null()
    {
        var act = () => TaskAssistantPromptBuilder.BuildMessages(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("user", LlmMessageRole.User)]
    [InlineData("USER", LlmMessageRole.User)]
    [InlineData("assistant", LlmMessageRole.Assistant)]
    [InlineData("Assistant", LlmMessageRole.Assistant)]
    public void MapRole_maps_allowed_roles(string role, LlmMessageRole expectedRole)
    {
        var result = TaskAssistantPromptBuilder.MapRole(role);

        result.Should().Be(expectedRole);
    }

    [Fact]
    public void MapRole_throws_for_unsupported_role()
    {
        var act = () => TaskAssistantPromptBuilder.MapRole("system");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported task assistant message role*");
    }
}
