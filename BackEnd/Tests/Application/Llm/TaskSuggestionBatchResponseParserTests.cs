using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Llm;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Application.Llm;

public sealed class TaskSuggestionBatchResponseParserTests
{
    [Fact]
    public void Parse_returns_normalized_items_for_valid_json()
    {
        // Arrange
        const string content = """
            {
              "tasks": [
                {"title":"  Revisar facturas  ","description":"  Solo pendientes  "},
                {"title":"Enviar reporte","description":""}
              ]
            }
            """;

        // Act
        var result = TaskSuggestionBatchResponseParser.Parse(content, expectedCount: 2);

        // Assert
        result.Should().BeEquivalentTo([
            new TaskSuggestionBatchItem("Revisar facturas", "Solo pendientes"),
            new TaskSuggestionBatchItem("Enviar reporte", string.Empty)
        ]);
    }

    [Fact]
    public void Parse_accepts_case_insensitive_property_names()
    {
        // Arrange
        const string content = """
            {"Tasks":[{"Title":"Sync with design","Description":"Review mockups"}]}
            """;

        // Act
        var result = TaskSuggestionBatchResponseParser.Parse(content, expectedCount: 1);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new TaskSuggestionBatchItem("Sync with design", "Review mockups"));
    }

    [Fact]
    public void Parse_throws_when_task_count_does_not_match_expected_count()
    {
        // Arrange
        const string content = """
            {"tasks":[{"title":"Only one","description":"details"}]}
            """;

        // Act
        var act = () => TaskSuggestionBatchResponseParser.Parse(content, expectedCount: 3);

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Be("Task suggestion batch must contain exactly 3 tasks.");
    }

    [Fact]
    public void Parse_throws_when_json_is_malformed()
    {
        // Act
        var act = () => TaskSuggestionBatchResponseParser.Parse("not-json", expectedCount: 1);

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Be("Task suggestion response could not be parsed.");
    }

    [Fact]
    public void Parse_throws_when_title_exceeds_max_length()
    {
        // Arrange
        var longTitle = new string('a', TaskTitle.MaxLength + 1);
        var content = $$"""{"tasks":[{"title":"{{longTitle}}","description":"details"}]}""";

        // Act
        var act = () => TaskSuggestionBatchResponseParser.Parse(content, expectedCount: 1);

        // Assert
        act.Should().Throw<ValidationException>()
            .Which.Message.Should().Contain("Title must be at most");
    }

    [Fact]
    public void Parse_throws_when_content_is_empty()
    {
        // Act
        var act = () => TaskSuggestionBatchResponseParser.Parse("   ", expectedCount: 1);

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Be("Task suggestion response was empty.");
    }
}
