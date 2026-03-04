namespace Hist.Server.Collection;

public record CollectionResult(
    bool Success,
    int RecordsWritten,
    string? ErrorMessage = null
);
