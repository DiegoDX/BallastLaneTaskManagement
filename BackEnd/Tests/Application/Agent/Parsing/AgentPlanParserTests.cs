using Application.Agent.Parsing;
using Application.DTOs.Agent;
using Application.Exceptions;
using FluentAssertions;

namespace Tests.Application.Agent.Parsing;

public sealed class AgentPlanParserTests
{
    [Fact]
    public void Parse_ShouldReturnPlan_WhenJsonIsValid()
    {
        const string content = """
            {
              "goal": "Organize tasks",
              "steps": [
                { "order": 1, "description": "List tasks", "toolHint": "list_tasks" }
              ],
              "requiresApproval": false,
              "riskLevel": "low"
            }
            """;

        var plan = AgentPlanParser.Parse(content);

        plan.Goal.Should().Be("Organize tasks");
        plan.Steps.Should().HaveCount(1);
        plan.Steps[0].ToolHint.Should().Be("list_tasks");
        plan.RequiresApproval.Should().BeFalse();
        plan.RiskLevel.Should().Be("low");
    }

    [Fact]
    public void Parse_ShouldThrowValidationException_WhenJsonIsInvalid()
    {
        var act = () => AgentPlanParser.Parse("not json");

        act.Should().Throw<ValidationException>()
            .WithMessage("Agent plan response could not be parsed.");
    }

    [Fact]
    public void Parse_ShouldThrowValidationException_WhenStepsAreMissing()
    {
        const string content = """
            {
              "goal": "Organize tasks",
              "steps": [],
              "requiresApproval": false,
              "riskLevel": "low"
            }
            """;

        var act = () => AgentPlanParser.Parse(content);

        act.Should().Throw<ValidationException>()
            .WithMessage("At least one plan step is required.");
    }
}
