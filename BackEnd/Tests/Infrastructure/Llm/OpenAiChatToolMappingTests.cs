using Application.DTOs.Llm;
using FluentAssertions;
using Infrastructure.Llm.Mapping;
using OpenAI.Chat;

namespace Tests.Infrastructure.Llm;

public sealed class OpenAiChatToolMappingTests
{
    [Fact]
    public void ToOpenAiTools_maps_tool_definitions_to_function_tools()
    {
        var tools = new List<LlmToolDefinition>
        {
            new(
                "create_task",
                "Creates a task.",
                """
                {
                  "type": "object",
                  "properties": {
                    "title": { "type": "string" },
                    "dueDate": { "type": "string" }
                  },
                  "required": ["title", "dueDate"]
                }
                """)
        };

        var openAiTools = OpenAiChatToolMapper.ToOpenAiTools(tools);

        openAiTools.Should().HaveCount(1);
        openAiTools[0].Kind.Should().Be(ChatToolKind.Function);
        openAiTools[0].FunctionName.Should().Be("create_task");
        openAiTools[0].FunctionDescription.Should().Be("Creates a task.");
        openAiTools[0].FunctionParameters.ToString().Should().Contain("dueDate");
    }

    [Fact]
    public void ToOpenAiOptionsWithTools_includes_mapped_tools()
    {
        var request = new LlmChatRequest(
            [new LlmMessage(LlmMessageRole.User, "Create a task")],
            Temperature: 0.3,
            MaxOutputTokens: 256);

        var tools = new List<LlmToolDefinition>
        {
            new("create_task", "Creates a task.", """{"type":"object","properties":{}}""")
        };

        var options = OpenAiChatCompletionOptionsMapper.ToOpenAiOptionsWithTools(request, tools);

        options.Tools.Should().HaveCount(1);
        options.Tools[0].FunctionName.Should().Be("create_task");
        options.MaxOutputTokenCount.Should().Be(256);
    }

    [Fact]
    public void ToOpenAiMessagesWithTools_maps_assistant_tool_calls_and_tool_messages()
    {
        var toolCall = new LlmToolCall(
            "call_1",
            "create_task",
            """{"title":"Buy milk","dueDate":"2026-07-10"}""");

        var messages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, "You are helpful."),
            new(LlmMessageRole.User, "Create a task"),
            new(LlmMessageRole.Assistant, string.Empty, ToolCalls: [toolCall]),
            new(LlmMessageRole.Tool, """{"success":true}""", ToolCallId: "call_1")
        };

        var openAiMessages = OpenAiChatMessageMapper.ToOpenAiMessagesWithTools(messages);

        openAiMessages.Should().HaveCount(4);
        openAiMessages[0].Should().BeOfType<SystemChatMessage>();
        openAiMessages[1].Should().BeOfType<UserChatMessage>();

        var assistantMessage = openAiMessages[2].Should().BeOfType<AssistantChatMessage>().Subject;
        assistantMessage.ToolCalls.Should().HaveCount(1);
        assistantMessage.ToolCalls[0].Id.Should().Be("call_1");
        assistantMessage.ToolCalls[0].FunctionName.Should().Be("create_task");
        assistantMessage.ToolCalls[0].FunctionArguments.ToString().Should().Contain("Buy milk");

        var toolMessage = openAiMessages[3].Should().BeOfType<ToolChatMessage>().Subject;
        toolMessage.ToolCallId.Should().Be("call_1");
        toolMessage.Content[0].Text.Should().Contain("success");
    }

    [Fact]
    public void ToOpenAiMessages_throws_when_tool_role_is_used_without_tools_flow()
    {
        var messages = new List<LlmMessage>
        {
            new(LlmMessageRole.Tool, """{"success":true}""", ToolCallId: "call_1")
        };

        var act = () => OpenAiChatMessageMapper.ToOpenAiMessages(messages);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Use CompleteChatWithToolsAsync*");
    }

    [Fact]
    public void ToOpenAiTools_throws_when_tool_name_is_missing()
    {
        var tools = new List<LlmToolDefinition>
        {
            new(" ", "Creates a task.", """{"type":"object"}""")
        };

        var act = () => OpenAiChatToolMapper.ToOpenAiTools(tools);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Tool name is required*");
    }
}
