namespace Infrastructure.Configuration;

public sealed class RagSettings
{
    public const string SectionName = "Rag";

    public string DocumentationPath { get; init; } = "Documentation";
}
