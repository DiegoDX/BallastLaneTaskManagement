using Application.DTOs.TaskAssistant;
using FluentAssertions;
using Infrastructure.Mcp;

namespace Tests.Infrastructure.Mcp;

public sealed class McpToolResultParserTests
{
    [Fact]
    public void Parse_ShouldMapListedAction_WhenTasksAreReturned()
    {
        var result = McpToolResultParser.Parse("""{"success":true,"tasks":[]}""");

        result.Action.Should().NotBeNull();
        result.Action!.Type.Should().Be(TaskAssistantActionTypes.Listed);
    }

    [Fact]
    public void Parse_ShouldMapCreatedAction_WhenTaskIsCreated()
    {
        var taskId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var result = McpToolResultParser.Parse(
            $$"""{"success":true,"taskId":"{{taskId}}","title":"Study history","dueDate":"2026-07-10"}""");

        result.Action.Should().NotBeNull();
        result.Action!.Type.Should().Be(TaskAssistantActionTypes.Created);
        result.Action.TaskId.Should().Be(taskId);
        result.Action.Title.Should().Be("Study history");
    }

    [Fact]
    public void Parse_ShouldReturnNullAction_WhenToolFailed()
    {
        var result = McpToolResultParser.Parse("""{"success":false,"error":"Title is required."}""");

        result.Action.Should().BeNull();
        result.ResultJson.Should().Contain("Title is required.");
    }
}
