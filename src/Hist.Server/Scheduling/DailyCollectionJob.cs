using Hist.Server.Collection;
using Hist.Server.Data;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Hist.Server.Scheduling;

[DisallowConcurrentExecution]
public class DailyCollectionJob(
    CollectionQueue queue,
    ClickHouseRepository repo,
    ILogger<DailyCollectionJob> logger
) : IJob
{
    public const string DataTypeKey = "DataType";

    public async Task Execute(IJobExecutionContext context)
    {
        var dataTypeStr = context.MergedJobDataMap.GetString(DataTypeKey)
            ?? throw new InvalidOperationException("DataType not set in job data");

        var dataType = DataTypeExtensions.FromApiString(dataTypeStr);
        var symbols = await repo.GetAllSymbolsAsync();

        logger.LogInformation("DailyCollectionJob: queuing {Count} symbols for {DataType}",
            symbols.Count, dataType);

        foreach (var symbol in symbols)
        {
            queue.Enqueue(new CollectionTask
            {
                Symbol = symbol,
                DataType = dataType,
                Priority = TaskPriority.Low
            });
        }
    }
}
