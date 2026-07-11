namespace Application.Interfaces;

public interface IDocumentIndexer
{
    Task IndexAsync(CancellationToken cancellationToken = default);
}
