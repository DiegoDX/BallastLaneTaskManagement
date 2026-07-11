using Application.DTOs.DocAssistant;
using Application.DTOs.Llm;
using Application.Llm.DocAssistant;
using Application.Rag;
using FluentAssertions;

namespace Tests.Application.Llm.DocAssistant;

public sealed class DocAssistantPromptBuilderTests
{
    [Fact]
    public void BuildChatRequest_injects_context_into_system_message()
    {
        var messages = new List<DocAssistantMessageDto>
        {
            new("user", "How does authentication work?")
        };
        var contextChunks = new[]
        {
            CreateChunk("README.md", 5, "Authentication uses JWT Bearer tokens."),
            CreateChunk("Requirements.docx", 2, "All task endpoints require authentication.")
        };

        var chatRequest = DocAssistantPromptBuilder.BuildChatRequest(messages, contextChunks);
        var systemMessage = chatRequest.Messages[0].Content;

        systemMessage.Should().Contain("Answer ONLY from the provided documentation context.");
        systemMessage.Should().Contain("If context lacks the answer, say you don't know.");
        systemMessage.Should().Contain("Cite source file names. Do not invent facts.");
        systemMessage.Should().Contain("--- CONTEXT ---");
        systemMessage.Should().Contain("[README.md chunk 5] Authentication uses JWT Bearer tokens.");
        systemMessage.Should().Contain("[Requirements.docx chunk 2] All task endpoints require authentication.");
        systemMessage.Should().Contain("--- END CONTEXT ---");
    }

    [Fact]
    public void BuildChatRequest_includes_conversation_history()
    {
        var messages = new List<DocAssistantMessageDto>
        {
            new("user", "What is the project architecture?"),
            new("assistant", "It follows Clean Architecture."),
            new("user", "Which layers exist?")
        };

        var chatRequest = DocAssistantPromptBuilder.BuildChatRequest(messages, []);

        chatRequest.Messages.Should().HaveCount(4);
        chatRequest.Messages[0].Role.Should().Be(LlmMessageRole.System);
        chatRequest.Messages[1].Role.Should().Be(LlmMessageRole.User);
        chatRequest.Messages[1].Content.Should().Be("What is the project architecture?");
        chatRequest.Messages[2].Role.Should().Be(LlmMessageRole.Assistant);
        chatRequest.Messages[3].Role.Should().Be(LlmMessageRole.User);
        chatRequest.Messages[3].Content.Should().Be("Which layers exist?");
    }

    [Fact]
    public void BuildChatRequest_uses_temperature_0_2()
    {
        var messages = new List<DocAssistantMessageDto>
        {
            new("user", "Hello")
        };

        var chatRequest = DocAssistantPromptBuilder.BuildChatRequest(messages, []);

        chatRequest.Temperature.Should().Be(0.2);
    }

    [Fact]
    public void BuildChatRequest_trims_message_content()
    {
        var messages = new List<DocAssistantMessageDto>
        {
            new("user", "  How does authentication work?  ")
        };

        var chatRequest = DocAssistantPromptBuilder.BuildChatRequest(messages, []);

        chatRequest.Messages[1].Content.Should().Be("How does authentication work?");
    }

    [Fact]
    public void BuildChatRequest_throws_when_messages_is_null()
    {
        var act = () => DocAssistantPromptBuilder.BuildChatRequest(null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildChatRequest_throws_when_context_chunks_is_null()
    {
        var messages = new List<DocAssistantMessageDto>
        {
            new("user", "Hello")
        };

        var act = () => DocAssistantPromptBuilder.BuildChatRequest(messages, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("user", LlmMessageRole.User)]
    [InlineData("USER", LlmMessageRole.User)]
    [InlineData("assistant", LlmMessageRole.Assistant)]
    [InlineData("Assistant", LlmMessageRole.Assistant)]
    public void MapRole_maps_allowed_roles(string role, LlmMessageRole expectedRole)
    {
        var result = DocAssistantPromptBuilder.MapRole(role);

        result.Should().Be(expectedRole);
    }

    [Fact]
    public void MapRole_throws_for_unsupported_role()
    {
        var act = () => DocAssistantPromptBuilder.MapRole("system");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported doc assistant message role*");
    }

    private static DocumentChunk CreateChunk(string sourceFile, int chunkIndex, string content) =>
        new($"{sourceFile}-{chunkIndex}", sourceFile, chunkIndex, content, [1f, 0f, 0f]);
}
