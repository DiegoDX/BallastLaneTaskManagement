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
        var result = TaskSuggestionBatchResponseParser.Parse(content);

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
        var result = TaskSuggestionBatchResponseParser.Parse(content);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new TaskSuggestionBatchItem("Sync with design", "Review mockups"));
    }

    [Fact]
    public void Parse_accepts_dynamic_task_count()
    {
        // Arrange
        const string content = """
            {
              "tasks": [
                {"title":"Task one","description":"details"},
                {"title":"Task two","description":"details"},
                {"title":"Task three","description":"details"}
              ]
            }
            """;

        // Act
        var result = TaskSuggestionBatchResponseParser.Parse(content);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_throws_when_task_count_exceeds_max_batch_size()
    {
        // Arrange
        var tasks = string.Join(',', Enumerable.Range(1, TaskSuggestionLimits.MaxBatchSize + 1)
            .Select(index => $$"""{"title":"Task {{index}}","description":"details"}"""));
        var content = $$"""{"tasks":[{{tasks}}]}""";

        // Act
        var act = () => TaskSuggestionBatchResponseParser.Parse(content);

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Be(
            $"Task suggestion batch must contain at most {TaskSuggestionLimits.MaxBatchSize} tasks.");
    }

    [Fact]
    public void Parse_ignores_extra_properties_on_tasks()
    {
        // Arrange
        const string content = """
            {
              "tasks": [
                {
                  "title":"Deploy release",
                  "description":"Push to production",
                  "priority":"High",
                  "estimatedHours":4
                }
              ]
            }
            """;

        // Act
        var result = TaskSuggestionBatchResponseParser.Parse(content);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new TaskSuggestionBatchItem("Deploy release", "Push to production"));
    }

    [Fact]
    public void Parse_throws_when_tasks_array_is_empty()
    {
        // Arrange
        const string content = """{"tasks":[]}""";

        // Act
        var act = () => TaskSuggestionBatchResponseParser.Parse(content);

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Be("Task suggestion batch must contain at least one task.");
    }

    [Fact]
    public void Parse_throws_when_title_is_missing()
    {
        // Arrange
        const string content = """{"tasks":[{"title":"   ","description":"details"}]}""";

        // Act
        var act = () => TaskSuggestionBatchResponseParser.Parse(content);

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Be("Title is required.");
    }

    [Fact]
    public void Parse_throws_when_json_is_malformed()
    {
        // Act
        var act = () => TaskSuggestionBatchResponseParser.Parse("not-json");

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
        var act = () => TaskSuggestionBatchResponseParser.Parse(content);

        // Assert
        act.Should().Throw<ValidationException>()
            .Which.Message.Should().Contain("Title must be at most");
    }

    [Fact]
    public void Parse_throws_when_content_is_empty()
    {
        // Act
        var act = () => TaskSuggestionBatchResponseParser.Parse("   ");

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Be("Task suggestion response was empty.");
    }
}
