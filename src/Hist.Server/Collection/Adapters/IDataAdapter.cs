namespace Hist.Server.Collection.Adapters;

public interface IDataAdapter
{
    Task<CollectionResult> ExecuteAsync(CollectionTask task, CancellationToken ct = default);
}
