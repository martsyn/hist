namespace Hist.Server.Collection;

public enum TaskStatus
{
    Pending,
    Active,
    Completed,
    Failed,
    Cancelled
}

public class CollectionTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Symbol { get; init; } = "";
    public DataType DataType { get; init; }
    public DateTimeOffset? Start { get; set; }
    public TaskPriority Priority { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? ErrorMessage { get; set; }

    public (string Symbol, DataType DataType) DedupeKey => (Symbol, DataType);
}
