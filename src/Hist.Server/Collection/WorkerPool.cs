using Hist.Server.Collection.Adapters;
using Hist.Server.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hist.Server.Collection;

public class WorkerPool(
    CollectionQueue queue,
    IDataAdapter adapter,
    AppSettings settings,
    ILogger<WorkerPool> logger
) : BackgroundService
{
    // Ticks of the UTC time after which collection may resume (0 = not suspended).
    private long _suspendUntilTicks = 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxWorkers = settings.Tiingo.MaxThreads;
        using var semaphore = new SemaphoreSlim(maxWorkers, maxWorkers);

        logger.LogInformation("WorkerPool started with {MaxWorkers} max concurrent workers", maxWorkers);

        while (!stoppingToken.IsCancellationRequested)
        {
            var suspendUntil = new DateTimeOffset(Interlocked.Read(ref _suspendUntilTicks), TimeSpan.Zero);
            if (DateTimeOffset.UtcNow < suspendUntil)
            {
                await Task.Delay(5000, stoppingToken);
                continue;
            }

            if (!queue.TryDequeue(out var task))
            {
                await Task.Delay(500, stoppingToken);
                continue;
            }

            await semaphore.WaitAsync(stoppingToken);
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Starting {DataType} for {Symbol}", task!.DataType, task.Symbol);
                    var result = await adapter.ExecuteAsync(task, stoppingToken);

                    if (result.RateLimited)
                    {
                        // Re-enqueue the task so it retries after the suspend window
                        queue.Enqueue(new CollectionTask
                        {
                            Symbol   = task.Symbol,
                            DataType = task.DataType,
                            Start    = task.Start,
                            Priority = task.Priority
                        });
                        // Suspend until the next hour boundary
                        var nextHour = DateTimeOffset.UtcNow.AddHours(1);
                        nextHour = new DateTimeOffset(nextHour.Year, nextHour.Month, nextHour.Day,
                            nextHour.Hour, 0, 0, TimeSpan.Zero);
                        Interlocked.Exchange(ref _suspendUntilTicks, nextHour.Ticks);
                        logger.LogWarning("Tiingo rate limit hit — suspending until {Until:HH:mm} UTC", nextHour);
                        queue.CompleteTask(task.Id, false, result.ErrorMessage);
                    }
                    else
                    {
                        queue.CompleteTask(task.Id, result.Success, result.ErrorMessage);
                        if (result.Success)
                            logger.LogInformation("Completed {DataType} for {Symbol}: {Count} records",
                                task.DataType, task.Symbol, result.RecordsWritten);
                        else
                            logger.LogWarning("Failed {DataType} for {Symbol}: {Error}",
                                task.DataType, task.Symbol, result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    queue.CompleteTask(task!.Id, false, ex.Message);
                    logger.LogError(ex, "Unhandled error in worker for {Symbol}/{DataType}",
                        task.Symbol, task.DataType);
                }
                finally
                {
                    semaphore.Release();
                }
            }, stoppingToken);
        }
    }
}
