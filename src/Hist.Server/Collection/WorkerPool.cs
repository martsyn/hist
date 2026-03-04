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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxWorkers = settings.Tiingo.MaxThreads;
        using var semaphore = new SemaphoreSlim(maxWorkers, maxWorkers);

        logger.LogInformation("WorkerPool started with {MaxWorkers} max concurrent workers", maxWorkers);

        while (!stoppingToken.IsCancellationRequested)
        {
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
                    queue.CompleteTask(task.Id, result.Success, result.ErrorMessage);
                    if (result.Success)
                        logger.LogInformation("Completed {DataType} for {Symbol}: {Count} records",
                            task.DataType, task.Symbol, result.RecordsWritten);
                    else
                        logger.LogWarning("Failed {DataType} for {Symbol}: {Error}",
                            task.DataType, task.Symbol, result.ErrorMessage);
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
