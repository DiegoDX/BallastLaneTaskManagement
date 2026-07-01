namespace Application.Exceptions;

public sealed class LlmException : ApplicationException
{
    public LlmException(string message, bool isTransient)
        : base(message)
    {
        IsTransient = isTransient;
    }

    public LlmException(string message, Exception innerException, bool isTransient)
        : base(message, innerException)
    {
        IsTransient = isTransient;
    }

    public bool IsTransient { get; }
}
