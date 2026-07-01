using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Llm;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Application.Llm;

public sealed class TaskSuggestionResponseParserTests
{
    [Fact]
    public void Parse_returns_normalized_title_and_description_for_valid_json()
    {
        // Arrange
        const string content = """
            {"title":"  Prepare Q2 report  ","description":"  Include revenue breakdown  "}
            """;

        // Act
        var result = TaskSuggestionResponseParser.Parse(content);

        // Assert
        result.Should().BeEquivalentTo(new TaskSuggestionResponse(
            "Prepare Q2 report",
            "Include revenue breakdown"));
    }

    [Fact]
    public void Parse_accepts_case_insensitive_property_names()
    {
        // Arrange
        const string content = """{"Title":"Sync with design","Description":"Review mockups"}""";

        // Act
        var result = TaskSuggestionResponseParser.Parse(content);

        // Assert
        result.Title.Should().Be("Sync with design");
        result.Description.Should().Be("Review mockups");
    }

    [Fact]
    public void Parse_allows_empty_description()
    {
        // Arrange
        const string content = """{"title":"Book travel","description":""}""";

        // Act
        var result = TaskSuggestionResponseParser.Parse(content);

        // Assert
        result.Should().BeEquivalentTo(new TaskSuggestionResponse("Book travel", string.Empty));
    }

    [Fact]
    public void Parse_throws_when_content_is_empty()
    {
        // Act
        var act = () => TaskSuggestionResponseParser.Parse("   ");

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Be("Task suggestion response was empty.");
    }

    [Fact]
    public void Parse_throws_when_json_is_malformed()
    {
        // Act
        var act = () => TaskSuggestionResponseParser.Parse("not-json");

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Be("Task suggestion response could not be parsed.");
    }

    [Fact]
    public void Parse_throws_when_title_is_missing()
    {
        // Act
        var act = () => TaskSuggestionResponseParser.Parse("""{"description":"Only description"}""");

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Be("Title is required.");
    }

    [Fact]
    public void Parse_throws_when_title_exceeds_max_length()
    {
        // Arrange
        var longTitle = new string('a', TaskTitle.MaxLength + 1);
        var content = $$"""{"title":"{{longTitle}}","description":"details"}""";

        // Act
        var act = () => TaskSuggestionResponseParser.Parse(content);

        // Assert
        act.Should().Throw<ValidationException>()
            .Which.Message.Should().Contain("Title must be at most");
    }
}
