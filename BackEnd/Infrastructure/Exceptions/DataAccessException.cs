namespace Infrastructure.Exceptions;

public sealed class DataAccessException : InfrastructureException
{
    public DataAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
