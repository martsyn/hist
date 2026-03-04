using Hist.Server.Collection.Adapters.Yahoo;

namespace Hist.Server.Collection.Adapters.Tiingo;

public class TiingoAdapter(
    TiingoEodAdapter eod,
    TiingoIntraAdapter intra,
    YahooEarningsAdapter earnings
) : IDataAdapter
{
    public Task<CollectionResult> ExecuteAsync(CollectionTask task, CancellationToken ct = default)
    {
        return task.DataType switch
        {
            DataType.DailyBars  => eod.ExecuteAsync(task, ct),
            DataType.Dividends  => eod.ExecuteAsync(task, ct),
            DataType.Splits     => eod.ExecuteAsync(task, ct),
            DataType.MinuteBars => intra.ExecuteAsync(task, ct),
            DataType.Earnings   => earnings.ExecuteAsync(task, ct),
            _ => throw new ArgumentException($"Unsupported DataType: {task.DataType}")
        };
    }
}
