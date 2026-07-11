namespace Infrastructure.Exceptions;

public sealed class DocumentExtractionException : InfrastructureException
{
    public DocumentExtractionException(string message)
        : base(message)
    {
    }

    public DocumentExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
