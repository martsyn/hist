using Hist.Server.Data;
using Hist.Server.Data.Models;
using Microsoft.Extensions.Logging;
using OoplesFinance.YahooFinanceAPI;

namespace Hist.Server.Collection.Adapters.Yahoo;

public class YahooEarningsAdapter(
    ClickHouseRepository repo,
    YahooClient ooples,  // OoplesFinance.YahooFinanceAPI.YahooClient
    ILogger<YahooEarningsAdapter> logger
)
{
    public async Task<CollectionResult> ExecuteAsync(CollectionTask task, CancellationToken ct)
    {
        try
        {
            var symbol = task.Symbol;

            var data = await ooples.GetEarningsHistoryAsync(symbol);
            var list = data?.ToList();
            if (list is null || list.Count == 0)
                return new CollectionResult(true, 0);

            var earnings = list
                .Where(e => e.Quarter?.Raw.HasValue == true)
                .Select(e =>
                {
                    // Quarter.Raw is a Unix timestamp (seconds since epoch)
                    var unixTs = e.Quarter!.Raw!.Value;
                    var period = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)unixTs).UtcDateTime);

                    return new Earning(
                        symbol,
                        period,
                        (decimal?)e.EpsActual?.Raw,
                        (decimal?)e.EpsEstimate?.Raw,
                        null,   // OoplesFinance EarningsHistory doesn't include revenue
                        null,
                        null
                    );
                })
                .ToList();

            await repo.InsertEarningsAsync(earnings);

            if (earnings.Count > 0)
            {
                var coverage = new SeriesCoverage(
                    symbol,
                    DataType.Earnings.ToApiString(),
                    earnings.Min(e => e.Period),
                    null,
                    earnings.Max(e => e.Period),
                    null,
                    DateTimeOffset.UtcNow
                );
                await repo.UpsertCoverageAsync(coverage);
            }

            return new CollectionResult(true, earnings.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "YahooEarningsAdapter failed for {Symbol}", task.Symbol);
            return new CollectionResult(false, 0, ex.Message);
        }
    }
}
