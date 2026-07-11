using Application.DTOs.Llm;
using Application.Interfaces.Mcp;
using Application.Mcp;
using FluentAssertions;

namespace Tests.Application.Mcp;

public sealed class McpToolCatalogMapperTests
{
    private readonly McpToolCatalogMapper _sut = new();

    [Fact]
    public void MapTools_ShouldConvertDescriptorsToLlmToolDefinitions()
    {
        var descriptors = new List<McpToolDescriptor>
        {
            new("search_tasks", "Search tasks", """{"type":"object","properties":{"title":{"type":"string"}}}"""),
            new("create_task", "Create task", """{"type":"object","required":["title","dueDate"]}""")
        };

        var tools = _sut.MapTools(descriptors);

        tools.Should().HaveCount(2);
        tools[0].Should().BeEquivalentTo(new LlmToolDefinition(
            "search_tasks",
            "Search tasks",
            """{"type":"object","properties":{"title":{"type":"string"}}}"""));
        tools[1].Name.Should().Be("create_task");
    }
}
