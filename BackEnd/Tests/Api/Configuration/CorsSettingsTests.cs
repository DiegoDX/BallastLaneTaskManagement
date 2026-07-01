using Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Tests.Api.Configuration;

public sealed class CorsSettingsBindingTests
{
    [Fact]
    public void Bind_reads_allowed_origins_from_configuration_section()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
                ["Cors:AllowedOrigins:1"] = "https://app.company.com"
            })
            .Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.AllowedOrigins.Should().Equal(
            "http://localhost:4200",
            "https://app.company.com");
    }

    [Fact]
    public void Bind_returns_empty_origins_when_section_is_missing()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()
            ?? new CorsSettings();

        // Assert
        settings.AllowedOrigins.Should().BeEmpty();
    }
}

public sealed class CorsSettingsValidatorTests
{
    [Fact]
    public void Validate_succeeds_for_valid_production_origins()
    {
        // Arrange
        var validator = CreateValidator(environments: ["Production"]);
        var settings = new CorsSettings
        {
            AllowedOrigins = ["https://app.company.com"]
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_fails_in_production_when_no_origins_are_configured()
    {
        // Arrange
        var validator = CreateValidator(environments: ["Production"]);
        var settings = new CorsSettings();

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("at least one origin in Production");
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("/relative-path")]
    [InlineData("ftp://files.company.com")]
    [InlineData("http://localhost:4200/app")]
    [InlineData("http://localhost:4200#fragment")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_fails_when_origin_format_is_invalid(string invalidOrigin)
    {
        // Arrange
        var validator = CreateValidator(environments: ["Development"]);
        var settings = new CorsSettings
        {
            AllowedOrigins = [invalidOrigin]
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("is not a valid origin");
    }

    [Fact]
    public void Validate_allows_empty_origins_outside_production()
    {
        // Arrange
        var validator = CreateValidator(environments: ["Development"]);
        var settings = new CorsSettings();

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("https://app.company.com")]
    [InlineData("https://app.company.com:8443")]
    public void CorsOriginValidator_accepts_valid_origins(string origin)
    {
        CorsOriginValidator.IsValidOrigin(origin).Should().BeTrue();
    }

    private static CorsSettingsValidator CreateValidator(params string[] environments)
    {
        var environment = new HostingEnvironment
        {
            EnvironmentName = environments[0]
        };

        return new CorsSettingsValidator(environment);
    }
}
