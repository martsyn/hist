using Hist.Server.Collection;
using Microsoft.AspNetCore.Mvc;

namespace Hist.Server.Api;

public static class QueueEndpoints
{
    public static RouteGroupBuilder MapQueueEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", EnqueueTasks);
        group.MapGet("/", GetQueue);
        group.MapDelete("/{id:guid}", CancelTask);
        group.MapPatch("/{id:guid}", UpdateTask);
        return group;
    }

    private static IResult EnqueueTasks(
        [FromBody] EnqueueRequest req,
        CollectionQueue queue)
    {
        if (req.Symbols is not { Length: > 0 })
            return Results.BadRequest("symbols required");

        DataType dataType;
        try { dataType = DataTypeExtensions.FromApiString(req.DataType); }
        catch { return Results.BadRequest($"Unknown data_type: {req.DataType}"); }

        var priority = (TaskPriority)Math.Clamp(req.Priority ?? (int)TaskPriority.Normal, 0, 4);

        DateTimeOffset? start = null;
        if (req.Start is not null)
        {
            if (!DateTimeOffset.TryParse(req.Start, out var parsed))
                return Results.BadRequest($"Invalid start: {req.Start}");
            start = parsed;
        }

        var tasks = req.Symbols.Select(sym => new CollectionTask
        {
            Symbol = sym.ToUpperInvariant(),
            DataType = dataType,
            Start = start,
            Priority = priority
        }).ToList();

        foreach (var t in tasks) queue.Enqueue(t);

        return Results.Accepted(null, new
        {
            enqueued = tasks.Count,
            source = dataType.ToDescription()
        });
    }

    private static IResult GetQueue(CollectionQueue queue)
    {
        var pending = queue.GetPendingTasks().Select(MapTask);
        var active = queue.GetActiveTasks().Select(MapTask);
        return Results.Ok(new { pending, active });
    }

    private static IResult CancelTask(Guid id, CollectionQueue queue)
    {
        return queue.CancelPending(id)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static IResult UpdateTask(
        Guid id,
        [FromBody] UpdateTaskRequest req,
        CollectionQueue queue)
    {
        if (req.Priority is null) return Results.BadRequest("priority required");
        var priority = (TaskPriority)Math.Clamp(req.Priority.Value, 0, 4);
        return queue.UpdatePriority(id, priority)
            ? Results.Ok()
            : Results.NotFound();
    }

    private static object MapTask(CollectionTask t) => new
    {
        id = t.Id,
        symbol = t.Symbol,
        data_type = t.DataType.ToApiString(),
        priority = (int)t.Priority,
        status = t.Status.ToString().ToLowerInvariant(),
        enqueued_at = t.EnqueuedAt,
        error = t.ErrorMessage
    };

    record EnqueueRequest(string DataType, string[] Symbols, string? Start, int? Priority);
    record UpdateTaskRequest(int? Priority);
}
