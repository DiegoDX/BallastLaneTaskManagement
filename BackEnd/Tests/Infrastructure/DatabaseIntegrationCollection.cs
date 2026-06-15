namespace Tests.Infrastructure;

[CollectionDefinition("DatabaseIntegration")]
public sealed class DatabaseIntegrationCollection : ICollectionFixture<IntegrationDatabaseFixture>
{
}
